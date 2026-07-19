using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SapPaymentsAdapter.Api.Middleware;
using SapPaymentsAdapter.Api.Services.GlAccounts;
using SapPaymentsAdapter.Api.Services.Payments;
using SapPaymentsAdapter.Api.Services.Sap;
using SapPaymentsAdapter.Api.Services.Vault;
using SapPaymentsAdapter.Api.Services.Vendors;

var builder = WebApplication.CreateBuilder(args);

// --- Vault ---
builder.Services.AddSingleton<IVaultService, VaultService>();

// --- SAP connector ---
// SapBapiSimulator today; swap for a real NCo-based implementation once RFC
// destination access exists. Nothing above ISapConnector changes.
builder.Services.AddSingleton<ISapConnector, SapBapiSimulator>();

// --- Domain services ---
builder.Services.AddScoped<IPaymentsService, PaymentsService>();
builder.Services.AddScoped<IVendorsService, VendorsService>();
builder.Services.AddScoped<IGlAccountsService, GlAccountsService>();

// --- JWT bearer authentication ---
// This API validates tokens - it does not issue them in production. A
// downstream module (Payments, etc.) would authenticate against the bank's
// real identity provider (e.g. an internal STS or Azure AD) and present the
// resulting JWT here. The signing key below is symmetric (HMAC) purely
// because there's no real IDP to trust in this hands-on setup; production
// should validate against an IDP's public signing keys (RS256 + JWKS
// endpoint), not a shared secret, and should also check `aud`/`scope`
// claims to enforce which modules may call which operations rather than
// treating every valid token as fully trusted.
var jwtSigningKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY")
    ?? "dev-only-signing-key-not-for-production-use-replace-me";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "sap-payments-adapter",
            ValidateAudience = true,
            ValidAudience = "sap-payments-adapter-clients",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SAP Payments Adapter API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste a token from POST /dev/token (Development environment only)",
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Dev-only token minting endpoint so Bruno / manual testing can get a
    // valid bearer token without a real IDP in the loop. Registered ONLY
    // under Development - this must never exist in a real deployed
    // environment, since it would let anyone mint a fully trusted token.
    app.MapPost("/dev/token", (string clientId = "payments-module") =>
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "sap-payments-adapter",
            audience: "sap-payments-adapter-clients",
            claims: new[] { new Claim("client_id", clientId) },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);
        return Results.Ok(new { access_token = handler.WriteToken(token), expires_in = 300 });
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Fetch SAP RFC credentials from Vault at startup so a misconfigured Vault
// path fails fast instead of surfacing as a mystery 502 on first request.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var vault = scope.ServiceProvider.GetRequiredService<IVaultService>();
        var sapCreds = await vault.GetSecretAsync("sap-adapter/dev/rfc-credentials");
        logger.LogInformation("SAP RFC credentials loaded from Vault:");
        foreach (var (k, v) in sapCreds)
        {
            logger.LogInformation("  {Key} = {MaskedValue}", k, v.Length >= 2 ? v[..2] + "****" : "****");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning("Could not load SAP credentials from Vault (expected if Vault dev server isn't running yet): {Message}", ex.Message);
    }
}

app.Run();

public partial class Program { } // exposed for WebApplicationFactory in integration tests

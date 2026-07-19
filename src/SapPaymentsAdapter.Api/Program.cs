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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SAP Payments Adapter API", Version = "v1" });
});

var app = builder.Build();

app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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

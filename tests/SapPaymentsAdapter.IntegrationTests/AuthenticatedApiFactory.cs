using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SapPaymentsAdapter.IntegrationTests;

/// <summary>
/// Wraps WebApplicationFactory&lt;Program&gt; for tests that need an authenticated
/// client. Two things it does beyond the default factory:
///
/// 1. Explicitly forces the Development environment. WebApplicationFactory is
///    documented to default to Development, but that default is unreliable
///    for apps using minimal hosting (top-level statements, as Program.cs
///    does here) - see https://github.com/dotnet/aspnetcore/issues/33889.
///    Program.cs only registers POST /dev/token when IsDevelopment() is
///    true, so this is forced explicitly rather than left to chance.
/// 2. Mints a bearer token once per test class (via that same /dev/token
///    endpoint) and hands out clients with it pre-attached, so individual
///    test methods don't each need to know about authentication.
///
/// Tokens minted by /dev/token expire after 5 minutes (see Program.cs) -
/// fine for a single test class run, but this would need refresh logic if
/// a class's tests ever collectively took longer than that.
/// </summary>
public class AuthenticatedApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string? _accessToken;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        base.ConfigureWebHost(builder);
    }

    public async Task InitializeAsync()
    {
        using var unauthenticatedClient = CreateClient();
        var response = await unauthenticatedClient.PostAsync("/dev/token?clientId=integration-tests", content: null);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DevTokenResponse>();
        _accessToken = payload!.AccessToken;
    }

    /// <summary>Client with the dev bearer token pre-attached.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return client;
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    private sealed record DevTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

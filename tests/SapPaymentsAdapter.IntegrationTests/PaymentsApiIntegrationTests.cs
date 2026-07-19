using System.Net;
using System.Net.Http.Json;
using SapPaymentsAdapter.Api.Generated;
using Xunit;

namespace SapPaymentsAdapter.IntegrationTests;

/// <summary>
/// Runs the full ASP.NET Core pipeline in-process (routing, DI, model
/// binding, middleware) against the real WebApplicationFactory host, with
/// SapBapiSimulator wired in exactly as it is at runtime - this is what
/// makes it an integration test rather than a mock-heavy unit test. Vault
/// is not called here because Program.cs's startup secret fetch is wrapped
/// in try/catch and logs a warning rather than failing startup, which is
/// intentional so integration tests don't require a live Vault dev server.
/// </summary>
public class PaymentsApiIntegrationTests : IClassFixture<AuthenticatedApiFactory>
{
    private readonly HttpClient _client;

    public PaymentsApiIntegrationTests(AuthenticatedApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task PostPayment_KnownVendor_Returns202WithSapDocumentNumber()
    {
        var request = new PaymentInitiationRequest
        {
            CompanyCode = "AU01",
            VendorId = "100001",
            Amount = 2500.0,
            Currency = "AUD",
            PaymentMethod = PaymentInitiationRequestPaymentMethod.BANK_TRANSFER,
            DueDate = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            Reference = "INV-2001",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/payments", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaymentInitiationResponse>();
        Assert.Equal(PaymentInitiationResponseStatus.POSTED, body!.Status);
        Assert.NotNull(body.SapDocumentNumber);
    }

    [Fact]
    public async Task PostPayment_BlockedVendor_Returns400WithSapErrorMessage()
    {
        var request = new PaymentInitiationRequest
        {
            CompanyCode = "AU01",
            VendorId = "100002",
            Amount = 500.0,
            Currency = "AUD",
            PaymentMethod = PaymentInitiationRequestPaymentMethod.BANK_TRANSFER,
            DueDate = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var response = await _client.PostAsJsonAsync("/api/v1/payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaymentInitiationResponse>();
        Assert.Equal(PaymentInitiationResponseStatus.REJECTED, body!.Status);
    }

    [Fact]
    public async Task GetVendor_UnknownVendor_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/vendors/000000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGlAccountBalance_ReturnsTwelvePeriodBalances()
    {
        var response = await _client.GetAsync("/api/v1/gl-accounts/400000/balance?fiscalYear=2026");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GlAccountBalance>();
        Assert.Equal(12, body!.PeriodBalances.Count);
    }
}

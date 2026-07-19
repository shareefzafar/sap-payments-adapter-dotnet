using Microsoft.Extensions.Logging.Abstractions;
using SapPaymentsAdapter.Api.Services.Sap;
using Xunit;

namespace SapPaymentsAdapter.UnitTests;

public class SapBapiSimulatorTests
{
    private readonly SapBapiSimulator _sut = new(NullLogger<SapBapiSimulator>.Instance);

    [Fact]
    public async Task PostPaymentAsync_KnownActiveVendor_ReturnsPostedDocumentWithSuccessReturn()
    {
        var request = new PaymentPostingRequest("AU01", "100001", 1500m, "AUD", "BANK_TRANSFER", new DateOnly(2026, 8, 1), "INV-1001");

        var result = await _sut.PostPaymentAsync(request, CancellationToken.None);

        Assert.False(result.HasErrors);
        Assert.NotNull(result.Data);
        Assert.StartsWith("51", result.Data!.SapDocumentNumber);
        Assert.Contains(result.Returns, r => r.Type == "S");
    }

    [Fact]
    public async Task PostPaymentAsync_UnknownVendor_ReturnsBapiErrorRow()
    {
        var request = new PaymentPostingRequest("AU01", "999999", 500m, "AUD", "BANK_TRANSFER", new DateOnly(2026, 8, 1), null);

        var result = await _sut.PostPaymentAsync(request, CancellationToken.None);

        Assert.True(result.HasErrors);
        Assert.Null(result.Data);
        Assert.Contains(result.Returns, r => r.Type == "E" && r.Message.Contains("does not exist"));
    }

    [Fact]
    public async Task PostPaymentAsync_BlockedVendor_ReturnsBapiErrorRowNotException()
    {
        // The point of this test: a blocked vendor is a business-rule
        // rejection, not a thrown exception - callers must check the
        // RETURN table, exactly like real BAPI consumers have to.
        var request = new PaymentPostingRequest("AU01", "100002", 500m, "AUD", "BANK_TRANSFER", new DateOnly(2026, 8, 1), null);

        var result = await _sut.PostPaymentAsync(request, CancellationToken.None);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Returns, r => r.Type == "E" && r.Message.Contains("blocked"));
    }

    [Fact]
    public async Task GetVendorDetailAsync_UnknownVendor_ReturnsError()
    {
        var result = await _sut.GetVendorDetailAsync("000000", CancellationToken.None);

        Assert.True(result.HasErrors);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetGlBalancesAsync_ReturnsTwelvePeriods()
    {
        var result = await _sut.GetGlBalancesAsync("400000", 2026, CancellationToken.None);

        Assert.False(result.HasErrors);
        Assert.Equal(12, result.Data!.PeriodBalances.Count);
    }
}

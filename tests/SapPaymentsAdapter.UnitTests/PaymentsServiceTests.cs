using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SapPaymentsAdapter.Api.Generated;
using SapPaymentsAdapter.Api.Services.Payments;
using SapPaymentsAdapter.Api.Services.Sap;
using Xunit;

namespace SapPaymentsAdapter.UnitTests;

public class PaymentsServiceTests
{
    private readonly Mock<ISapConnector> _sapConnectorMock = new();

    private PaymentsService CreateSut() => new(_sapConnectorMock.Object, NullLogger<PaymentsService>.Instance);

    [Fact]
    public async Task InitiatePaymentAsync_SapReturnsSuccess_MapsToPostedStatus()
    {
        _sapConnectorMock
            .Setup(c => c.PostPaymentAsync(It.IsAny<PaymentPostingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BapiResult<PaymentPostingResult>
            {
                Data = new PaymentPostingResult("5100001", 2026),
                Returns = new() { new SapPaymentsAdapter.Api.Services.Sap.BapiReturnMessage("S", "FI", "312", "Document posted") },
            });

        var request = new PaymentInitiationRequest
        {
            CompanyCode = "AU01",
            VendorId = "100001",
            Amount = 1500.0,
            Currency = "AUD",
            PaymentMethod = PaymentInitiationRequestPaymentMethod.BANK_TRANSFER,
            DueDate = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            Reference = "INV-1001",
        };

        var response = await CreateSut().InitiatePaymentAsync(request, CancellationToken.None);

        Assert.Equal(PaymentInitiationResponseStatus.POSTED, response.Status);
        Assert.Equal("5100001", response.SapDocumentNumber);
    }

    [Fact]
    public async Task InitiatePaymentAsync_SapReturnsErrorRow_MapsToRejectedWithoutThrowing()
    {
        _sapConnectorMock
            .Setup(c => c.PostPaymentAsync(It.IsAny<PaymentPostingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BapiResult<PaymentPostingResult>
            {
                Data = null,
                Returns = new() { new SapPaymentsAdapter.Api.Services.Sap.BapiReturnMessage("E", "FI", "042", "Vendor is blocked for payment") },
            });

        var request = new PaymentInitiationRequest
        {
            CompanyCode = "AU01",
            VendorId = "100002",
            Amount = 500.0,
            Currency = "AUD",
            PaymentMethod = PaymentInitiationRequestPaymentMethod.BANK_TRANSFER,
            DueDate = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
        };

        var response = await CreateSut().InitiatePaymentAsync(request, CancellationToken.None);

        Assert.Equal(PaymentInitiationResponseStatus.REJECTED, response.Status);
        Assert.Null(response.SapDocumentNumber);
        Assert.Contains(response.ReturnMessages, m => m.Type == BapiReturnMessageType.E);
    }
}

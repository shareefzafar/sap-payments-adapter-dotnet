using SapPaymentsAdapter.Api.Generated;
using SapPaymentsAdapter.Api.Mappers;
using SapPaymentsAdapter.Api.Services.Sap;

namespace SapPaymentsAdapter.Api.Services.Payments;

public interface IPaymentsService
{
    Task<PaymentInitiationResponse> InitiatePaymentAsync(PaymentInitiationRequest request, CancellationToken ct);
    Task<PaymentStatusResponse?> GetPaymentStatusAsync(string paymentId, CancellationToken ct);
}

public class PaymentsService : IPaymentsService
{
    private readonly ISapConnector _sapConnector;
    private readonly ILogger<PaymentsService> _logger;

    public PaymentsService(ISapConnector sapConnector, ILogger<PaymentsService> logger)
    {
        _sapConnector = sapConnector;
        _logger = logger;
    }

    public async Task<PaymentInitiationResponse> InitiatePaymentAsync(PaymentInitiationRequest request, CancellationToken ct)
    {
        var result = await _sapConnector.PostPaymentAsync(PaymentsMapper.ToSapRequest(request), ct);
        var response = PaymentsMapper.ToApiResponse(result);

        if (response.Status == PaymentInitiationResponseStatus.REJECTED)
        {
            _logger.LogWarning(
                "Payment posting rejected: {Messages}",
                string.Join("; ", response.ReturnMessages.Select(m => m.Message)));
        }

        return response;
    }

    public async Task<PaymentStatusResponse?> GetPaymentStatusAsync(string paymentId, CancellationToken ct)
    {
        var result = await _sapConnector.GetPaymentStatusAsync(paymentId, ct);
        return PaymentsMapper.ToApiStatusResponse(paymentId, result);
    }
}

using SapPaymentsAdapter.Api.Generated;
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
        var result = await _sapConnector.PostPaymentAsync(
            new PaymentPostingRequest(
                request.CompanyCode,
                request.VendorId,
                (decimal)request.Amount,
                request.Currency,
                request.PaymentMethod.ToString(),
                DateOnly.FromDateTime(request.DueDate.Date),
                request.Reference),
            ct);

        var messages = result.Returns.Select(r => new Generated.BapiReturnMessage
        {
            Type = Enum.Parse<BapiReturnMessageType>(r.Type),
            Id = r.Id,
            Number = r.Number,
            Message = r.Message,
        }).ToList();

        if (result.HasErrors || result.Data is null)
        {
            _logger.LogWarning("Payment posting rejected: {Messages}", string.Join("; ", messages.Select(m => m.Message)));
            return new PaymentInitiationResponse
            {
                Status = PaymentInitiationResponseStatus.REJECTED,
                ReturnMessages = messages,
            };
        }

        return new PaymentInitiationResponse
        {
            SapDocumentNumber = result.Data.SapDocumentNumber,
            FiscalYear = result.Data.FiscalYear,
            Status = PaymentInitiationResponseStatus.POSTED,
            ReturnMessages = messages,
        };
    }

    public async Task<PaymentStatusResponse?> GetPaymentStatusAsync(string paymentId, CancellationToken ct)
    {
        var result = await _sapConnector.GetPaymentStatusAsync(paymentId, ct);
        var messages = result.Returns.Select(r => new Generated.BapiReturnMessage
        {
            Type = Enum.Parse<BapiReturnMessageType>(r.Type),
            Id = r.Id,
            Number = r.Number,
            Message = r.Message,
        }).ToList();

        if (result.HasErrors || result.Data is null)
            return null;

        return new PaymentStatusResponse
        {
            PaymentId = paymentId,
            SapDocumentNumber = result.Data.SapDocumentNumber,
            Status = Enum.Parse<PaymentStatusResponseStatus>(result.Data.Status),
            ClearedDate = result.Data.ClearedDate is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue)) : null,
            ReturnMessages = messages,
        };
    }
}

using SapPaymentsAdapter.Api.Generated;
using Sap = SapPaymentsAdapter.Api.Services.Sap;

namespace SapPaymentsAdapter.Api.Mappers;

/// <summary>
/// Glue code translating between the REST contract (Generated.* - generated
/// from openapi/sap-payments-adapter.yaml, don't hand-edit) and the
/// SAP-facing shapes in Services.Sap.* (ISapConnector). Pulled out of
/// PaymentsService so that class is left doing orchestration only (call the
/// connector, decide what to do with the result) rather than mixing that
/// with field-by-field translation.
/// </summary>
public static class PaymentsMapper
{
    public static Sap.PaymentPostingRequest ToSapRequest(PaymentInitiationRequest request) =>
        new(
            request.CompanyCode,
            request.VendorId,
            (decimal)request.Amount,
            request.Currency,
            request.PaymentMethod.ToString(),
            DateOnly.FromDateTime(request.DueDate.Date),
            request.Reference);

    /// <summary>
    /// Shared by PaymentsService.InitiatePaymentAsync and
    /// GlAccountsService.PostEntryAsync - both post through a BAPI that
    /// returns BapiResult&lt;PaymentPostingResult&gt;, and both map to the
    /// same PaymentInitiationResponse shape per the OpenAPI spec.
    /// </summary>
    public static PaymentInitiationResponse ToApiResponse(Sap.BapiResult<Sap.PaymentPostingResult> result)
    {
        var messages = BapiMessageMapper.ToApiMessages(result.Returns);

        if (result.HasErrors || result.Data is null)
        {
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

    public static PaymentStatusResponse? ToApiStatusResponse(string paymentId, Sap.BapiResult<Sap.PaymentStatusResult> result)
    {
        if (result.HasErrors || result.Data is null)
            return null;

        return new PaymentStatusResponse
        {
            PaymentId = paymentId,
            SapDocumentNumber = result.Data.SapDocumentNumber,
            Status = Enum.Parse<PaymentStatusResponseStatus>(result.Data.Status),
            ClearedDate = result.Data.ClearedDate is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue)) : null,
            ReturnMessages = BapiMessageMapper.ToApiMessages(result.Returns),
        };
    }
}

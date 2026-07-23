using SapPaymentsAdapter.Api.Generated;
using Sap = SapPaymentsAdapter.Api.Services.Sap;

namespace SapPaymentsAdapter.Api.Mappers;

/// <summary>
/// Glue code: translates SAP's raw BAPIRET2 rows (Services.Sap.BapiReturnMessage,
/// as returned by ISapConnector) into the REST-facing shape declared by the
/// OpenAPI contract (Generated.BapiReturnMessage). This one mapping was
/// duplicated verbatim in PaymentsService (twice) and GlAccountsService
/// (once) before being pulled out here - a good example of translation
/// logic that belongs in its own layer rather than copy-pasted at every
/// call site that touches a BAPI result.
/// </summary>
public static class BapiMessageMapper
{
    public static List<BapiReturnMessage> ToApiMessages(IEnumerable<Sap.BapiReturnMessage> sapMessages) =>
        sapMessages.Select(r => new BapiReturnMessage
        {
            Type = Enum.Parse<BapiReturnMessageType>(r.Type),
            Id = r.Id,
            Number = r.Number,
            Message = r.Message,
        }).ToList();
}

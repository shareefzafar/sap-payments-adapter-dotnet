using SapPaymentsAdapter.Api.Generated;
using SapPaymentsAdapter.Api.Mappers;
using SapPaymentsAdapter.Api.Services.Sap;

namespace SapPaymentsAdapter.Api.Services.GlAccounts;

public interface IGlAccountsService
{
    /// <summary>
    /// Fetches the period balances for a GL account in a fiscal year via
    /// BAPI_GL_GETBALANCES. Returns <c>null</c> when SAP reports the account
    /// does not exist or the BAPIRET2 return table carries an error row, so
    /// the caller can surface a 404 rather than an empty balance.
    /// </summary>
    Task<GlAccountBalance?> GetBalanceAsync(string glAccount, int fiscalYear, CancellationToken ct);

    /// <summary>
    /// Posts a GL journal entry via BAPI_ACC_DOCUMENT_POST. The returned
    /// <see cref="PaymentInitiationResponse"/> is REJECTED (with the SAP
    /// return messages) when the posting fails, or POSTED with the SAP
    /// document number on success.
    /// </summary>
    Task<PaymentInitiationResponse> PostEntryAsync(string glAccount, Generated.GlPostingRequest request, CancellationToken ct);
}

public class GlAccountsService : IGlAccountsService
{
    private readonly ISapConnector _sapConnector;
    public GlAccountsService(ISapConnector sapConnector) => _sapConnector = sapConnector;

    public async Task<GlAccountBalance?> GetBalanceAsync(string glAccount, int fiscalYear, CancellationToken ct)
    {
        var result = await _sapConnector.GetGlBalancesAsync(glAccount, fiscalYear, ct);
        return result.HasErrors || result.Data is null ? null : GlAccountsMapper.ToApiModel(result.Data);
    }

    public async Task<PaymentInitiationResponse> PostEntryAsync(string glAccount, Generated.GlPostingRequest request, CancellationToken ct)
    {
        var result = await _sapConnector.PostGlEntryAsync(GlAccountsMapper.ToSapRequest(glAccount, request), ct);
        return PaymentsMapper.ToApiResponse(result);
    }
}

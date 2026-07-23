using SapPaymentsAdapter.Api.Generated;
using SapPaymentsAdapter.Api.Mappers;
using SapPaymentsAdapter.Api.Services.Sap;

namespace SapPaymentsAdapter.Api.Services.GlAccounts;

public interface IGlAccountsService
{
    Task<GlAccountBalance?> GetBalanceAsync(string glAccount, int fiscalYear, CancellationToken ct);
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

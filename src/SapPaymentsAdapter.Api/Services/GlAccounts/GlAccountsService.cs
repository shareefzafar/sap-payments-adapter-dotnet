using SapPaymentsAdapter.Api.Generated;
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
        if (result.HasErrors || result.Data is null) return null;

        var d = result.Data;
        return new GlAccountBalance
        {
            GlAccount = d.GlAccount,
            FiscalYear = d.FiscalYear,
            Currency = d.Currency,
            PeriodBalances = d.PeriodBalances.Select(p => new PeriodBalances
            {
                Period = p.Period,
                Debit = (double)p.Debit,
                Credit = (double)p.Credit,
                Balance = (double)p.Balance,
            }).ToList(),
        };
    }

    public async Task<PaymentInitiationResponse> PostEntryAsync(string glAccount, Generated.GlPostingRequest request, CancellationToken ct)
    {
        var result = await _sapConnector.PostGlEntryAsync(
            new Sap.GlPostingRequest(
                request.CompanyCode,
                glAccount,
                (decimal)request.Amount,
                request.Currency,
                DateOnly.FromDateTime(request.PostingDate.Date),
                request.DocType,
                request.Text),
            ct);

        var messages = result.Returns.Select(r => new Generated.BapiReturnMessage
        {
            Type = Enum.Parse<BapiReturnMessageType>(r.Type),
            Id = r.Id,
            Number = r.Number,
            Message = r.Message,
        }).ToList();

        if (result.HasErrors || result.Data is null)
            return new PaymentInitiationResponse { Status = PaymentInitiationResponseStatus.REJECTED, ReturnMessages = messages };

        return new PaymentInitiationResponse
        {
            SapDocumentNumber = result.Data.SapDocumentNumber,
            FiscalYear = result.Data.FiscalYear,
            Status = PaymentInitiationResponseStatus.POSTED,
            ReturnMessages = messages,
        };
    }
}

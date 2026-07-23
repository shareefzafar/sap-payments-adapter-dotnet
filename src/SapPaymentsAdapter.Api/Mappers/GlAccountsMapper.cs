using SapPaymentsAdapter.Api.Generated;
using Sap = SapPaymentsAdapter.Api.Services.Sap;

namespace SapPaymentsAdapter.Api.Mappers;

/// <summary>Glue code: GL account request/balance <-> REST contract shapes.</summary>
public static class GlAccountsMapper
{
    public static Sap.GlPostingRequest ToSapRequest(string glAccount, GlPostingRequest request) =>
        new(
            request.CompanyCode,
            glAccount,
            (decimal)request.Amount,
            request.Currency,
            DateOnly.FromDateTime(request.PostingDate.Date),
            request.DocType,
            request.Text);

    public static GlAccountBalance ToApiModel(Sap.GlBalanceResult d) =>
        new()
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

namespace SapPaymentsAdapter.Api.Services.Sap;

/// <summary>
/// Abstraction over "make an RFC call into SAP and get a BAPI-shaped result
/// back". In production this would wrap SAP's .NET Connector (NCo):
/// open an RFC destination from pooled connection settings (sourced from
/// Vault, not appsettings), build an IRfcFunction from the repository
/// metadata, set import parameters, Invoke(), read export parameters and
/// the RETURN/ET_RETURN table, then explicitly BAPI_TRANSACTION_COMMIT if
/// the BAPI doesn't auto-commit (most FI posting BAPIs don't - that's a
/// common real-world bug source when porting from RFC samples).
///
/// SapBapiSimulator implements this same contract with realistic latency,
/// error codes, and BAPIRET2 rows so the REST layer above it, and its
/// tests, don't change at all when the real NCo implementation replaces
/// this class.
/// </summary>
public interface ISapConnector
{
    Task<BapiResult<PaymentPostingResult>> PostPaymentAsync(PaymentPostingRequest request, CancellationToken ct);
    Task<BapiResult<PaymentStatusResult>> GetPaymentStatusAsync(string sapDocumentNumber, CancellationToken ct);
    Task<BapiResult<VendorDetailResult>> GetVendorDetailAsync(string vendorId, CancellationToken ct);
    Task<BapiResult<GlBalanceResult>> GetGlBalancesAsync(string glAccount, int fiscalYear, CancellationToken ct);
    Task<BapiResult<PaymentPostingResult>> PostGlEntryAsync(GlPostingRequest request, CancellationToken ct);
}

public record PaymentPostingRequest(string CompanyCode, string VendorId, decimal Amount, string Currency, string PaymentMethod, DateOnly DueDate, string? Reference);
public record PaymentPostingResult(string SapDocumentNumber, int FiscalYear);
public record PaymentStatusResult(string SapDocumentNumber, string Status, DateOnly? ClearedDate);
public record VendorDetailResult(string VendorId, string Name, string CompanyCode, string PaymentTerms, string BankAccountMasked, bool Blocked);
public record GlPeriodBalance(int Period, decimal Debit, decimal Credit, decimal Balance);
public record GlBalanceResult(string GlAccount, int FiscalYear, string Currency, List<GlPeriodBalance> PeriodBalances);
public record GlPostingRequest(string CompanyCode, string GlAccount, decimal Amount, string Currency, DateOnly PostingDate, string DocType, string? Text);

namespace SapPaymentsAdapter.Api.Services.Sap;

/// <summary>
/// In-process stand-in for real SAP RFC/BAPI calls, deliberately shaped like
/// the real thing rather than a generic REST/OData mock:
///   - Named after the actual BAPIs a bank's FI/CO team would use
///     (BAPI_ACC_DOCUMENT_POST, BAPI_VENDOR_GETDETAIL, BAPI_GL_GETBALANCES)
///   - Returns a BAPIRET2-style RETURN table that must be inspected for
///     'E'/'A' rows even when the call itself didn't throw
///   - Simulates RFC round-trip latency (SAP RFC calls are synchronous and
///     typically 100-800ms depending on system load - nothing like an
///     in-memory call)
///   - Requires an explicit "commit" step for FI posting BAPIs, mirroring
///     the real BAPI_TRANSACTION_COMMIT requirement that trips people up
///     when they assume BAPI_ACC_DOCUMENT_POST alone persists the document
///
/// Swap this for a real NCo-based ISapConnector implementation once you
/// have RFC destination access - nothing above this interface needs to
/// change.
/// </summary>
public class SapBapiSimulator : ISapConnector
{
    private readonly ILogger<SapBapiSimulator> _logger;
    private static readonly Dictionary<string, VendorDetailResult> Vendors = new()
    {
        ["100001"] = new VendorDetailResult("100001", "Acme Industrial Supplies Pty Ltd", "AU01", "NET30", "****4821", false),
        ["100002"] = new VendorDetailResult("100002", "Blocked Vendor Co", "AU01", "NET14", "****9012", true),
    };
    private static readonly Dictionary<string, PaymentStatusResult> PostedDocuments = new();

    public SapBapiSimulator(ILogger<SapBapiSimulator> logger) => _logger = logger;

    public async Task<BapiResult<PaymentPostingResult>> PostPaymentAsync(PaymentPostingRequest request, CancellationToken ct)
    {
        await SimulateRfcRoundTrip(ct);
        _logger.LogInformation("RFC CALL: BAPI_ACC_DOCUMENT_POST company={Company} vendor={Vendor}", request.CompanyCode, request.VendorId);

        if (!Vendors.TryGetValue(request.VendorId, out var vendor))
        {
            return new BapiResult<PaymentPostingResult>
            {
                Data = null,
                Returns = new() { new BapiReturnMessage("E", "FI", "008", $"Vendor {request.VendorId} does not exist for company code {request.CompanyCode}") },
            };
        }
        if (vendor.Blocked)
        {
            return new BapiResult<PaymentPostingResult>
            {
                Data = null,
                Returns = new() { new BapiReturnMessage("E", "FI", "042", $"Vendor {request.VendorId} is blocked for payment") },
            };
        }

        var docNumber = $"51{Random.Shared.Next(1000000, 9999999)}";
        var fiscalYear = request.DueDate.Year;

        // Real BAPI_ACC_DOCUMENT_POST does not commit the document itself -
        // callers must follow with BAPI_TRANSACTION_COMMIT or the posting
        // rolls back at the end of the RFC session. Simulated here as a
        // second RFC round-trip so the timing/behaviour is realistic.
        await SimulateRfcRoundTrip(ct);
        _logger.LogInformation("RFC CALL: BAPI_TRANSACTION_COMMIT doc={Doc}", docNumber);

        PostedDocuments[docNumber] = new PaymentStatusResult(docNumber, "POSTED", null);

        return new BapiResult<PaymentPostingResult>
        {
            Data = new PaymentPostingResult(docNumber, fiscalYear),
            Returns = new() { new BapiReturnMessage("S", "FI", "312", $"Document {docNumber} was posted in company code {request.CompanyCode}") },
        };
    }

    public async Task<BapiResult<PaymentStatusResult>> GetPaymentStatusAsync(string sapDocumentNumber, CancellationToken ct)
    {
        await SimulateRfcRoundTrip(ct);
        _logger.LogInformation("RFC CALL: Z_FI_GET_PAYMENT_STATUS doc={Doc}", sapDocumentNumber);

        if (!PostedDocuments.TryGetValue(sapDocumentNumber, out var status))
        {
            return new BapiResult<PaymentStatusResult>
            {
                Data = null,
                Returns = new() { new BapiReturnMessage("E", "FI", "003", $"Document {sapDocumentNumber} does not exist") },
            };
        }
        return new BapiResult<PaymentStatusResult> { Data = status, Returns = new() { new BapiReturnMessage("S", "FI", "000", "OK") } };
    }

    public async Task<BapiResult<VendorDetailResult>> GetVendorDetailAsync(string vendorId, CancellationToken ct)
    {
        await SimulateRfcRoundTrip(ct);
        _logger.LogInformation("RFC CALL: BAPI_VENDOR_GETDETAIL vendor={Vendor}", vendorId);

        if (!Vendors.TryGetValue(vendorId, out var vendor))
        {
            return new BapiResult<VendorDetailResult>
            {
                Data = null,
                Returns = new() { new BapiReturnMessage("E", "F2", "018", $"Vendor {vendorId} does not exist") },
            };
        }
        return new BapiResult<VendorDetailResult> { Data = vendor, Returns = new() { new BapiReturnMessage("S", "F2", "000", "OK") } };
    }

    public async Task<BapiResult<GlBalanceResult>> GetGlBalancesAsync(string glAccount, int fiscalYear, CancellationToken ct)
    {
        await SimulateRfcRoundTrip(ct);
        _logger.LogInformation("RFC CALL: BAPI_GL_GETBALANCES account={Account} year={Year}", glAccount, fiscalYear);

        var periods = Enumerable.Range(1, 12).Select(p =>
        {
            var debit = Math.Round((decimal)(Random.Shared.NextDouble() * 50000), 2);
            var credit = Math.Round((decimal)(Random.Shared.NextDouble() * 45000), 2);
            return new GlPeriodBalance(p, debit, credit, debit - credit);
        }).ToList();

        return new BapiResult<GlBalanceResult>
        {
            Data = new GlBalanceResult(glAccount, fiscalYear, "AUD", periods),
            Returns = new() { new BapiReturnMessage("S", "GL", "000", "OK") },
        };
    }

    public async Task<BapiResult<PaymentPostingResult>> PostGlEntryAsync(GlPostingRequest request, CancellationToken ct)
    {
        await SimulateRfcRoundTrip(ct);
        _logger.LogInformation("RFC CALL: BAPI_ACC_DOCUMENT_POST (GL) account={Account}", request.GlAccount);
        await SimulateRfcRoundTrip(ct);
        _logger.LogInformation("RFC CALL: BAPI_TRANSACTION_COMMIT");

        var docNumber = $"52{Random.Shared.Next(1000000, 9999999)}";
        return new BapiResult<PaymentPostingResult>
        {
            Data = new PaymentPostingResult(docNumber, request.PostingDate.Year),
            Returns = new() { new BapiReturnMessage("S", "FI", "312", $"Document {docNumber} was posted in company code {request.CompanyCode}") },
        };
    }

    /// SAP RFC calls are synchronous over a pooled connection - never
    /// instant. Simulated latency keeps timeout/retry/circuit-breaker
    /// practice honest instead of everything resolving in <1ms.
    private static Task SimulateRfcRoundTrip(CancellationToken ct) =>
        Task.Delay(Random.Shared.Next(100, 400), ct);
}

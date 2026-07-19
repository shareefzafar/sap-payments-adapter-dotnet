using SapPaymentsAdapter.Api.Generated;
using SapPaymentsAdapter.Api.Services.Sap;

namespace SapPaymentsAdapter.Api.Services.Vendors;

public interface IVendorsService
{
    Task<VendorDetail?> GetVendorAsync(string vendorId, CancellationToken ct);
}

public class VendorsService : IVendorsService
{
    private readonly ISapConnector _sapConnector;
    public VendorsService(ISapConnector sapConnector) => _sapConnector = sapConnector;

    public async Task<VendorDetail?> GetVendorAsync(string vendorId, CancellationToken ct)
    {
        var result = await _sapConnector.GetVendorDetailAsync(vendorId, ct);
        if (result.HasErrors || result.Data is null) return null;

        var v = result.Data;
        return new VendorDetail
        {
            VendorId = v.VendorId,
            Name = v.Name,
            CompanyCode = v.CompanyCode,
            PaymentTerms = v.PaymentTerms,
            BankAccountMasked = v.BankAccountMasked,
            Blocked = v.Blocked,
        };
    }
}

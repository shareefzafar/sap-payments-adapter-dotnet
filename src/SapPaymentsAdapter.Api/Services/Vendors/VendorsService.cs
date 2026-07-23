using SapPaymentsAdapter.Api.Generated;
using SapPaymentsAdapter.Api.Mappers;
using SapPaymentsAdapter.Api.Services.Sap;

namespace SapPaymentsAdapter.Api.Services.Vendors;

public interface IVendorsService
{
    Task<VendorDetail?> GetVendorAsync(string vendorId, CancellationToken ct);
}

public class VendorsService : IVendorsService
{
    private readonly ISapConnector _sapConnector;
    private readonly ILogger<VendorsService> _logger;

    public VendorsService(ISapConnector sapConnector, ILogger<VendorsService> logger)
    {
        _sapConnector = sapConnector;
        _logger = logger;
    }

    public async Task<VendorDetail?> GetVendorAsync(string vendorId, CancellationToken ct)
    {
        var result = await _sapConnector.GetVendorDetailAsync(vendorId, ct);
        if (result.HasErrors || result.Data is null)
        {
            _logger.LogDebug("Vendor {VendorId} not found in SAP (or lookup returned errors)", vendorId);
            return null;
        }

        return VendorsMapper.ToApiModel(result.Data);
    }
}

using SapPaymentsAdapter.Api.Generated;
using Sap = SapPaymentsAdapter.Api.Services.Sap;

namespace SapPaymentsAdapter.Api.Mappers;

/// <summary>Glue code: SAP vendor-master shape -> REST contract shape.</summary>
public static class VendorsMapper
{
    public static VendorDetail ToApiModel(Sap.VendorDetailResult v) =>
        new()
        {
            VendorId = v.VendorId,
            Name = v.Name,
            CompanyCode = v.CompanyCode,
            PaymentTerms = v.PaymentTerms,
            BankAccountMasked = v.BankAccountMasked,
            Blocked = v.Blocked,
        };
}

using Microsoft.AspNetCore.Mvc;
using SapPaymentsAdapter.Api.Generated;
using SapPaymentsAdapter.Api.Services.GlAccounts;
using SapPaymentsAdapter.Api.Services.Payments;
using SapPaymentsAdapter.Api.Services.Vendors;

namespace SapPaymentsAdapter.Api.Controllers;

public class SapAdapterController : ControllerBaseControllerBase
{
    private readonly IPaymentsService _paymentsService;
    private readonly IVendorsService _vendorsService;
    private readonly IGlAccountsService _glAccountsService;

    public SapAdapterController(IPaymentsService paymentsService, IVendorsService vendorsService, IGlAccountsService glAccountsService)
    {
        _paymentsService = paymentsService;
        _vendorsService = vendorsService;
        _glAccountsService = glAccountsService;
    }

    public override async System.Threading.Tasks.Task<PaymentInitiationResponse> InitiatePayment(PaymentInitiationRequest body, System.Threading.CancellationToken cancellationToken)
    {
        var result = await _paymentsService.InitiatePaymentAsync(body, cancellationToken);
        // NSwag's generated abstract method returns the raw DTO, not
        // ActionResult<T>, so MVC defaults to 200 unless we explicitly set
        // the response status code ourselves before returning the body.
        Response.StatusCode = result.Status == PaymentInitiationResponseStatus.REJECTED ? StatusCodes.Status400BadRequest : StatusCodes.Status202Accepted;
        return result;
    }

    public override async System.Threading.Tasks.Task<PaymentStatusResponse> GetPaymentStatus(string paymentId, System.Threading.CancellationToken cancellationToken)
    {
        var result = await _paymentsService.GetPaymentStatusAsync(paymentId, cancellationToken);
        if (result is null) throw new KeyNotFoundException($"Payment {paymentId} not found");
        return result;
    }

    public override async System.Threading.Tasks.Task<VendorDetail> GetVendor(string vendorId, System.Threading.CancellationToken cancellationToken)
    {
        var result = await _vendorsService.GetVendorAsync(vendorId, cancellationToken);
        if (result is null) throw new KeyNotFoundException($"Vendor {vendorId} not found");
        return result;
    }

    public override async System.Threading.Tasks.Task<GlAccountBalance> GetGlAccountBalance(string glAccount, int fiscalYear, System.Threading.CancellationToken cancellationToken)
    {
        var result = await _glAccountsService.GetBalanceAsync(glAccount, fiscalYear, cancellationToken);
        if (result is null) throw new KeyNotFoundException($"GL account {glAccount} not found");
        return result;
    }

    public override async System.Threading.Tasks.Task<PaymentInitiationResponse> PostGlEntry(string glAccount, GlPostingRequest body, System.Threading.CancellationToken cancellationToken)
    {
        var result = await _glAccountsService.PostEntryAsync(glAccount, body, cancellationToken);
        Response.StatusCode = result.Status == PaymentInitiationResponseStatus.REJECTED ? StatusCodes.Status400BadRequest : StatusCodes.Status202Accepted;
        return result;
    }
}
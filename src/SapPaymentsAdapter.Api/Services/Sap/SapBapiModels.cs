namespace SapPaymentsAdapter.Api.Services.Sap;

/// <summary>
/// Mirrors SAP's standard BAPIRET2 structure returned in the RETURN table
/// of virtually every BAPI. Type S/I = success/info, W = warning,
/// E/A = error/abort. Real SAP integration code always inspects this table
/// before trusting the "happy path" export parameters — a BAPI call can
/// return HTTP-200-equivalent (no exception) and still have failed
/// business-wise, signalled only via an 'E' row here.
/// </summary>
public record BapiReturnMessage(string Type, string Id, string Number, string Message)
{
    public bool IsError => Type is "E" or "A";
}

public class BapiResult<T>
{
    public required T? Data { get; init; }
    public required List<BapiReturnMessage> Returns { get; init; } = new();
    public bool HasErrors => Returns.Any(r => r.IsError);
}

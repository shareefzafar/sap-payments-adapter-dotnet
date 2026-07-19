using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace SapPaymentsAdapter.Api.Services.Vault;

public interface IVaultService
{
    Task<Dictionary<string, string>> GetSecretAsync(string path);
    Task<string?> GetSecretValueAsync(string path, string key);
}

/// <summary>
/// Reads SAP RFC connection credentials (ashost, sysnr, client, RFC user,
/// password) and other private keys from Vault's KV v2 secrets engine.
///
/// Auth here uses a token for local dev simplicity (VAULT_TOKEN env var /
/// docker-compose dev server). For the real environment, prefer AppRole
/// (role_id baked into the deploy, secret_id injected by the CI/CD
/// pipeline or a Vault Agent sidecar) over a long-lived token, and prefer
/// short-lived dynamic secrets over static KV entries wherever SAP's own
/// auth model allows it - static RFC user/password in KV is the fallback,
/// not the target state.
/// </summary>
public class VaultService : IVaultService
{
    private readonly IVaultClient _vaultClient;
    private readonly ILogger<VaultService> _logger;
    private const string MountPoint = "secret";

    public VaultService(IConfiguration configuration, ILogger<VaultService> logger)
    {
        _logger = logger;
        var vaultAddr = configuration["Vault:Address"] ?? "http://localhost:8200";
        var vaultToken = configuration["Vault:Token"] ?? Environment.GetEnvironmentVariable("VAULT_TOKEN")
            ?? throw new InvalidOperationException("VAULT_TOKEN not set (env var or Vault:Token config)");

        var authMethod = new TokenAuthMethodInfo(vaultToken);
        var settings = new VaultClientSettings(vaultAddr, authMethod);
        _vaultClient = new VaultClient(settings);
    }

    public async Task<Dictionary<string, string>> GetSecretAsync(string path)
    {
        try
        {
            Secret<SecretData> secret = await _vaultClient.V1.Secrets.KeyValue.V2
                .ReadSecretAsync(path: path, mountPoint: MountPoint);

            _logger.LogInformation("Secret fetched from Vault: {Path}", path);

            return secret.Data.Data.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch secret from Vault: {Path}", path);
            throw new InvalidOperationException($"Could not fetch secret: {path}", ex);
        }
    }

    public async Task<string?> GetSecretValueAsync(string path, string key)
    {
        var secrets = await GetSecretAsync(path);
        if (!secrets.TryGetValue(key, out var value))
        {
            _logger.LogWarning("Key not found in Vault secret: {Key}", key);
            return null;
        }
        return value;
    }
}

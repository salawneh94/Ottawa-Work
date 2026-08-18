using System.Net.Http;
using System.Net.Http.Json;

namespace BIMFlow.Shared;

public record LicenseCheckResult(bool Valid, string? Reason);

file record ValidateRequest(string licenseKey, string machineId, string? machineName, string pluginSlug);

file record ValidateResponse(bool valid, string? reason);

/// <summary>
/// Talks to BIMFlow's /api/license/validate endpoint to confirm a plugin is
/// licensed on this machine, with a short offline grace period backed by
/// <see cref="LicenseStore"/> so a flaky connection doesn't block work.
/// </summary>
public static class LicenseClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly TimeSpan OfflineGrace = TimeSpan.FromDays(7);

    public static LicenseCheckResult EnsureLicensed(string pluginSlug)
    {
        var key = LicenseStore.ReadKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            key = LicenseActivationDialog.PromptForKey();
            if (string.IsNullOrWhiteSpace(key))
                return new LicenseCheckResult(false, "No license key entered.");
            LicenseStore.SaveKey(key);
        }

        var cached = LicenseStore.ReadCache(pluginSlug, key);

        try
        {
            var result = ValidateOnlineAsync(key, pluginSlug).GetAwaiter().GetResult();
            LicenseStore.WriteCache(pluginSlug, key, result.Valid);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (cached is { Valid: true } c && c.CheckedAtUtc > DateTime.UtcNow - OfflineGrace)
                return new LicenseCheckResult(true, null);

            return new LicenseCheckResult(
                false,
                "Couldn't reach BIMFlow to verify your license, and no recent cached activation " +
                "was found on this machine. Check your internet connection and try again.");
        }
    }

    private static async Task<LicenseCheckResult> ValidateOnlineAsync(string key, string pluginSlug)
    {
        var payload = new ValidateRequest(key, MachineIdentity.GetMachineId(), Environment.MachineName, pluginSlug);
        using var response = await Http.PostAsJsonAsync($"{BimFlowConfig.ApiBaseUrl}/api/license/validate", payload);
        var body = await response.Content.ReadFromJsonAsync<ValidateResponse>();

        if (body is null)
            return new LicenseCheckResult(false, "Unexpected response from BIMFlow.");

        return new LicenseCheckResult(body.valid, body.reason);
    }
}

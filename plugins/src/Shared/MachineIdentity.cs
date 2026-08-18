using System.Security.Cryptography;
using System.Text;

namespace BIMFlow.Shared;

/// <summary>
/// A best-effort, dependency-free machine fingerprint used for license seat counting.
/// Not tamper-proof — it only needs to be stable across runs on the same workstation.
/// </summary>
public static class MachineIdentity
{
    public static string GetMachineId()
    {
        var raw = string.Join(
            '|',
            Environment.MachineName,
            Environment.UserDomainName,
            Environment.ProcessorCount.ToString(),
            Environment.OSVersion.VersionString);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..24];
    }
}

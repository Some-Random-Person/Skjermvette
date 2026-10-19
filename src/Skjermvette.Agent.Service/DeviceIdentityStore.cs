using System.Security.Cryptography;
using System.Text.Json;

namespace Skjermvette.Agent.Service;

/// <summary>
/// Keeps the <see cref="DeviceIdentity"/> in <c>device.dat</c>, encrypted with DPAPI for this computer. Machine scope,
/// because pairing runs as an administrator and the service as SYSTEM; the folder's ACL keeps other users out.
/// </summary>
public sealed class DeviceIdentityStore(string folder)
{
    private static readonly byte[] Entropy = "Skjermvette.Agent.DeviceIdentity"u8.ToArray();

    private string FilePath => Path.Combine(folder, "device.dat");

    public bool Exists => File.Exists(FilePath);

    public DeviceIdentity? Load()
    {
        if (!Exists)
            return null;
        var json = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.LocalMachine);
        return JsonSerializer.Deserialize<DeviceIdentity>(json, ServerClient.Json);
    }

    public void Save(DeviceIdentity identity)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(identity, ServerClient.Json);
        File.WriteAllBytes(FilePath, ProtectedData.Protect(json, Entropy, DataProtectionScope.LocalMachine));
    }
}

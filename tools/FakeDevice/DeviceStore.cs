using System.Text.Json;

namespace FakeDevice;

/// <summary>Keeps paired fake devices in %LOCALAPPDATA%\ScreenTime\FakeDevice, one JSON file per device.</summary>
public static class DeviceStore
{
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenTime", "FakeDevice");

    public static async Task SaveAsync(SavedDevice device)
    {
        Directory.CreateDirectory(Folder);
        await File.WriteAllTextAsync(PathFor(device.Name), JsonSerializer.Serialize(device, DeviceClient.Json));
    }

    public static async Task<SavedDevice> LoadAsync(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path))
            throw new UsageException($"No fake device called \"{name}\". Pair one first, or run 'list'.");
        return JsonSerializer.Deserialize<SavedDevice>(await File.ReadAllTextAsync(path), DeviceClient.Json)!;
    }

    public static IEnumerable<SavedDevice> All() =>
        Directory.Exists(Folder)
            ? Directory.EnumerateFiles(Folder, "*.json")
                .Select(path => JsonSerializer.Deserialize<SavedDevice>(File.ReadAllText(path), DeviceClient.Json)!)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            : [];

    public static bool Forget(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path))
            return false;
        File.Delete(path);
        return true;
    }

    private static string PathFor(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new UsageException($"\"{name}\" can't be used as a device name.");
        return Path.Combine(Folder, $"{name.ToLowerInvariant()}.json");
    }
}

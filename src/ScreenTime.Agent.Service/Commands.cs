using ScreenTime.Core.DeviceApi;

namespace ScreenTime.Agent.Service;

/// <summary>The command-line commands that set the agent up, run by an administrator instead of the service.</summary>
public static class Commands
{
    public const string PairUsage = "ScreenTime.Agent.Service pair <code> --server <url> [--name <device name>]";

    private const string Help = $"""
        ScreenTime agent. Without a command it runs the service (also in a console, for testing).

        Commands (as an administrator):
          {PairUsage}
              Pair this computer with a 6-digit code from the dashboard (Devices tab). The name defaults to the computer's name.
          ScreenTime.Agent.Service forget
              Delete this computer's token, unsent usage and cached budget. Unpair it in the dashboard too.
        """;

    public static bool IsCommand(string[] args) => args is ["pair", ..] or ["forget"] or ["help" or "--help" or "-h" or "/?"];

    public static async Task<int> RunAsync(string[] args, IConfiguration configuration)
    {
        // Checked first, so a pairing code isn't used up by a token that then can't be saved.
        if (args is not ["help" or "--help" or "-h" or "/?"] && AgentFolder.IsRestricted(configuration) && !Environment.IsPrivilegedProcess)
        {
            Console.Error.WriteLine("Run this as an administrator: only administrators can open the agent's data.");
            return 1;
        }

        try
        {
            return args switch
            {
                ["pair", var code, .. var options] => await PairAsync(code, options, configuration),
                ["forget"] => Forget(configuration),
                _ => ShowHelp(),
            };
        }
        catch (Exception e) when (e is ServerRejectedException or HttpRequestException)
        {
            Console.Error.WriteLine(e is HttpRequestException ? $"Can't reach the server ({e.Message})." : e.Message);
            return 1;
        }
    }

    private static async Task<int> PairAsync(string code, string[] options, IConfiguration configuration)
    {
        if (Option(options, "--server") is not { } server || !Uri.TryCreate(server, UriKind.Absolute, out var serverUri))
        {
            Console.Error.WriteLine($"Give the server's address with --server, e.g. --server https://screentime.example.com\nUsage: {PairUsage}");
            return 2;
        }

        var store = new DeviceIdentityStore(AgentFolder.Create(configuration));
        // Pairing again would leave the old device paired with nobody holding its token.
        if (store.Exists)
        {
            Console.Error.WriteLine("This computer is already paired. Run 'forget' first (and unpair it in the dashboard).");
            return 1;
        }

        var name = Option(options, "--name") ?? Environment.MachineName;
        var paired = await ServerClient.PairAsync(serverUri, new PairRequest(code, name, DevicePlatformEnum.Windows));
        store.Save(new DeviceIdentity(serverUri, paired.DeviceId, paired.ChildId, paired.Token));
        Console.WriteLine($"Paired \"{name}\". It now shows on the child's Devices tab. Start the service to begin counting.");
        return 0;
    }

    private static int Forget(IConfiguration configuration)
    {
        var folder = AgentFolder.Path(configuration);
        foreach (var file in new[] { "device.dat", "outbox.db", "budget.json" })
            File.Delete(Path.Combine(folder, file));
        Console.WriteLine("Forgot this computer's pairing. Unpair it in the dashboard too, if it's still listed.");
        return 0;
    }

    private static int ShowHelp()
    {
        Console.WriteLine(Help);
        return 2;
    }

    private static string? Option(string[] options, string name)
    {
        var i = Array.FindIndex(options, o => o.Equals(name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < options.Length ? options[i + 1] : null;
    }
}

using System.Diagnostics;
using Skjermvette.Core;

namespace Skjermvette.Agent.Helper;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (BrowserHost.StartedByBrowser(args))
            return BrowserHost.RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput(), BrowserPipe.Name(Process.GetCurrentProcess().SessionId)).GetAwaiter().GetResult();

        // One Helper per session when started by hand. The Service keeps one per session itself, and any program could
        // take the mutex first to keep the Helper from starting.
        var first = true;
        using var single = args.Contains(HelperPipe.ServiceArgument) ? null : new Mutex(true, @"Local\Skjermvette.Agent.Helper", out first);
        if (!first)
            return 0;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Application.Exit();
        };
        using var context = new HelperContext();
        Application.Run(context);
        return 0;
    }
}

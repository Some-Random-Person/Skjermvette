using ScreenTime.Core;

namespace ScreenTime.Agent.Helper;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // One Helper per session when started by hand. The Service keeps one per session itself, and any program could
        // take the mutex first to keep the Helper from starting.
        var first = true;
        using var single = args.Contains(HelperPipe.ServiceArgument) ? null : new Mutex(true, @"Local\ScreenTime.Agent.Helper", out first);
        if (!first)
            return;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Application.Exit();
        };
        using var context = new HelperContext();
        Application.Run(context);
    }
}

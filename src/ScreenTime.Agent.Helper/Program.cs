namespace ScreenTime.Agent.Helper;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // One Helper per session.
        using var single = new Mutex(true, @"Local\ScreenTime.Agent.Helper", out var first);
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

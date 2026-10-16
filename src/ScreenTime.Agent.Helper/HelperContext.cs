using Microsoft.Win32;
using ScreenTime.Core.Models;

namespace ScreenTime.Agent.Helper;

/// <summary>
/// Runs in the child's session with no window: samples what is in use (the program in front and any making sound) every
/// second, and straight away when another window comes to the front, and sends it to the Service.
/// </summary>
public sealed class HelperContext : ApplicationContext
{
    private readonly PipeSender _sender = new();
    private readonly AudioSessions _audio = new();
    private readonly RecentlyHeard _heard = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    // Kept in a field so the delegate isn't collected while Windows still calls it.
    private readonly NativeMethods.WinEventProc _onForeground;
    private readonly IntPtr _hook;
    private bool _sessionActive = true;
    private string _last = "";

    public HelperContext()
    {
        _onForeground = (_, _, _, _, _, _, _) => Sample();
        _hook = NativeMethods.SetWinEventHook(NativeMethods.EventSystemForeground, NativeMethods.EventSystemForeground, IntPtr.Zero,
            _onForeground, 0, 0, NativeMethods.WinEventOutOfContext);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _timer.Tick += (_, _) => Sample();
        _timer.Start();
        Sample();
    }

    private void Sample()
    {
        var sinceLastInput = Sampler.Elapsed(NativeMethods.LastInputTick(), (uint)Environment.TickCount);
        var playing = _audio.Playing().Select(NativeMethods.ProcessPath).OfType<string>();
        var audio = _heard.Update(TimeSpan.FromMilliseconds(Environment.TickCount64), playing);
        var inUse = Sampler.InUse(NativeMethods.ForegroundProcessPath(), audio, sinceLastInput, _sessionActive);
        _sender.Send(inUse);

        var described = inUse.Length == 0 ? "nothing" : string.Join(", ", inUse.Select(k => k.Identifier));
        if (described != _last)
            Console.WriteLine($"{DateTime.Now:T} In use: {described}");
        _last = described;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        _sessionActive = e.Reason switch
        {
            SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect => false,
            SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect => true,
            _ => _sessionActive,
        };
        Sample();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _timer.Dispose();
            NativeMethods.UnhookWinEvent(_hook);
            _sender.Dispose();
            _audio.Dispose();
        }
        base.Dispose(disposing);
    }
}

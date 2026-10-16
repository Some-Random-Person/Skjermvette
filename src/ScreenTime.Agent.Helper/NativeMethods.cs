using System.Runtime.InteropServices;
using System.Text;

namespace ScreenTime.Agent.Helper;

/// <summary>The Windows calls the Helper needs to see what is in use and whether anyone is using the computer.</summary>
internal static class NativeMethods
{
    public const uint EventSystemForeground = 0x0003;
    public const uint WinEventOutOfContext = 0x0000;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint thread, uint time);

    /// <summary>The full path of the program whose window is in front, or null if there's none or it can't be read.</summary>
    public static string? ForegroundProcessPath()
    {
        var window = GetForegroundWindow();
        return window == IntPtr.Zero || GetWindowThreadProcessId(window, out var processId) == 0 ? null : ProcessPath(processId);
    }

    /// <summary>The full path of a process's program, or null if it can't be read.</summary>
    public static string? ProcessPath(uint processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
            return null;
        try
        {
            var path = new StringBuilder(1024);
            var length = path.Capacity;
            return QueryFullProcessImageName(process, 0, path, ref length) ? path.ToString() : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>Milliseconds since boot (wrapping like <c>GetTickCount</c>) at the last keyboard or mouse input in this session.</summary>
    public static uint LastInputTick()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info) ? info.Time : (uint)Environment.TickCount;
    }

    /// <summary>The Remote Desktop Services session a process runs in, or <see cref="uint.MaxValue"/> if it can't be read.</summary>
    public static uint SessionOf(uint processId) => ProcessIdToSessionId(processId, out var session) ? session : uint.MaxValue;

    public static uint CurrentSessionId() => SessionOf((uint)Environment.ProcessId);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int length);

    [DllImport("kernel32.dll")]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }
}

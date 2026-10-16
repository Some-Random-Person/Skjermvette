using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ScreenTime.Agent.Service;

/// <summary>Starts the Helper as the user signed in to a session. Only works as SYSTEM (<c>WTSQueryUserToken</c> needs it).</summary>
public static class HelperLauncher
{
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateNoWindow = 0x08000000;

    /// <summary>Starts <paramref name="path"/> on the user's desktop, or returns null when it stopped again straight away.</summary>
    /// <exception cref="Win32Exception">There's no user in the session, or the program can't be started.</exception>
    public static Process? Start(string path, int session)
    {
        if (!WTSQueryUserToken((uint)session, out var token))
            throw new Win32Exception();
        var environment = IntPtr.Zero;
        try
        {
            if (!CreateEnvironmentBlock(out environment, token, false))
                throw new Win32Exception();

            var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>(), lpDesktop = @"winsta0\default" };
            if (!CreateProcessAsUser(token, path, new StringBuilder($"\"{path}\""), IntPtr.Zero, IntPtr.Zero, false,
                    CreateUnicodeEnvironment | CreateNoWindow, environment, Path.GetDirectoryName(path), ref startup, out var info))
                throw new Win32Exception();
            try
            {
                // Taken while the handle is still open, so the id can't belong to another process yet.
                return Process.GetProcessById(info.dwProcessId);
            }
            catch (ArgumentException)
            {
                return null;
            }
            finally
            {
                CloseHandle(info.hThread);
                CloseHandle(info.hProcess);
            }
        }
        finally
        {
            if (environment != IntPtr.Zero)
                DestroyEnvironmentBlock(environment);
            CloseHandle(token);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint session, out IntPtr token);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

    [DllImport("userenv.dll")]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessAsUser(IntPtr token, string application, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string? directory, ref StartupInfo startup,
        out ProcessInformation info);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using ScreenTime.Core;

namespace ScreenTime.Agent.Service;

/// <summary>
/// Starts the Helper as the user signed in to a session, at High integrity, so the user's own programs (Medium) can't
/// close, pause or change it. Only works as SYSTEM (<c>WTSQueryUserToken</c> needs it).
/// </summary>
public static class HelperLauncher
{
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateNoWindow = 0x08000000;
    private const uint MaximumAllowed = 0x02000000;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const int TokenIntegrityLevel = 25;
    private const uint SeGroupIntegrity = 0x00000020;

    private static readonly SecurityIdentifier HighIntegrity = new("S-1-16-12288");

    /// <summary>
    /// Settings that would let the user load their own code into the Helper (e.g. <c>DOTNET_STARTUP_HOOKS</c>, or
    /// <c>DOTNET_ROOT</c> pointing at a runtime of their own), since it would run at a higher integrity than they do.
    /// </summary>
    private static readonly string[] RuntimePrefixes = ["DOTNET_", "COREHOST_", "CORECLR_", "COMPlus_", "COR_"];

    /// <summary>Starts <paramref name="path"/> on the user's desktop, or returns null when it stopped again straight away.</summary>
    /// <exception cref="Win32Exception">There's no user in the session, or the program can't be started.</exception>
    public static Process? Start(string path, int session)
    {
        if (!WTSQueryUserToken((uint)session, out var userToken))
            throw new Win32Exception();
        var token = IntPtr.Zero;
        var environment = IntPtr.Zero;
        try
        {
            if (!DuplicateTokenEx(userToken, MaximumAllowed, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out token))
                throw new Win32Exception();
            RaiseToHigh(token);
            environment = CleanEnvironment(token);

            var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>(), lpDesktop = @"winsta0\default" };
            if (!CreateProcessAsUser(token, path, new StringBuilder($"\"{path}\" {HelperPipe.ServiceArgument}"), IntPtr.Zero, IntPtr.Zero, false,
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
            Marshal.FreeHGlobal(environment);
            if (token != IntPtr.Zero)
                CloseHandle(token);
            CloseHandle(userToken);
        }
    }

    /// <summary>The user's environment variables, without the ones that change how .NET loads (<see cref="RuntimePrefixes"/>).</summary>
    public static IEnumerable<string> WithoutRuntimeSettings(IEnumerable<string> variables) =>
        variables.Where(v => !RuntimePrefixes.Any(prefix => v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    private static void RaiseToHigh(IntPtr token)
    {
        var sid = new byte[HighIntegrity.BinaryLength];
        HighIntegrity.GetBinaryForm(sid, 0);
        var pinned = GCHandle.Alloc(sid, GCHandleType.Pinned);
        try
        {
            var label = new TokenMandatoryLabel { Sid = pinned.AddrOfPinnedObject(), Attributes = SeGroupIntegrity };
            if (!SetTokenInformation(token, TokenIntegrityLevel, ref label, Marshal.SizeOf<TokenMandatoryLabel>() + sid.Length))
                throw new Win32Exception();
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <summary>The user's environment block (<see cref="WithoutRuntimeSettings"/>), to be freed with <see cref="Marshal.FreeHGlobal"/>.</summary>
    private static IntPtr CleanEnvironment(IntPtr token)
    {
        if (!CreateEnvironmentBlock(out var block, token, false))
            throw new Win32Exception();
        var variables = new List<string>();
        try
        {
            // Each variable ends in a 0, and the block ends with an empty one.
            for (var p = block; Marshal.PtrToStringUni(p) is { Length: > 0 } variable; p += (variable.Length + 1) * sizeof(char))
                variables.Add(variable);
        }
        finally
        {
            DestroyEnvironmentBlock(block);
        }
        // StringToHGlobalUni adds the final 0.
        return Marshal.StringToHGlobalUni(string.Concat(WithoutRuntimeSettings(variables).Select(v => v + '\0')));
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

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenMandatoryLabel
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint session, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(IntPtr token, uint access, IntPtr attributes, int impersonationLevel, int tokenType,
        out IntPtr newToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool SetTokenInformation(IntPtr token, int infoClass, ref TokenMandatoryLabel info, int length);

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

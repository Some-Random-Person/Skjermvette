using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ScreenTime.Agent.Service;

/// <summary>Who is signed in at the computer itself (the active console session), not over Remote Desktop.</summary>
public static class ConsoleSession
{
    private const uint NoSession = 0xFFFFFFFF;

    /// <summary>The signed-in user, or null when nobody is.</summary>
    public static SecurityIdentifier? User()
    {
        var session = WTSGetActiveConsoleSessionId();
        if (session == NoSession || Query(session, WtsInfoClass.UserName) is not { Length: > 0 } user)
            return null;

        try
        {
            return (SecurityIdentifier)new NTAccount(Query(session, WtsInfoClass.DomainName) ?? "", user).Translate(typeof(SecurityIdentifier));
        }
        catch (IdentityNotMappedException)
        {
            return null;
        }
    }

    private static string? Query(uint session, WtsInfoClass info)
    {
        if (!WTSQuerySessionInformation(IntPtr.Zero, session, info, out var buffer, out _))
            return null;
        try
        {
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private enum WtsInfoClass
    {
        UserName = 5,
        DomainName = 7,
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool WTSQuerySessionInformation(IntPtr server, uint session, WtsInfoClass info, out IntPtr buffer, out uint bytes);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);
}

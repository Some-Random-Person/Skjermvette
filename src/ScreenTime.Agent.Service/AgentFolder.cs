using System.Security.AccessControl;
using System.Security.Principal;

namespace ScreenTime.Agent.Service;

/// <summary>
/// Where the agent keeps its device token, outbox and cached budget: <c>%ProgramData%\ScreenTime\Agent</c>,
/// or <c>Agent:DataFolder</c> from configuration.
/// </summary>
public static class AgentFolder
{
    public static string Path(IConfiguration configuration) =>
        configuration["Agent:DataFolder"] is { Length: > 0 } folder
            ? Environment.ExpandEnvironmentVariables(folder)
            : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ScreenTime", "Agent");

    /// <summary>Only SYSTEM and administrators can open the folder (always, except in development).</summary>
    public static bool IsRestricted(IConfiguration configuration) => configuration.GetValue("Agent:RestrictFolder", true);

    /// <summary>
    /// Creates the folder. Unless <c>Agent:RestrictFolder</c> is false (development), only SYSTEM and administrators
    /// can open it, so the child can't read the token or edit the outbox.
    /// </summary>
    public static string Create(IConfiguration configuration)
    {
        var path = Path(configuration);
        var folder = Directory.CreateDirectory(path);
        if (IsRestricted(configuration))
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
            {
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            }
            folder.SetAccessControl(security);
        }
        return path;
    }
}

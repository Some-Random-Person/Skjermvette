using Skjermvette.Agent.Service;

namespace Skjermvette.Agent.Tests;

public class HelperLauncherTests
{
    [Fact]
    public void SettingsThatChangeHowDotnetLoads_AreLeftOut()
    {
        string[] variables =
        [
            "PATH=C:\\Windows", "dotnet_root=C:\\Users\\kid\\runtime", "DOTNET_STARTUP_HOOKS=C:\\Users\\kid\\hook.dll",
            "COR_PROFILER={00000000-0000-0000-0000-000000000000}", "COMPlus_EnableDiagnostics=1", "COREHOST_TRACE=1",
            "USERNAME=kid", "=C:=C:\\Users\\kid",
        ];

        Assert.Equal(["PATH=C:\\Windows", "USERNAME=kid", "=C:=C:\\Users\\kid"], HelperLauncher.WithoutRuntimeSettings(variables));
    }
}

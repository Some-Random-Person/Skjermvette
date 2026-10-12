using System.ComponentModel;

namespace ScreenTime.Core.Models;

public sealed record AppMatcher(MatcherKindEnum Kind, string Value);

public enum MatcherKindEnum
{
    [Description("Windows/macOS/Linux process name")]
    Process = 1,

    [Description("Android package name")]
    Package = 2,

    [Description("Apple bundle identifier")]
    Bundle = 3,

    [Description("Web domain, including subdomains")]
    Domain = 4,

    [Description("Opaque iOS Screen Time application token")]
    IosToken = 5,
}

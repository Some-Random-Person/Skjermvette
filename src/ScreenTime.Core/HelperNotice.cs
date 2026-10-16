using System.ComponentModel;

namespace ScreenTime.Core;

/// <summary>
/// What the Windows agent's Service tells the Helper to show the child. <see cref="App"/> is the app whose own limit is
/// running out, or null when it's the daily total. <see cref="Minutes"/> is the time left, rounded up (0 once closed).
/// </summary>
public sealed record HelperNotice(HelperNoticeEnum Kind, string? App, int Minutes);

public enum HelperNoticeEnum
{
    [Description("Time is running out")]
    Warning = 1,

    [Description("Time is up: the program was closed")]
    Closed = 2,
}

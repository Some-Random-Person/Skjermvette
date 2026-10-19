using Skjermvette.Core;

namespace Skjermvette.Agent.Helper;

/// <summary>What the child reads for each <see cref="HelperNotice"/>.</summary>
public static class NoticeText
{
    public static string For(HelperNotice notice)
    {
        var minutes = notice.Minutes == 1 ? "1 minute" : $"{notice.Minutes} minutes";
        return (notice.Kind, notice.App) switch
        {
            (HelperNoticeEnum.Warning, null) => $"{minutes} of screen time left today.",
            (HelperNoticeEnum.Warning, { } app) => $"{minutes} left for {app} today.",
            (_, null) => "Your screen time for today is used up, so the program was closed.\nApps that are always allowed still work.",
            (_, { } app) => $"Time's up for {app} today, so it was closed.",
        };
    }
}

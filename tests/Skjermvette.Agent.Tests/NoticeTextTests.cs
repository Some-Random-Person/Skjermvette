using Skjermvette.Agent.Helper;
using Skjermvette.Core;

namespace Skjermvette.Agent.Tests;

public class NoticeTextTests
{
    [Theory]
    [InlineData(HelperNoticeEnum.Warning, "YouTube", 5, "5 minutes left for YouTube today.")]
    [InlineData(HelperNoticeEnum.Warning, null, 1, "1 minute of screen time left today.")]
    [InlineData(HelperNoticeEnum.Closed, "YouTube", 0, "Time's up for YouTube today, so it was closed.")]
    public void Texts(HelperNoticeEnum kind, string? app, int minutes, string expected)
    {
        Assert.Equal(expected, NoticeText.For(new HelperNotice(kind, app, minutes)));
    }
}

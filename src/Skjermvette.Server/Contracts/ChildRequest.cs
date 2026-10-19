namespace Skjermvette.Server.Contracts;

public sealed record ChildRequest(
    string Name,
    string TimeZoneId,
    int ResetHour,
    int DailyTotalMinutes,
    int OfflineAllowanceMinutes,
    IReadOnlyList<AppRuleDto> Rules);

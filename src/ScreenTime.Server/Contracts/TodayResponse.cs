namespace ScreenTime.Server.Contracts;

public sealed record TodayResponse(
    Guid ChildId,
    DateTimeOffset DayStart,
    DateTimeOffset DayEnd,
    double TotalLimitMinutes,
    double ExtraMinutes,
    double TotalUsedMinutes,
    double TotalRemainingMinutes,
    IReadOnlyList<TodayAppResponse> Apps,
    IReadOnlyList<DeviceResponse> Devices);

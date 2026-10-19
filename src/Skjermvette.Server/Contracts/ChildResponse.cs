using Skjermvette.Server.Data.Entities;

namespace Skjermvette.Server.Contracts;

public sealed record ChildResponse(
    Guid Id,
    string Name,
    string TimeZoneId,
    int ResetHour,
    int DailyTotalMinutes,
    int OfflineAllowanceMinutes,
    IReadOnlyList<AppRuleDto> Rules)
{
    public static ChildResponse From(Child child) => new(
        child.Id,
        child.Name,
        child.TimeZoneId,
        child.ResetHour,
        (int)child.DailyTotal.TotalMinutes,
        (int)child.OfflineAllowance.TotalMinutes,
        child.AppRules
            .OrderBy(r => r.AppDefinitionId)
            .Select(r => new AppRuleDto(r.AppDefinitionId, r.Kind, r.Limit is { } l ? (int)l.TotalMinutes : null))
            .ToList());
}

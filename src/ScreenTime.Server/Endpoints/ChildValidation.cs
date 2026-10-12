using ScreenTime.Core.Models;
using ScreenTime.Server.Contracts;
using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Endpoints;

public static class ChildValidation
{
    private const int MinutesPerDay = 24 * 60;

    /// <summary>Returns validation errors keyed by field, or an empty dictionary when the request is valid.</summary>
    public static Dictionary<string, string[]> Validate(ChildRequest request, IReadOnlySet<Guid> visibleAppIds)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string field, string message) => errors[field] = [.. errors.GetValueOrDefault(field, []), message];

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
            Add(nameof(request.Name), "Name is required and must be at most 100 characters.");
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId ?? "", out _))
            Add(nameof(request.TimeZoneId), "Unknown time zone. Use an IANA name such as \"Europe/Oslo\".");
        if (request.ResetHour is < 0 or > 23)
            Add(nameof(request.ResetHour), "Reset hour must be between 0 and 23.");
        if (request.DailyTotalMinutes is < 0 or > MinutesPerDay)
            Add(nameof(request.DailyTotalMinutes), $"Daily total must be between 0 and {MinutesPerDay} minutes.");

        var maxAllowance = (int)Policy.MaxOfflineAllowance.TotalMinutes;
        if (request.OfflineAllowanceMinutes < 0 || request.OfflineAllowanceMinutes > maxAllowance)
            Add(nameof(request.OfflineAllowanceMinutes), $"Offline allowance must be between 0 and {maxAllowance} minutes.");

        foreach (var rule in request.Rules ?? [])
        {
            if (!visibleAppIds.Contains(rule.AppId))
                Add(nameof(request.Rules), $"Unknown app {rule.AppId}.");
            if (!Enum.IsDefined(rule.Kind))
                Add(nameof(request.Rules), $"Unknown rule kind for app {rule.AppId}.");
            if (rule.Kind == AppRuleKindEnum.Limited && rule.LimitMinutes is not (>= 0 and <= MinutesPerDay))
                Add(nameof(request.Rules), $"Limited app {rule.AppId} needs a limit between 0 and {MinutesPerDay} minutes.");
        }

        if ((request.Rules ?? []).GroupBy(r => r.AppId).Any(g => g.Count() > 1))
            Add(nameof(request.Rules), "Each app can have only one rule: limited or exempt, not both.");

        return errors;
    }
}

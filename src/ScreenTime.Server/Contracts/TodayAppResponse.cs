using ScreenTime.Server.Data.Entities;

namespace ScreenTime.Server.Contracts;

/// <summary>Today's use of one app. <see cref="Kind"/> is null for apps without a rule.</summary>
public sealed record TodayAppResponse(Guid? AppId, string Name, AppRuleKindEnum? Kind, double UsedMinutes, double? LimitMinutes, double? RemainingMinutes);

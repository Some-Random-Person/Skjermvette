using Skjermvette.Server.Data.Entities;

namespace Skjermvette.Server.Contracts;

/// <summary>A rule for one app. <see cref="LimitMinutes"/> is required for limited apps and ignored for exempt ones.</summary>
public sealed record AppRuleDto(Guid AppId, AppRuleKindEnum Kind, int? LimitMinutes);

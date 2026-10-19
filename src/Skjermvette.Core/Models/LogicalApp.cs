namespace Skjermvette.Core.Models;

/// <summary>A platform-neutral app such as "YouTube", identified on each platform by its matchers.</summary>
public sealed record LogicalApp(Guid Id, string Name, IReadOnlyList<AppMatcher> Matchers);

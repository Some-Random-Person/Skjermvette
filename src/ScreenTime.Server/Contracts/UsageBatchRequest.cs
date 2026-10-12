namespace ScreenTime.Server.Contracts;

public sealed record UsageBatchRequest(IReadOnlyList<UsageEventDto> Events);

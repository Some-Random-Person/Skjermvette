namespace ScreenTime.Core.DeviceApi;

public sealed record UsageBatchRequest(IReadOnlyList<UsageEventDto> Events);

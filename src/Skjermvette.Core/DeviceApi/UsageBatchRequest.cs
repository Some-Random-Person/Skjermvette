namespace Skjermvette.Core.DeviceApi;

public sealed record UsageBatchRequest(IReadOnlyList<UsageEventDto> Events);

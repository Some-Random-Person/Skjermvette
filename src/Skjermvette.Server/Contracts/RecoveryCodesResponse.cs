namespace Skjermvette.Server.Contracts;

/// <summary>New single-use recovery codes, shown once. They replace any earlier ones.</summary>
public sealed record RecoveryCodesResponse(IReadOnlyList<string> Codes);

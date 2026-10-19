namespace Skjermvette.Server.Contracts;

/// <summary>What deleting the account would remove. Children and devices are only deleted when <see cref="IsLastParent"/>.</summary>
public sealed record AccountDeletionResponse(bool IsLastParent, int Children, int Devices);

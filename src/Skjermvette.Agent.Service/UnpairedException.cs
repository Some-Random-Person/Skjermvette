namespace Skjermvette.Agent.Service;

/// <summary>The server no longer accepts this device's token: a parent unpaired it, so it needs pairing again.</summary>
public class UnpairedException() : Exception("The server doesn't accept this device's token. It has been unpaired; pair it again.");

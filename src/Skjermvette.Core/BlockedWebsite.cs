namespace Skjermvette.Core;

/// <summary>A blocked domain (with its subdomains). <see cref="App"/> is the app whose own limit is used up, or null when it's the daily total.</summary>
public sealed record BlockedWebsite(string Domain, string? App);

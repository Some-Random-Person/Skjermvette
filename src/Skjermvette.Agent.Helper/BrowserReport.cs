namespace Skjermvette.Agent.Helper;

/// <summary>
/// What the browser extension reports: which browser program it runs in (<c>chrome.exe</c>, <c>msedge.exe</c>), the domain
/// of the active tab in its last focused window (null for pages that aren't websites, e.g. a new tab), and the domains of
/// the tabs making sound (not muted).
/// </summary>
public sealed record BrowserReport(string Browser, string? Active, string[] Audible);

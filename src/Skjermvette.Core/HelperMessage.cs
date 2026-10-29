namespace Skjermvette.Core;

/// <summary>
/// What the Windows agent's Service sends the Helper, one per line: either a <see cref="HelperNotice"/> to show the child,
/// or the <see cref="WebsiteBlocks"/> for the browser extension, sent whenever they change.
/// </summary>
public sealed record HelperMessage(HelperNotice? Notice = null, WebsiteBlocks? Websites = null);

using Skjermvette.Core.Models;

namespace Skjermvette.Core;

/// <summary>
/// One sample from the Windows agent's Helper: everything in use right now (empty when nothing is), and the browser
/// programs whose extension is connected, which block websites themselves, so the Service doesn't close them.
/// </summary>
public sealed record HelperSample(UsageKey[] InUse, string[] Browsers);

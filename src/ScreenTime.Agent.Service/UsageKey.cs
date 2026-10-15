using ScreenTime.Core.Models;

namespace ScreenTime.Agent.Service;

/// <summary>Something in use: a program (process name) or a website (domain).</summary>
public sealed record UsageKey(MatcherKindEnum Kind, string Identifier);

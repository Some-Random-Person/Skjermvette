using ScreenTime.Core.Models;

namespace ScreenTime.Server.Contracts;

public sealed record IdentifierDto(MatcherKindEnum Kind, string Value);

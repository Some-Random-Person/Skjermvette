using Skjermvette.Core.Models;

namespace Skjermvette.Server.Contracts;

public sealed record IdentifierDto(MatcherKindEnum Kind, string Value);

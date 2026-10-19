using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Skjermvette.Server.Data;

/// <summary>All DateTimes are stored as UTC; SQLite loses the kind, so restore it on read.</summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

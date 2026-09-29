using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SportMate.Infrastructure.Persistence.Conventions;

/// <summary>
/// MySQL DATETIME has no time zone; values are stored as UTC and read back with <see cref="DateTimeKind.Utc"/>.
/// </summary>
internal sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

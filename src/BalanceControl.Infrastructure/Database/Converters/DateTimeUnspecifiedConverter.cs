using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BalanceControl.Infrastructure.Database.Converters;

/// <summary>
/// Garante DateTimeKind.Unspecified em leitura e escrita no banco.
/// Necessário para compatibilidade com timestamp without time zone do PostgreSQL/Npgsql 7+,
/// que rejeita DateTime com Kind=UTC ou Kind=Local.
/// Registrado globalmente em ApplicationDbContext.OnModelCreating.
/// </summary>
public sealed class DateTimeUnspecifiedConverter()
    : ValueConverter<DateTime, DateTime>(
        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
        v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));

public sealed class NullableDateTimeUnspecifiedConverter()
    : ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : null,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : null);

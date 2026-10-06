using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.Infrastructure.Persistence;

/// <summary>
/// Stores DateTime values as they are (the Domain only accepts UTC) and marks values read from the database as
/// <see cref="DateTimeKind.Utc"/>. Applied to every DateTime property in <see cref="CrmDbContext.ConfigureConventions"/>.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

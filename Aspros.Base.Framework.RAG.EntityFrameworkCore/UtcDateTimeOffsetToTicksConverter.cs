using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aspros.Base.Framework.RAG.EntityFrameworkCore;

/// <summary>
/// Stores UTC instants as ticks so range predicates work consistently on providers
/// such as SQLite, which do not support ordering DateTimeOffset natively.
/// The RAG persistence contract treats all timestamps as UTC instants.
/// </summary>
internal sealed class UtcDateTimeOffsetToTicksConverter
    : ValueConverter<DateTimeOffset, long>
{
    public UtcDateTimeOffsetToTicksConverter()
        : base(
            value => value.UtcDateTime.Ticks,
            ticks => new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)))
    {
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Persistence.Conversions
{
    /// <summary>
    /// Stores a list of small records as one JSON value in a single jsonb
    /// column, instead of a child table. Suits data that is always read and
    /// written as a whole together with its owner, and never queried on its
    /// own -- ImportJob's Rows and Errors.
    /// </summary>
    internal static class JsonListConversion
    {
        // Enums as their names ("Expense"), not numbers, so the stored JSON
        // stays readable and doesn't silently change meaning if an enum's
        // members are ever reordered.
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };

        public static PropertyBuilder<IReadOnlyList<T>> HasJsonListConversion<T>(this PropertyBuilder<IReadOnlyList<T>> builder)
        {
            var converter = new ValueConverter<IReadOnlyList<T>, string>(
                list => JsonSerializer.Serialize(list, Options),
                json => JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>());

            // Without a ValueComparer, EF Core compares a list property by
            // reference only, so replacing the list with a new one works,
            // but EF can't reliably tell whether the list changed. This
            // compares by content, and snapshots a copy for that comparison.
            var comparer = new ValueComparer<IReadOnlyList<T>>(
                (left, right) => left!.SequenceEqual(right!),
                list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item!.GetHashCode())),
                list => list.ToList());

            builder.HasConversion(converter, comparer).HasColumnType("jsonb");

            return builder;
        }
    }
}

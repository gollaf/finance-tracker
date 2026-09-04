using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Persistence.Conversions
{
    /// <summary>
    /// Converts a strongly-typed ID (any of the readonly record structs in
    /// FinanceTracker.Domain.Common — AccountId, CategoryId, and so on —
    /// each wrapping a single Guid) to and from the raw Guid column EF Core
    /// actually persists. One reusable, generic converter instead of a
    /// near-identical class per ID type, or an inline HasConversion lambda
    /// repeated at every property that uses one. See ADR 0003.
    /// </summary>
    public sealed class StronglyTypedIdValueConverter<TId> : ValueConverter<TId, Guid>
        where TId : struct
    {
        public StronglyTypedIdValueConverter(Func<TId, Guid> toGuid, Func<Guid, TId> fromGuid)
            : base(id => toGuid(id), value => fromGuid(value))
        {
        }
    }
}

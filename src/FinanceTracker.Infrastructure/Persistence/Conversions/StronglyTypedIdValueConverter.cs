using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FinanceTracker.Infrastructure.Persistence.Conversions
{
    /// <summary>
    /// Converts a strongly-typed ID (AccountId, CategoryId, ...) to and from
    /// the Guid column it is stored in. See ADR 0003.
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

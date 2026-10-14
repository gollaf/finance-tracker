namespace FinanceTracker.Domain.Common
{
    /// <summary>
    /// The user who owns a piece of data. Users themselves live outside the
    /// Domain (in the identity store); the Domain only ever sees their id.
    /// No New(): user ids are issued when a user registers, never here.
    /// </summary>
    public readonly record struct UserId(Guid Value)
    {
        public override string ToString() => Value.ToString();
    }
}

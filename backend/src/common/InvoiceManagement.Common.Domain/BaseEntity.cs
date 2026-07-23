namespace InvoiceManagement.Common.Domain;

/// <summary>
/// Base class for all domain entities. Provides equality by identifier.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<DomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; }
    public DateTimeOffset UpdatedAt { get; protected set; }
    public byte[] RowVersion { get; protected set; } = [];

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj)
    {
        if (obj is not BaseEntity other)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (Id == Guid.Empty || other.Id == Guid.Empty)
            return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(BaseEntity? left, BaseEntity? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(BaseEntity? left, BaseEntity? right) => !(left == right);
}

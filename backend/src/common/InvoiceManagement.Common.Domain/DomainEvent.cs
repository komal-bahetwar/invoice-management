using MediatR;

namespace InvoiceManagement.Common.Domain;

/// <summary>
/// Base class for domain events. Implements INotification for MediatR dispatch.
/// </summary>
public abstract class DomainEvent : INotification
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    public Guid TenantId { get; init; }
}

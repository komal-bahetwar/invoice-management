using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Domain.Enums;

namespace InvoiceManagement.Modules.Invoicing.Domain.Events;

/// <summary>
/// Raised when an invoice's status changes.
/// </summary>
public sealed class InvoiceStatusChangedDomainEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public InvoiceStatus OldStatus { get; }
    public InvoiceStatus NewStatus { get; }

    public InvoiceStatusChangedDomainEvent(
        Guid invoiceId,
        Guid tenantId,
        string invoiceNumber,
        InvoiceStatus oldStatus,
        InvoiceStatus newStatus)
    {
        InvoiceId = invoiceId;
        TenantId = tenantId;
        InvoiceNumber = invoiceNumber;
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}

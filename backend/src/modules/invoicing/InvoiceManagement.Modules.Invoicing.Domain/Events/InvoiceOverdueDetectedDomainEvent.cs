using InvoiceManagement.Common.Domain;

namespace InvoiceManagement.Modules.Invoicing.Domain.Events;

/// <summary>
/// Raised when the system detects an invoice has passed its due date.
/// </summary>
public sealed class InvoiceOverdueDetectedDomainEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public decimal OutstandingAmount { get; }
    public string Currency { get; }
    public DateTimeOffset DueDate { get; }

    public InvoiceOverdueDetectedDomainEvent(
        Guid invoiceId,
        Guid tenantId,
        string invoiceNumber,
        decimal outstandingAmount,
        string currency,
        DateTimeOffset dueDate)
    {
        InvoiceId = invoiceId;
        TenantId = tenantId;
        InvoiceNumber = invoiceNumber;
        OutstandingAmount = outstandingAmount;
        Currency = currency;
        DueDate = dueDate;
    }
}

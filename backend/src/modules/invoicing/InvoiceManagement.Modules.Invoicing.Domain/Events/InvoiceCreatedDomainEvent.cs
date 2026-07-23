using InvoiceManagement.Common.Domain;

namespace InvoiceManagement.Modules.Invoicing.Domain.Events;

/// <summary>
/// Raised when a new invoice is created.
/// </summary>
public sealed class InvoiceCreatedDomainEvent : DomainEvent
{
    public Guid InvoiceId { get; }
    public string InvoiceNumber { get; }
    public string CustomerName { get; }
    public decimal TotalAmount { get; }
    public string Currency { get; }

    public InvoiceCreatedDomainEvent(
        Guid invoiceId,
        Guid tenantId,
        string invoiceNumber,
        string customerName,
        decimal totalAmount,
        string currency)
    {
        InvoiceId = invoiceId;
        TenantId = tenantId;
        InvoiceNumber = invoiceNumber;
        CustomerName = customerName;
        TotalAmount = totalAmount;
        Currency = currency;
    }
}

namespace InvoiceManagement.Modules.Invoicing.Domain.Enums;

/// <summary>
/// Invoice status lifecycle:
/// Draft → Sent → Paid (terminal)
/// Draft → Sent → Overdue → Paid (terminal)
/// Draft → Sent → Cancelled (terminal)
/// Draft → Cancelled (terminal)
/// </summary>
public enum InvoiceStatus
{
    Draft = 1,
    Sent = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}

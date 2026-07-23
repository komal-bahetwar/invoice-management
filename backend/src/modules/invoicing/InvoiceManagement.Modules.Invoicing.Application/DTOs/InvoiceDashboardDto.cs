namespace InvoiceManagement.Modules.Invoicing.Application.DTOs;

public sealed record InvoiceDashboardDto(
    int TotalInvoices,
    decimal TotalAmount,
    Dictionary<string, int> ByStatus,
    decimal OverdueAmount,
    decimal PaidThisMonth,
    string Currency);

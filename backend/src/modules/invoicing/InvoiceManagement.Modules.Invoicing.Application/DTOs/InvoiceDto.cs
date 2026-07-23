using InvoiceManagement.Modules.Invoicing.Domain.Enums;

namespace InvoiceManagement.Modules.Invoicing.Application.DTOs;

public sealed record InvoiceDto(
    Guid Id,
    string InvoiceNumber,
    string CustomerName,
    string CustomerEmail,
    string? CustomerAddress,
    DateTimeOffset IssueDate,
    DateTimeOffset DueDate,
    string Status,
    decimal SubTotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    string Currency,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<InvoiceLineItemDto> LineItems);

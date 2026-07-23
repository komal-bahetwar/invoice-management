namespace InvoiceManagement.Modules.Invoicing.Application.DTOs;

public sealed record InvoiceLineItemDto(
    Guid Id,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

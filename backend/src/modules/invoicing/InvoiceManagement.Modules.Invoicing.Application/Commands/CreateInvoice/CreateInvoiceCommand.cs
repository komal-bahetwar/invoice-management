using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using MediatR;

namespace InvoiceManagement.Modules.Invoicing.Application.Commands.CreateInvoice;

public sealed record CreateInvoiceLineItemRequest(
    string Description,
    int Quantity,
    decimal UnitPrice);

public sealed record CreateInvoiceCommand(
    string CustomerName,
    string CustomerEmail,
    string? CustomerAddress,
    DateTimeOffset IssueDate,
    DateTimeOffset DueDate,
    decimal TaxRate,
    string Currency,
    string? Notes,
    List<CreateInvoiceLineItemRequest> LineItems) : IRequest<Result<InvoiceDto>>;

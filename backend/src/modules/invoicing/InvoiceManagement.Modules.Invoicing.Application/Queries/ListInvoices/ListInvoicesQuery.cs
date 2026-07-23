using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using MediatR;

namespace InvoiceManagement.Modules.Invoicing.Application.Queries.ListInvoices;

public sealed record ListInvoicesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? SearchTerm = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null) : IRequest<PagedResponse<InvoiceDto>>;

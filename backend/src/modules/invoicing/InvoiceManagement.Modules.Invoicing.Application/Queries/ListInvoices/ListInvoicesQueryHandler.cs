using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;
using Mapster;
using MediatR;

namespace InvoiceManagement.Modules.Invoicing.Application.Queries.ListInvoices;

public sealed class ListInvoicesQueryHandler : IRequestHandler<ListInvoicesQuery, PagedResponse<InvoiceDto>>
{
    private readonly IInvoiceRepository _repository;

    public ListInvoicesQueryHandler(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResponse<InvoiceDto>> Handle(ListInvoicesQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _repository.ListAsync(
            request.Page,
            request.PageSize,
            request.Status,
            request.SearchTerm,
            request.FromDate,
            request.ToDate,
            ct);

        var dtos = items.Adapt<IReadOnlyList<InvoiceDto>>();
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResponse<InvoiceDto>(dtos, request.Page, request.PageSize, totalCount, totalPages);
    }
}

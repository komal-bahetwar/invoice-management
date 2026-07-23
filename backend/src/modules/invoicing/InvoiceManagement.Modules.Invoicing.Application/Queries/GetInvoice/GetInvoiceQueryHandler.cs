using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;
using Mapster;
using MediatR;

namespace InvoiceManagement.Modules.Invoicing.Application.Queries.GetInvoice;

public sealed class GetInvoiceQueryHandler : IRequestHandler<GetInvoiceQuery, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _repository;

    public GetInvoiceQueryHandler(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<InvoiceDto>> Handle(GetInvoiceQuery request, CancellationToken ct)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, ct);
        if (invoice is null)
            return Result<InvoiceDto>.Failure($"Invoice {request.InvoiceId} not found.");

        return Result<InvoiceDto>.Success(invoice.Adapt<InvoiceDto>());
    }
}

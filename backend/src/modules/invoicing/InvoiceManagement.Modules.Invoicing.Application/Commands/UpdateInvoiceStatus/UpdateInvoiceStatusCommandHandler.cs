using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InvoiceManagement.Modules.Invoicing.Application.Commands.UpdateInvoiceStatus;

public sealed class UpdateInvoiceStatusCommandHandler : IRequestHandler<UpdateInvoiceStatusCommand, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateInvoiceStatusCommandHandler> _logger;

    public UpdateInvoiceStatusCommandHandler(
        IInvoiceRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateInvoiceStatusCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<InvoiceDto>> Handle(UpdateInvoiceStatusCommand request, CancellationToken ct)
    {
        var invoice = await _repository.GetByIdAsync(request.InvoiceId, ct);
        if (invoice is null)
            return Result<InvoiceDto>.Failure($"Invoice {request.InvoiceId} not found.");

        Result transitionResult = request.Status switch
        {
            Domain.Enums.InvoiceStatus.Sent => invoice.MarkAsSent(),
            Domain.Enums.InvoiceStatus.Paid => invoice.MarkAsPaid(),
            Domain.Enums.InvoiceStatus.Cancelled => invoice.MarkAsCancelled(),
            _ => Result.Failure($"Status {request.Status} cannot be set manually.")
        };

        if (transitionResult.IsFailure)
        {
            _logger.LogWarning("Status transition failed for invoice {InvoiceId}: {Errors}",
                request.InvoiceId, string.Join(", ", transitionResult.Errors));
            return Result<InvoiceDto>.Failure(transitionResult.Errors);
        }

        _repository.Update(invoice);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Invoice {InvoiceId} status changed to {Status}",
            request.InvoiceId, request.Status);

        return Result<InvoiceDto>.Success(invoice.Adapt<InvoiceDto>());
    }
}

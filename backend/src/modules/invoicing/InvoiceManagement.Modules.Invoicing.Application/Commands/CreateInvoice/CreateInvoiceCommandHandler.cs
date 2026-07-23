using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using InvoiceManagement.Modules.Invoicing.Domain.Entities;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;
using InvoiceManagement.Modules.Invoicing.Domain.ValueObjects;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InvoiceManagement.Modules.Invoicing.Application.Commands.CreateInvoice;

public sealed class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Result<InvoiceDto>>
{
    private readonly IInvoiceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateInvoiceCommandHandler> _logger;

    public CreateInvoiceCommandHandler(
        IInvoiceRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateInvoiceCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<InvoiceDto>> Handle(CreateInvoiceCommand request, CancellationToken ct)
    {
        // Build line items
        var lineItemResults = new List<Result<InvoiceLineItem>>();
        foreach (var li in request.LineItems)
        {
            var result = InvoiceLineItem.Create(li.Description, li.Quantity, li.UnitPrice);
            if (result.IsFailure)
                return Result<InvoiceDto>.Failure(result.Errors);
            lineItemResults.Add(result);
        }

        var lineItems = lineItemResults.Select(r => r.Value!).ToList();

        // Generate invoice number
        var year = request.IssueDate.Year;
        var sequence = await _repository.GetNextSequenceNumberAsync(year, ct) + 1;
        var invoiceNumber = InvoiceNumber.Create(year, sequence);

        // Check uniqueness
        var existing = await _repository.GetByInvoiceNumberAsync(invoiceNumber.Value, ct);
        if (existing is not null)
        {
            _logger.LogWarning("Invoice number collision detected: {InvoiceNumber}", invoiceNumber.Value);
            return Result<InvoiceDto>.Failure($"Invoice number {invoiceNumber.Value} already exists.");
        }

        // Create invoice (tenant ID will be set from context)
        var createResult = Invoice.Create(
            invoiceNumber,
            request.CustomerName,
            request.CustomerEmail,
            request.CustomerAddress,
            request.IssueDate,
            request.DueDate,
            request.TaxRate,
            request.Currency,
            request.Notes,
            lineItems,
            Guid.Empty); // Tenant ID resolved by Finbuckle middleware

        if (createResult.IsFailure)
            return Result<InvoiceDto>.Failure(createResult.Errors);

        var invoice = createResult.Value!;
        await _repository.AddAsync(invoice, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Invoice {InvoiceNumber} created successfully", invoice.InvoiceNumber.Value);

        return Result<InvoiceDto>.Success(invoice.Adapt<InvoiceDto>());
    }
}

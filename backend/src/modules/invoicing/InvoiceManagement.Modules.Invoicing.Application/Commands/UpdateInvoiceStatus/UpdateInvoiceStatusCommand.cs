using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using InvoiceManagement.Modules.Invoicing.Domain.Enums;
using MediatR;

namespace InvoiceManagement.Modules.Invoicing.Application.Commands.UpdateInvoiceStatus;

public sealed record UpdateInvoiceStatusCommand(
    Guid InvoiceId,
    InvoiceStatus Status) : IRequest<Result<InvoiceDto>>;

using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using MediatR;

namespace InvoiceManagement.Modules.Invoicing.Application.Queries.GetInvoice;

public sealed record GetInvoiceQuery(Guid InvoiceId) : IRequest<Result<InvoiceDto>>;

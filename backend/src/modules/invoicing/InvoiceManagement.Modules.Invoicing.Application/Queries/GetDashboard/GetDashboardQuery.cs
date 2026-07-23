using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using MediatR;

namespace InvoiceManagement.Modules.Invoicing.Application.Queries.GetDashboard;

public sealed record GetDashboardQuery : IRequest<InvoiceDashboardDto>;

using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;
using MediatR;

namespace InvoiceManagement.Modules.Invoicing.Application.Queries.GetDashboard;

public sealed class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, InvoiceDashboardDto>
{
    private readonly IInvoiceRepository _repository;

    public GetDashboardQueryHandler(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<InvoiceDashboardDto> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        // Fetch all invoices for the tenant (dashboard is aggregated in-memory for assessment scope)
        var (allInvoices, _) = await _repository.ListAsync(1, int.MaxValue, ct: ct);

        var byStatus = allInvoices
            .GroupBy(i => i.Status.ToString().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        // Ensure all statuses are present
        foreach (var status in new[] { "draft", "sent", "paid", "overdue", "cancelled" })
        {
            byStatus.TryAdd(status, 0);
        }

        var now = DateTimeOffset.UtcNow;
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return new InvoiceDashboardDto(
            TotalInvoices: allInvoices.Count,
            TotalAmount: allInvoices.Sum(i => i.TotalAmount),
            ByStatus: byStatus,
            OverdueAmount: allInvoices
                .Where(i => i.Status == Domain.Enums.InvoiceStatus.Overdue)
                .Sum(i => i.TotalAmount),
            PaidThisMonth: allInvoices
                .Where(i => i.Status == Domain.Enums.InvoiceStatus.Paid && i.UpdatedAt >= startOfMonth)
                .Sum(i => i.TotalAmount),
            Currency: allInvoices.FirstOrDefault()?.Currency ?? "USD"
        );
    }
}

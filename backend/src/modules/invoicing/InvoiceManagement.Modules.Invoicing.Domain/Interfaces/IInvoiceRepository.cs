using InvoiceManagement.Modules.Invoicing.Domain.Entities;

namespace InvoiceManagement.Modules.Invoicing.Domain.Interfaces;

/// <summary>
/// Repository contract for Invoice aggregate persistence.
/// </summary>
public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken ct = default);
    Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        string? statusFilter = null,
        string? searchTerm = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken ct = default);
    Task<int> GetNextSequenceNumberAsync(int year, CancellationToken ct = default);
    Task AddAsync(Invoice invoice, CancellationToken ct = default);
    void Update(Invoice invoice);
}

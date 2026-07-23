using InvoiceManagement.Modules.Invoicing.Domain.Entities;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;
using InvoiceManagement.Modules.Invoicing.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace InvoiceManagement.Modules.Invoicing.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IInvoiceRepository.
/// </summary>
public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly Data.InvoicingDbContext _context;

    public InvoiceRepository(Data.InvoicingDbContext context)
    {
        _context = context;
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    // public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken ct = default)
    // {
    //     return await _context.Invoices
    //         .FirstOrDefaultAsync(i => i.InvoiceNumber.Value == invoiceNumber, ct);
    // }
    
    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken ct = default)
    {
        // Create an instance of your Value Object first (use your specific constructor or factory method)
        var targetInvoiceNumber = new InvoiceNumber(invoiceNumber); 
        // Or if you use a factory: var targetInvoiceNumber = InvoiceNumber.Create(invoiceNumber);

        return await _context.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceNumber == targetInvoiceNumber, ct); //  Translates perfectly!
    }

    public async Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        string? statusFilter = null,
        string? searchTerm = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        var query = _context.Invoices
            .Include(i => i.LineItems)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter) &&
            Enum.TryParse<Domain.Enums.InvoiceStatus>(statusFilter, ignoreCase: true, out var status))
        {
            query = query.Where(i => i.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(i =>
                i.CustomerName.ToLower().Contains(term) ||
                i.InvoiceNumber.Value.ToLower().Contains(term) ||
                i.CustomerEmail.ToLower().Contains(term));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(i => i.IssueDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(i => i.IssueDate <= toDate.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.IssueDate)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<int> GetNextSequenceNumberAsync(int year, CancellationToken ct = default)
    {
        var count = await _context.Invoices
            .Where(i => i.IssueDate.Year == year)
            .CountAsync(ct);

        return count;
    }

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        await _context.Invoices.AddAsync(invoice, ct);
    }

    public void Update(Invoice invoice)
    {
        _context.Invoices.Update(invoice);
    }
}

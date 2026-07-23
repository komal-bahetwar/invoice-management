namespace InvoiceManagement.Modules.Invoicing.Domain.Interfaces;

/// <summary>
/// Unit of Work contract for transactional consistency.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

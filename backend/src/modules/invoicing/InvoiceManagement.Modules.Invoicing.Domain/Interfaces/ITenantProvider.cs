namespace InvoiceManagement.Modules.Invoicing.Domain.Interfaces;

/// <summary>
/// Abstraction for resolving the current tenant ID from the multi-tenancy infrastructure.
/// Keeps the Domain and Application layers decoupled from Finbuckle.
/// </summary>
public interface ITenantProvider
{
    Guid GetTenantId();
}

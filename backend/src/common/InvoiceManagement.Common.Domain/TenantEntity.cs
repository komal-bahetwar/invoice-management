namespace InvoiceManagement.Common.Domain;

/// <summary>
/// Base class for domain entities that belong to a specific tenant.
/// Extends <see cref="BaseEntity"/> with a <see cref="TenantId"/> property.
/// Entities that do not require multi-tenant isolation should inherit from <see cref="BaseEntity"/> directly.
/// </summary>
public abstract class TenantEntity : BaseEntity
{
    public Guid TenantId { get; protected set; }
}

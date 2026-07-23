using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;

namespace InvoiceManagement.Modules.Invoicing.Infrastructure.Tenant;

/// <summary>
/// Resolves the current tenant ID from the Finbuckle <see cref="IMultiTenantContextAccessor{ITenantInfo}"/>.
/// </summary>
public sealed class TenantProvider : ITenantProvider
{
    private readonly IMultiTenantContextAccessor _accessor;

    public TenantProvider(IMultiTenantContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid GetTenantId()
    {
        var tenantInfo = _accessor.MultiTenantContext?.TenantInfo
            ?? throw new InvalidOperationException("No tenant context available. Ensure the multi-tenant middleware is enabled and a valid tenant identifier is provided.");

        if (!Guid.TryParse(tenantInfo.Id, out var tenantId))
            throw new InvalidOperationException($"Tenant ID '{tenantInfo.Id}' is not a valid GUID.");

        return tenantId;
    }
}

using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InvoiceManagement.Modules.Invoicing.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core migrations. Uses a stub multi-tenant context
/// since the full tenant pipeline is not available during migration generation.
/// </summary>
public sealed class InvoicingDbContextFactory : IDesignTimeDbContextFactory<InvoicingDbContext>
{
    public InvoicingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InvoicingDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost,1433;Database=InvoiceManagement;User Id=sa;Password=YourStrong!Pass;TrustServerCertificate=True;");

        var tenantInfo = new TenantInfo
        {
            Id = "00000000-0000-0000-0000-000000000000",
            Identifier = "design-time",
            Name = "Design-Time Tenant"
        };

        var accessor = new StubMultiTenantContextAccessor
        {
            MultiTenantContext = new MultiTenantContext<TenantInfo>(tenantInfo)
        };

        return new InvoicingDbContext(accessor, optionsBuilder.Options);
    }

    private sealed class StubMultiTenantContextAccessor : IMultiTenantContextAccessor
    {
        public IMultiTenantContext? MultiTenantContext { get; set; }
    }
}

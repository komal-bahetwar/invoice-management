using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Domain.Entities;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InvoiceManagement.Modules.Invoicing.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for the Invoicing module. Multi-tenant aware via Finbuckle.
/// Also implements IUnitOfWork for transactional consistency.
/// </summary>
public sealed class InvoicingDbContext : MultiTenantDbContext, IUnitOfWork
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();

    public InvoicingDbContext(
        IMultiTenantContextAccessor accessor,
        DbContextOptions<InvoicingDbContext> options)
        : base(accessor, options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Ignore<DomainEvent>();
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoicingDbContext).Assembly);
    }
}

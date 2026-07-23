using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using InvoiceManagement.Modules.Invoicing.Domain.Entities;
using InvoiceManagement.Modules.Invoicing.Domain.Enums;
using InvoiceManagement.Modules.Invoicing.Domain.ValueObjects;
using InvoiceManagement.Modules.Invoicing.Infrastructure.Data;
using InvoiceManagement.Modules.Invoicing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.MsSql;
using Xunit;

namespace InvoiceManagement.Modules.Invoicing.IntegrationTests;

public class InvoiceRepositoryTests : IAsyncLifetime
{
    private sealed class TestMultiTenantContextAccessor : IMultiTenantContextAccessor
    {
        public IMultiTenantContext? MultiTenantContext { get; set; }
    }

    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithPassword("YourStrong!Pass")
        .Build();

    private InvoicingDbContext _context = null!;
    private InvoiceRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<InvoicingDbContext>()
            .UseSqlServer(_sqlContainer.GetConnectionString())
            .Options;

        // Use a simple TenantInfo for integration tests
        var tenantInfo = new TenantInfo
        {
            Id = "test-tenant",
            Identifier = "test-tenant",
            Name = "Test Tenant"
        };

        var multiTenantContext = new MultiTenantContext<TenantInfo>(tenantInfo);
        var accessor = new TestMultiTenantContextAccessor { MultiTenantContext = multiTenantContext };

        _context = new InvoicingDbContext(accessor, options);
        await _context.Database.EnsureCreatedAsync();
        _repository = new InvoiceRepository(_context);
    }

    public async Task DisposeAsync()
    {
        if (_context is not null)
            await _context.DisposeAsync();
        if (_sqlContainer is not null)
            await _sqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_And_GetById_ShouldWork()
    {
        var invoice = CreateValidInvoice();
        await _repository.AddAsync(invoice);
        await _context.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(invoice.Id);

        retrieved.ShouldNotBeNull();
        retrieved!.InvoiceNumber.Value.ShouldBe("INV-2026-000001");
        retrieved.CustomerName.ShouldBe("Test Corp");
    }

    [Fact]
    public async Task ListAsync_ShouldReturnPagedResults()
    {
        // Add 5 invoices
        for (int i = 0; i < 5; i++)
        {
            var invoice = CreateValidInvoice($"INV-2026-{i + 1:D6}", $"Customer {i + 1}");
            await _repository.AddAsync(invoice);
        }
        await _context.SaveChangesAsync();

        var (items, totalCount) = await _repository.ListAsync(1, 2);

        items.Count.ShouldBe(2);
        totalCount.ShouldBe(5);
    }

    [Fact]
    public async Task ListAsync_WithStatusFilter_ShouldFilter()
    {
        var draft = CreateValidInvoice("INV-2026-000001", "Draft Invoice");
        var sent = CreateValidInvoice("INV-2026-000002", "Sent Invoice");
        sent.MarkAsSent();

        await _repository.AddAsync(draft);
        await _repository.AddAsync(sent);
        await _context.SaveChangesAsync();

        var (items, _) = await _repository.ListAsync(1, 10, statusFilter: "Sent");

        items.Count.ShouldBe(1);
        items[0].Status.ShouldBe(InvoiceStatus.Sent);
    }

    [Fact]
    public async Task GetByInvoiceNumber_ShouldFindByNumber()
    {
        var invoice = CreateValidInvoice("INV-2026-000042", "Test");
        await _repository.AddAsync(invoice);
        await _context.SaveChangesAsync();

        var found = await _repository.GetByInvoiceNumberAsync("INV-2026-000042");

        found.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetNextSequenceNumber_ShouldReturnCorrectCount()
    {
        await _repository.AddAsync(CreateValidInvoice("INV-2026-000001", "First"));
        await _repository.AddAsync(CreateValidInvoice("INV-2026-000002", "Second"));
        await _context.SaveChangesAsync();

        var next = await _repository.GetNextSequenceNumberAsync(2026);

        next.ShouldBe(2); // count of invoices in 2026
    }

    [Fact]
    public async Task Update_WithOptimisticConcurrency_ShouldDetectConflicts()
    {
        var invoice = CreateValidInvoice();
        await _repository.AddAsync(invoice);
        await _context.SaveChangesAsync();

        // Simulate concurrent update by loading the same invoice in a different context
        var options = new DbContextOptionsBuilder<InvoicingDbContext>()
            .UseSqlServer(_sqlContainer.GetConnectionString())
            .Options;
        var tenantInfo2 = new TenantInfo
        {
            Id = "test-tenant",
            Identifier = "test-tenant",
            Name = "Test Tenant"
        };

        var accessor2 = new TestMultiTenantContextAccessor { MultiTenantContext = new MultiTenantContext<TenantInfo>(tenantInfo2) };
        await using var context2 = new InvoicingDbContext(accessor2, options);
        var invoiceInContext2 = await context2.Invoices.FindAsync(invoice.Id);
        invoiceInContext2!.MarkAsSent();
        await context2.SaveChangesAsync();

        // Now try to update the original — should throw DbUpdateConcurrencyException
        invoice.MarkAsSent();
        Should.Throw<DbUpdateConcurrencyException>(() => _context.SaveChanges());
    }

    private static Invoice CreateValidInvoice(string? invoiceNumber = null, string? customerName = null)
    {
        var lineItem = InvoiceLineItem.Create("Test service", 1, 100m).Value!;
        return Invoice.Create(
            new InvoiceNumber(invoiceNumber ?? "INV-2026-000001"),
            customerName ?? "Test Corp",
            "test@test.com",
            null,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            10m,
            "USD",
            null,
            new List<InvoiceLineItem> { lineItem },
            Guid.NewGuid()).Value!;
    }
}

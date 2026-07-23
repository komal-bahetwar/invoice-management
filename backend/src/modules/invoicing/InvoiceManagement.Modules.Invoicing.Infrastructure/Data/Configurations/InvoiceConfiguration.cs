using InvoiceManagement.Modules.Invoicing.Domain.Entities;
using InvoiceManagement.Modules.Invoicing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceManagement.Modules.Invoicing.Infrastructure.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .ValueGeneratedNever();

        builder.Property(i => i.InvoiceNumber)
            .HasConversion(
                v => v.Value,
                v => new Domain.ValueObjects.InvoiceNumber(v))
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        builder.Property(i => i.CustomerName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(i => i.CustomerEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(i => i.CustomerAddress)
            .HasMaxLength(500);

        builder.Property(i => i.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(i => i.Status);

        builder.Property(i => i.SubTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.TaxRate)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(i => i.TaxAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(i => i.Notes)
            .HasMaxLength(2000);

        builder.HasIndex(i => i.DueDate);
        builder.HasIndex(i => i.IssueDate);
        builder.HasIndex(i => new { i.Status, i.DueDate });

        builder.Property(i => i.TenantId)
            .IsRequired();

        builder.HasIndex(i => i.TenantId);

        builder.Property(i => i.RowVersion)
            .IsRowVersion();

        builder.HasMany(i => i.LineItems)
            .WithOne()
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(i => i.DomainEvents);
        builder.Ignore(i => i.IsTerminal);
    }
}

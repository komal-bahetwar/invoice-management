using InvoiceManagement.Modules.Invoicing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceManagement.Modules.Invoicing.Infrastructure.Data.Configurations;

public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");

        builder.HasKey(li => li.Id);

        builder.Property(li => li.Id)
            .ValueGeneratedNever();

        builder.Property(li => li.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(li => li.Quantity)
            .IsRequired();

        builder.Property(li => li.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(li => li.TotalPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(li => li.InvoiceId);
    }
}

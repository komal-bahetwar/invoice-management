using InvoiceManagement.Common.Domain;

namespace InvoiceManagement.Modules.Invoicing.Domain.Entities;

/// <summary>
/// A line item within an invoice. Owned by the Invoice aggregate root.
/// </summary>
public sealed class InvoiceLineItem : BaseEntity
{
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice { get; private set; }

    // EF Core constructor
    private InvoiceLineItem() { }

    private InvoiceLineItem(string description, int quantity, decimal unitPrice)
    {
        Id = Guid.NewGuid();
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalPrice = quantity * unitPrice;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Factory with validation.
    /// </summary>
    public static Result<InvoiceLineItem> Create(string description, int quantity, decimal unitPrice)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(description))
            errors.Add("Line item description is required.");
        if (description?.Length > 500 == true)
            errors.Add("Line item description cannot exceed 500 characters.");
        if (quantity <= 0)
            errors.Add("Quantity must be greater than zero.");
        if (unitPrice <= 0)
            errors.Add("Unit price must be greater than zero.");

        if (errors.Count > 0)
            return Result<InvoiceLineItem>.Failure(errors);

        return Result<InvoiceLineItem>.Success(
            new InvoiceLineItem(description.Trim(), quantity, Math.Round(unitPrice, 2)));
    }
}

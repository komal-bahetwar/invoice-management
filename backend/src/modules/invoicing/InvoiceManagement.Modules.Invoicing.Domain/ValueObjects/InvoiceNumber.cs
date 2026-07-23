namespace InvoiceManagement.Modules.Invoicing.Domain.ValueObjects;

/// <summary>
/// Auto-generated invoice number: INV-{year}-{sequence}.
/// Immutable once created.
/// </summary>
public sealed record InvoiceNumber
{
    public string Value { get; }

    public InvoiceNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Invoice number cannot be empty.", nameof(value));
        if (value.Length > 50)
            throw new ArgumentException("Invoice number cannot exceed 50 characters.", nameof(value));

        Value = value;
    }

    public static InvoiceNumber Create(int year, int sequence) =>
        new($"INV-{year}-{sequence:D6}");

    public override string ToString() => Value;
}

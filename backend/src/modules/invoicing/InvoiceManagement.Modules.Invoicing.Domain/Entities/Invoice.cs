using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Domain.Enums;
using InvoiceManagement.Modules.Invoicing.Domain.Events;
using InvoiceManagement.Modules.Invoicing.Domain.ValueObjects;

namespace InvoiceManagement.Modules.Invoicing.Domain.Entities;

/// <summary>
/// Invoice aggregate root. Owns line items and enforces status lifecycle invariants.
/// </summary>
public sealed class Invoice : TenantEntity
{
    private readonly List<InvoiceLineItem> _lineItems = [];

    public InvoiceNumber InvoiceNumber { get; private set; } = null!;
    public string CustomerName { get; private set; } = null!;
    public string CustomerEmail { get; private set; } = null!;
    public string? CustomerAddress { get; private set; }
    public DateTimeOffset IssueDate { get; private set; }
    public DateTimeOffset DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal TaxRate { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? Notes { get; private set; }

    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    // EF Core constructor
    private Invoice() { }

    private Invoice(
        InvoiceNumber invoiceNumber,
        string customerName,
        string customerEmail,
        string? customerAddress,
        DateTimeOffset issueDate,
        DateTimeOffset dueDate,
        decimal taxRate,
        string currency,
        string? notes,
        List<InvoiceLineItem> lineItems,
        Guid tenantId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        InvoiceNumber = invoiceNumber;
        CustomerName = customerName;
        CustomerEmail = customerEmail;
        CustomerAddress = customerAddress;
        IssueDate = issueDate;
        DueDate = dueDate;
        TaxRate = taxRate;
        Currency = currency;
        Notes = notes;
        Status = InvoiceStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var item in lineItems)
        {
            AddLineItem(item);
        }

        RecalculateTotals();

        AddDomainEvent(new InvoiceCreatedDomainEvent(
            Id, tenantId, invoiceNumber.Value, customerName, TotalAmount, currency));
    }

    /// <summary>
    /// Factory method with full validation.
    /// </summary>
    public static Result<Invoice> Create(
        InvoiceNumber invoiceNumber,
        string customerName,
        string customerEmail,
        string? customerAddress,
        DateTimeOffset issueDate,
        DateTimeOffset dueDate,
        decimal taxRate,
        string currency,
        string? notes,
        List<InvoiceLineItem> lineItems,
        Guid tenantId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(customerName))
            errors.Add("Customer name is required.");
        if (string.IsNullOrWhiteSpace(customerEmail))
            errors.Add("Customer email is required.");
        if (!customerEmail.Contains('@'))
            errors.Add("Customer email is invalid.");
        if (dueDate < issueDate)
            errors.Add("Due date must be on or after issue date.");
        if (taxRate < 0 || taxRate > 100)
            errors.Add("Tax rate must be between 0 and 100.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            errors.Add("Currency must be a 3-letter ISO 4217 code.");
        if (lineItems is null || lineItems.Count == 0)
            errors.Add("At least one line item is required.");
        if (tenantId == Guid.Empty)
            errors.Add("Tenant ID is required.");

        if (errors.Count > 0)
            return Result<Invoice>.Failure(errors);

        var invoice = new Invoice(
            invoiceNumber,
            customerName.Trim(),
            customerEmail.Trim(),
            customerAddress?.Trim(),
            issueDate,
            dueDate,
            taxRate,
            currency.ToUpperInvariant(),
            notes?.Trim(),
            lineItems,
            tenantId);

        return Result<Invoice>.Success(invoice);
    }

    /// <summary>
    /// Transition to Sent status.
    /// </summary>
    public Result MarkAsSent()
    {
        if (Status != InvoiceStatus.Draft)
            return Result.Failure($"Cannot mark as Sent from {Status} status. Only Draft invoices can be sent.");

        var oldStatus = Status;
        Status = InvoiceStatus.Sent;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new InvoiceStatusChangedDomainEvent(
            Id, TenantId, InvoiceNumber.Value, oldStatus, Status));

        return Result.Success();
    }

    /// <summary>
    /// Transition to Paid status (terminal).
    /// </summary>
    public Result MarkAsPaid()
    {
        if (Status != InvoiceStatus.Sent && Status != InvoiceStatus.Overdue)
            return Result.Failure($"Cannot mark as Paid from {Status} status. Only Sent or Overdue invoices can be paid.");

        var oldStatus = Status;
        Status = InvoiceStatus.Paid;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new InvoiceStatusChangedDomainEvent(
            Id, TenantId, InvoiceNumber.Value, oldStatus, Status));

        return Result.Success();
    }

    /// <summary>
    /// System-detected overdue transition.
    /// </summary>
    public Result MarkAsOverdue()
    {
        if (Status != InvoiceStatus.Sent)
            return Result.Failure($"Cannot mark as Overdue from {Status} status. Only Sent invoices can become overdue.");

        if (DueDate >= DateTimeOffset.UtcNow)
            return Result.Failure("Cannot mark as Overdue before the due date has passed.");

        var oldStatus = Status;
        Status = InvoiceStatus.Overdue;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new InvoiceStatusChangedDomainEvent(
            Id, TenantId, InvoiceNumber.Value, oldStatus, Status));
        AddDomainEvent(new InvoiceOverdueDetectedDomainEvent(
            Id, TenantId, InvoiceNumber.Value, TotalAmount, Currency, DueDate));

        return Result.Success();
    }

    /// <summary>
    /// Transition to Cancelled status (terminal).
    /// </summary>
    public Result MarkAsCancelled()
    {
        if (Status != InvoiceStatus.Draft && Status != InvoiceStatus.Sent)
            return Result.Failure($"Cannot cancel from {Status} status. Only Draft or Sent invoices can be cancelled.");

        var oldStatus = Status;
        Status = InvoiceStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new InvoiceStatusChangedDomainEvent(
            Id, TenantId, InvoiceNumber.Value, oldStatus, Status));

        return Result.Success();
    }

    private void AddLineItem(InvoiceLineItem item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));
        _lineItems.Add(item);
    }

    private void RecalculateTotals()
    {
        SubTotal = _lineItems.Sum(li => li.TotalPrice);
        TaxAmount = Math.Round(SubTotal * (TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
        TotalAmount = SubTotal + TaxAmount;
    }

    /// <summary>
    /// Whether this invoice is in a terminal state.
    /// </summary>
    public bool IsTerminal => Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled;
}

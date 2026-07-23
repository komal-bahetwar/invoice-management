using InvoiceManagement.Modules.Invoicing.Domain.Entities;
using InvoiceManagement.Modules.Invoicing.Domain.Enums;
using InvoiceManagement.Modules.Invoicing.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace InvoiceManagement.Modules.Invoicing.UnitTests.Domain;

public class InvoiceTests
{
    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var lineItem = InvoiceLineItem.Create("Consulting services", 10, 150.00m).Value!;

        // Act
        var result = Invoice.Create(
            InvoiceNumber.Create(2026, 1),
            "Acme Corp",
            "billing@acme.com",
            "123 Main St",
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            8.5m,
            "USD",
            "Net 30",
            new List<InvoiceLineItem> { lineItem },
            Guid.NewGuid());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var invoice = result.Value!;
        invoice.Status.ShouldBe(InvoiceStatus.Draft);
        invoice.SubTotal.ShouldBe(1500.00m);
        invoice.TaxAmount.ShouldBe(127.50m);
        invoice.TotalAmount.ShouldBe(1627.50m);
        invoice.InvoiceNumber.Value.ShouldBe("INV-2026-000001");
        invoice.DomainEvents.ShouldNotBeEmpty();
    }

    [Fact]
    public void Create_WithEmptyLineItems_ShouldFail()
    {
        var result = Invoice.Create(
            InvoiceNumber.Create(2026, 1),
            "Acme Corp",
            "billing@acme.com",
            null,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            8.5m,
            "USD",
            null,
            new List<InvoiceLineItem>(),
            Guid.NewGuid());

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Contains("line item"));
    }

    [Fact]
    public void Create_WithDueDateBeforeIssueDate_ShouldFail()
    {
        var lineItem = InvoiceLineItem.Create("Test", 1, 100m).Value!;

        var result = Invoice.Create(
            InvoiceNumber.Create(2026, 1),
            "Acme Corp",
            "billing@acme.com",
            null,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), // due before issue
            8.5m,
            "USD",
            null,
            new List<InvoiceLineItem> { lineItem },
            Guid.NewGuid());

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Contains("Due date"));
    }

    [Fact]
    public void MarkAsSent_FromDraft_ShouldSucceed()
    {
        var invoice = CreateValidInvoice();

        var result = invoice.MarkAsSent();

        result.IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Sent);
    }

    [Fact]
    public void MarkAsSent_FromPaid_ShouldFail()
    {
        var invoice = CreateValidInvoice();
        invoice.MarkAsSent();
        invoice.MarkAsPaid();

        var result = invoice.MarkAsSent(); // already paid

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void MarkAsPaid_FromSent_ShouldSucceed()
    {
        var invoice = CreateValidInvoice();
        invoice.MarkAsSent();

        var result = invoice.MarkAsPaid();

        result.IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Paid);
    }

    [Fact]
    public void MarkAsPaid_FromDraft_ShouldFail()
    {
        var invoice = CreateValidInvoice();

        var result = invoice.MarkAsPaid();

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void MarkAsCancelled_FromDraft_ShouldSucceed()
    {
        var invoice = CreateValidInvoice();

        var result = invoice.MarkAsCancelled();

        result.IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void MarkAsCancelled_FromPaid_ShouldFail()
    {
        var invoice = CreateValidInvoice();
        invoice.MarkAsSent();
        invoice.MarkAsPaid();

        var result = invoice.MarkAsCancelled();

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void MarkAsOverdue_FromSent_PastDueDate_ShouldSucceed()
    {
        var lineItem = InvoiceLineItem.Create("Test", 1, 100m).Value!;
        var pastDue = DateTimeOffset.UtcNow.AddDays(-10);

        var result = Invoice.Create(
            InvoiceNumber.Create(2026, 1),
            "Acme Corp",
            "billing@acme.com",
            null,
            pastDue.AddDays(-30),
            pastDue,
            0,
            "USD",
            null,
            new List<InvoiceLineItem> { lineItem },
            Guid.NewGuid());

        var invoice = result.Value!;
        invoice.MarkAsSent();

        var overdueResult = invoice.MarkAsOverdue();

        overdueResult.IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Overdue);
    }

    [Fact]
    public void MarkAsOverdue_FromDraft_ShouldFail()
    {
        var invoice = CreateValidInvoice();

        var result = invoice.MarkAsOverdue();

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void FullLifecycle_DraftToSentToPaid_ShouldSucceed()
    {
        var invoice = CreateValidInvoice();

        invoice.Status.ShouldBe(InvoiceStatus.Draft);
        invoice.MarkAsSent().IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Sent);
        invoice.MarkAsPaid().IsSuccess.ShouldBeTrue();
        invoice.Status.ShouldBe(InvoiceStatus.Paid);
        invoice.IsTerminal.ShouldBeTrue();
    }

    private static Invoice CreateValidInvoice()
    {
        var lineItem = InvoiceLineItem.Create("Test service", 5, 200.00m).Value!;
        return Invoice.Create(
            InvoiceNumber.Create(2026, 1),
            "Acme Corp",
            "billing@acme.com",
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

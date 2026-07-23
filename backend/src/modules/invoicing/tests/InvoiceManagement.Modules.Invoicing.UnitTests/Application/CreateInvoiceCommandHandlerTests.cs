using InvoiceManagement.Modules.Invoicing.Application.Commands.CreateInvoice;
using InvoiceManagement.Modules.Invoicing.Domain.Entities;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace InvoiceManagement.Modules.Invoicing.UnitTests.Application;

public class CreateInvoiceCommandHandlerTests
{
    private readonly IInvoiceRepository _repository = Substitute.For<IInvoiceRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<CreateInvoiceCommandHandler> _logger = Substitute.For<ILogger<CreateInvoiceCommandHandler>>();
    private readonly CreateInvoiceCommandHandler _handler;

    public CreateInvoiceCommandHandlerTests()
    {
        _handler = new CreateInvoiceCommandHandler(_repository, _unitOfWork, _logger);
        _repository.GetNextSequenceNumberAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(0);
        _repository.GetByInvoiceNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Invoice?)null);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateInvoice()
    {
        var command = new CreateInvoiceCommand(
            "Acme Corp",
            "billing@acme.com",
            "123 Main St",
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            8.5m,
            "USD",
            "Net 30",
            new List<CreateInvoiceLineItemRequest>
            {
                new("Consulting", 10, 150.00m)
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CustomerName.ShouldBe("Acme Corp");
        result.Value.TotalAmount.ShouldBe(1627.50m);
        result.Value.Status.ShouldBe("Draft");

        await _repository.Received(1).AddAsync(Arg.Any<Invoice>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvoiceNumberCollision_ShouldFail()
    {
        _repository.GetByInvoiceNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(CreateValidInvoice());

        var command = new CreateInvoiceCommand(
            "Acme Corp",
            "billing@acme.com",
            null,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            0,
            "USD",
            null,
            new List<CreateInvoiceLineItemRequest>
            {
                new("Test", 1, 100m)
            });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_ZeroLineItems_ShouldFail()
    {
        var command = new CreateInvoiceCommand(
            "Acme Corp",
            "billing@acme.com",
            null,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            0,
            "USD",
            null,
            new List<CreateInvoiceLineItemRequest>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }

    private static Invoice CreateValidInvoice()
    {
        var lineItem = InvoiceLineItem.Create("Test", 1, 100m).Value!;
        return Invoice.Create(
            InvoiceManagement.Modules.Invoicing.Domain.ValueObjects.InvoiceNumber.Create(2026, 1),
            "Test",
            "test@test.com",
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30),
            0,
            "USD",
            null,
            new List<InvoiceLineItem> { lineItem },
            Guid.NewGuid()).Value!;
    }
}

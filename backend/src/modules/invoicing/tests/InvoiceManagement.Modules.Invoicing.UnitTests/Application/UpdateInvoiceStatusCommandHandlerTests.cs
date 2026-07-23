using InvoiceManagement.Modules.Invoicing.Application.Commands.UpdateInvoiceStatus;
using InvoiceManagement.Modules.Invoicing.Domain.Entities;
using InvoiceManagement.Modules.Invoicing.Domain.Enums;
using InvoiceManagement.Modules.Invoicing.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace InvoiceManagement.Modules.Invoicing.UnitTests.Application;

public class UpdateInvoiceStatusCommandHandlerTests
{
    private readonly IInvoiceRepository _repository = Substitute.For<IInvoiceRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<UpdateInvoiceStatusCommandHandler> _logger = Substitute.For<ILogger<UpdateInvoiceStatusCommandHandler>>();
    private readonly UpdateInvoiceStatusCommandHandler _handler;

    public UpdateInvoiceStatusCommandHandlerTests()
    {
        _handler = new UpdateInvoiceStatusCommandHandler(_repository, _unitOfWork, _logger);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    [Fact]
    public async Task Handle_MarkAsSent_FromDraft_ShouldSucceed()
    {
        var invoice = CreateDraftInvoice();
        _repository.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);

        var result = await _handler.Handle(
            new UpdateInvoiceStatusCommand(invoice.Id, InvoiceManagement.Modules.Invoicing.Domain.Enums.InvoiceStatus.Sent),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Status.ShouldBe("Sent");
        _repository.Received(1).Update(invoice);
    }

    [Fact]
    public async Task Handle_InvoiceNotFound_ShouldFail()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Invoice?)null);

        var result = await _handler.Handle(
            new UpdateInvoiceStatusCommand(Guid.NewGuid(), InvoiceManagement.Modules.Invoicing.Domain.Enums.InvoiceStatus.Sent),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_InvalidTransition_ShouldFail()
    {
        var invoice = CreateDraftInvoice();
        _repository.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);

        var result = await _handler.Handle(
            new UpdateInvoiceStatusCommand(invoice.Id, InvoiceManagement.Modules.Invoicing.Domain.Enums.InvoiceStatus.Paid),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }

    private static Invoice CreateDraftInvoice()
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

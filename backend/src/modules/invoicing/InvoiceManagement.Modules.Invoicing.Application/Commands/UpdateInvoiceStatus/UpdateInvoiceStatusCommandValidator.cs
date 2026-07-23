using FluentValidation;
using InvoiceManagement.Modules.Invoicing.Domain.Enums;

namespace InvoiceManagement.Modules.Invoicing.Application.Commands.UpdateInvoiceStatus;

public sealed class UpdateInvoiceStatusCommandValidator : AbstractValidator<UpdateInvoiceStatusCommand>
{
    public UpdateInvoiceStatusCommandValidator()
    {
        RuleFor(x => x.InvoiceId)
            .NotEmpty().WithMessage("Invoice ID is required.");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid status value.")
            .Must(s => s is InvoiceStatus.Sent or InvoiceStatus.Paid or InvoiceStatus.Cancelled)
            .WithMessage("Only Sent, Paid, or Cancelled statuses can be set manually.");
    }
}

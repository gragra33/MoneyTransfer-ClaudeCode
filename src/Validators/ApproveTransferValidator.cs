using FluentValidation;
using MoneyTransfer.Models;

namespace MoneyTransfer.Validators;

/// <summary>Command for approving a money transfer.</summary>
/// <param name="EmployeeId">The identifier of the employee granting approval.</param>
public sealed record ApproveTransferCommand(EmployeeId EmployeeId);

/// <summary>Validates an <see cref="ApproveTransferCommand"/> before any domain logic runs.</summary>
public sealed class ApproveTransferValidator : AbstractValidator<ApproveTransferCommand>
{
    /// <summary>Initializes a new <see cref="ApproveTransferValidator"/>.</summary>
    public ApproveTransferValidator() =>
        RuleFor(x => x.EmployeeId.Value)
            .NotEmpty()
            .WithMessage("Employee ID must not be empty.");
}

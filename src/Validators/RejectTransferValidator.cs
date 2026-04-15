using FluentValidation;
using MoneyTransfer.Models;

namespace MoneyTransfer.Validators;

/// <summary>Command for rejecting a money transfer.</summary>
/// <param name="EmployeeId">The identifier of the employee performing the rejection.</param>
public sealed record RejectTransferCommand(EmployeeId EmployeeId);

/// <summary>Validates a <see cref="RejectTransferCommand"/> before any domain logic runs.</summary>
public sealed class RejectTransferValidator : AbstractValidator<RejectTransferCommand>
{
    /// <summary>Initializes a new <see cref="RejectTransferValidator"/>.</summary>
    public RejectTransferValidator() =>
        RuleFor(x => x.EmployeeId.Value)
            .NotEmpty()
            .WithMessage("Employee ID must not be empty.");
}

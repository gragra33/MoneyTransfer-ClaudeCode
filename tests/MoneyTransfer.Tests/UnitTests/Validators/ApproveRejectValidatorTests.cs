using FluentValidation.TestHelper;
using MoneyTransfer.Models;
using MoneyTransfer.Tests.Fixtures;
using MoneyTransfer.Validators;
using Shouldly;

namespace MoneyTransfer.Tests.UnitTests.Validators;

/// <summary>Unit tests for <see cref="ApproveTransferValidator"/> and <see cref="RejectTransferValidator"/>.</summary>
[Trait("Category", "Unit")]
public class ApproveRejectValidatorTests
{
    private readonly ApproveTransferValidator _approveSut = new();
    private readonly RejectTransferValidator  _rejectSut  = new();

    #region ApproveTransferValidator

    [Fact]
    public void ApproveValidator_ValidEmployeeId_Passes()
    {
        // Arrange
        var command = TestData.ApproveCommand(TestData.EmployeeA);

        // Act
        var result = _approveSut.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ApproveValidator_EmptyEmployeeId_Fails()
    {
        // Arrange
        var command = new ApproveTransferCommand(new EmployeeId(Guid.Empty));

        // Act
        var result = _approveSut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EmployeeId.Value)
              .WithErrorMessage("Employee ID must not be empty.");
    }

    #endregion

    #region RejectTransferValidator

    [Fact]
    public void RejectValidator_ValidEmployeeId_Passes()
    {
        // Arrange
        var command = TestData.RejectCommand(TestData.EmployeeA);

        // Act
        var result = _rejectSut.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void RejectValidator_EmptyEmployeeId_Fails()
    {
        // Arrange
        var command = new RejectTransferCommand(new EmployeeId(Guid.Empty));

        // Act
        var result = _rejectSut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EmployeeId.Value)
              .WithErrorMessage("Employee ID must not be empty.");
    }

    #endregion
}

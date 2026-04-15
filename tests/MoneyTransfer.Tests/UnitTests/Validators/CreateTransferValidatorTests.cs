using FluentValidation;
using FluentValidation.TestHelper;
using MoneyTransfer.Models;
using MoneyTransfer.Tests.Fixtures;
using MoneyTransfer.Validators;
using Shouldly;

namespace MoneyTransfer.Tests.UnitTests.Validators;

/// <summary>Unit tests for <see cref="CreateTransferValidator"/>.</summary>
[Trait("Category", "Unit")]
public class CreateTransferValidatorTests
{
    private readonly CreateTransferValidator _sut;

    public CreateTransferValidatorTests()
    {
        // Use a fixed "now" so expiry comparisons are deterministic
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _sut = new CreateTransferValidator(fakeTime);
    }

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        // Arrange
        var command = TestData.ValidCreateCommand();

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ZeroAmount_Fails()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(amount: 0m);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Amount)
              .WithErrorMessage("Amount must be a positive value.");
    }

    [Fact]
    public void Validate_NegativeAmount_Fails()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(amount: -1m);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData("XXX")]
    [InlineData("INVALID")]
    [InlineData("")]
    [InlineData("US")]
    public void Validate_InvalidCurrency_Fails(string currency)
    {
        // Arrange
        var command = TestData.ValidCreateCommand(currency: currency);

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        var errors = result.ShouldHaveValidationErrorFor(x => x.Currency);
        errors.ShouldContain(e => e.ErrorMessage.Contains("ISO-4217"));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("usd")] // case-insensitive
    public void Validate_ValidCurrency_Passes(string currency)
    {
        // Arrange
        var command = TestData.ValidCreateCommand(currency: currency);

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue($"Expected '{currency}' to be valid but got: {result}");
    }

    [Fact]
    public void Validate_SameSourceAndDestination_Fails()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(
            source: TestData.SourceAccount,
            dest:   TestData.SourceAccount); // same

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SourceAccountId)
              .WithErrorMessage("Source and destination accounts must be different.");
    }

    [Fact]
    public void Validate_EmptySourceAccountId_Fails()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(source: new AccountId(Guid.Empty));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SourceAccountId.Value);
    }

    [Fact]
    public void Validate_EmptyDestinationAccountId_Fails()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(dest: new AccountId(Guid.Empty));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DestinationAccountId.Value);
    }

    [Fact]
    public void Validate_PastExpiresAt_Fails()
    {
        // Arrange — FakeTimeProvider is fixed to 2024-01-01; use a date before that
        var command = TestData.ValidCreateCommand(expiresAt: new DateTimeOffset(2023, 12, 31, 0, 0, 0, TimeSpan.Zero));

        // Act
        var result = _sut.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExpiresAt)
              .WithErrorMessage("Expiration timestamp must be in the future.");
    }

    [Fact]
    public void Validate_FutureExpiresAt_Passes()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(expiresAt: new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero));

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}

/// <summary>Minimal <see cref="TimeProvider"/> with a fixed current time for deterministic tests.</summary>
internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

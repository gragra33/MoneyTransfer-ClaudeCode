using FluentValidation;
using Moq;
using MoneyTransfer.Common;
using MoneyTransfer.Models;
using MoneyTransfer.Services;
using MoneyTransfer.Tests.Fixtures;
using MoneyTransfer.Validators;
using Shouldly;

namespace MoneyTransfer.Tests.UnitTests.Services;

/// <summary>Unit tests for <see cref="TransferService"/> — all validators and TimeProvider are mocked.</summary>
[Trait("Category", "Unit")]
public class TransferServiceTests
{
    private readonly Mock<TimeProvider>                        _timeMock;
    private readonly Mock<IValidator<CreateTransferCommand>>  _createValidatorMock;
    private readonly Mock<IValidator<ApproveTransferCommand>> _approveValidatorMock;
    private readonly Mock<IValidator<RejectTransferCommand>>  _rejectValidatorMock;
    private readonly TransferService                          _sut;

    public TransferServiceTests()
    {
        _timeMock             = new Mock<TimeProvider>();
        _createValidatorMock  = new Mock<IValidator<CreateTransferCommand>>();
        _approveValidatorMock = new Mock<IValidator<ApproveTransferCommand>>();
        _rejectValidatorMock  = new Mock<IValidator<RejectTransferCommand>>();

        // Default "now" is always before FutureExpiry
        _timeMock.Setup(t => t.GetUtcNow())
                 .Returns(new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));

        _sut = new TransferService(
            _timeMock.Object,
            _createValidatorMock.Object,
            _approveValidatorMock.Object,
            _rejectValidatorMock.Object);
    }

    #region Create

    [Fact]
    public void Create_WhenValidationPasses_ReturnsSuccessWithTransfer()
    {
        // Arrange
        var command = TestData.ValidCreateCommand();
        SetupValidationPass(_createValidatorMock, command);

        // Act
        var result = _sut.Create(command);

        // Assert
        result.ShouldSucceed();
        result.Value.ShouldNotBeNull();
        result.Value!.SourceAccountId.ShouldBe(command.SourceAccountId);
        result.Value.DestinationAccountId.ShouldBe(command.DestinationAccountId);
        result.Value.Amount.ShouldBe(command.Amount);
        result.Value.Currency.ShouldBe(command.Currency);
    }

    [Fact]
    public void Create_WhenValidationFails_ReturnsFailResultWithErrors()
    {
        // Arrange
        var command = TestData.ValidCreateCommand();
        SetupValidationFail(_createValidatorMock, command, "Amount must be a positive value.");

        // Act
        var result = _sut.Create(command);

        // Assert
        result.ShouldFail("Amount must be a positive value.");
    }

    [Fact]
    public void Create_WithRequiresApproval_TransferStartsPending()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(requiresApproval: true);
        SetupValidationPass(_createValidatorMock, command);

        // Act
        var result = _sut.Create(command);

        // Assert
        result.ShouldSucceed();
        result.Value!.Status.ShouldBe(TransferStatus.Pending);
    }

    [Fact]
    public void Create_WithoutRequiresApproval_TransferStartsApproved()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(requiresApproval: false);
        SetupValidationPass(_createValidatorMock, command);

        // Act
        var result = _sut.Create(command);

        // Assert
        result.ShouldSucceed();
        result.Value!.Status.ShouldBe(TransferStatus.Approved);
    }

    #endregion

    #region Approve

    [Fact]
    public void Approve_WhenValidationPassesAndPending_Succeeds()
    {
        // Arrange
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);
        var command  = TestData.ApproveCommand();
        SetupValidationPass(_approveValidatorMock, command);

        // Act
        var result = _sut.Approve(transfer, command);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.PartlyApproved);
    }

    [Fact]
    public void Approve_WhenValidationFails_ReturnsFailResult()
    {
        // Arrange
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);
        var command  = TestData.ApproveCommand();
        SetupValidationFail(_approveValidatorMock, command, EmptyEmployeeError);

        // Act
        var result = _sut.Approve(transfer, command);

        // Assert
        result.ShouldFail(EmptyEmployeeError);
    }

    [Fact]
    public void Approve_WhenTransferAlreadyExpired_ChecksExpiryFirst()
    {
        // Arrange — time is past the expiry
        var pastExpiry = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _timeMock.Setup(t => t.GetUtcNow()).Returns(pastExpiry.AddDays(1));

        var transfer = TestHelpers.BuildTransfer(requiresApproval: true, expiresAt: pastExpiry);
        var command  = TestData.ApproveCommand();
        SetupValidationPass(_approveValidatorMock, command);

        // Act
        var result = _sut.Approve(transfer, command);

        // Assert — CheckExpiry ran, transfer is Expired, Approve fails
        transfer.Status.ShouldBe(TransferStatus.Expired);
        result.ShouldFail();
    }

    #endregion

    #region Execute

    [Fact]
    public void Execute_WhenApprovedAndNotExpired_Succeeds()
    {
        // Arrange — time is well before FutureExpiry
        var transfer = TestHelpers.BuildApprovedTransfer();

        // Act
        var result = _sut.Execute(transfer);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Executed);
    }

    [Fact]
    public void Execute_WhenApprovedButExpiredByTime_Fails()
    {
        // Arrange — set "now" past expiry
        var pastExpiry = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _timeMock.Setup(t => t.GetUtcNow()).Returns(pastExpiry.AddDays(1));

        var transfer = TestHelpers.BuildApprovedTransfer(expiresAt: pastExpiry);

        // Act
        var result = _sut.Execute(transfer);

        // Assert
        result.ShouldFail("expired");
    }

    #endregion

    #region Reject

    [Fact]
    public void Reject_WhenValidationPassesAndPending_Succeeds()
    {
        // Arrange
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);
        var command  = TestData.RejectCommand();
        SetupValidationPass(_rejectValidatorMock, command);

        // Act
        var result = _sut.Reject(transfer, command);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Rejected);
    }

    [Fact]
    public void Reject_WhenValidationFails_ReturnsFailResult()
    {
        // Arrange
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);
        var command  = TestData.RejectCommand();
        SetupValidationFail(_rejectValidatorMock, command, EmptyEmployeeError);

        // Act
        var result = _sut.Reject(transfer, command);

        // Assert
        result.ShouldFail(EmptyEmployeeError);
        transfer.Status.ShouldBe(TransferStatus.Pending);
    }

    #endregion

    #region CheckExpiry

    [Fact]
    public void CheckExpiry_WhenExpired_TransferBecomesExpired()
    {
        // Arrange — move "now" past expiry
        _timeMock.Setup(t => t.GetUtcNow()).Returns(TestData.FutureExpiry.AddDays(1));
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);

        // Act
        var result = _sut.CheckExpiry(transfer);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Expired);
    }

    [Fact]
    public void CheckExpiry_WhenNotExpired_StatusUnchanged()
    {
        // Arrange — default "now" is 2024-06-01, well before FutureExpiry (2099)
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);

        // Act
        var result = _sut.CheckExpiry(transfer);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Pending);
    }

    #endregion

    #region Setup helpers

    private const string EmptyEmployeeError = "Employee ID must not be empty.";

    private static void SetupValidationPass<T>(Mock<IValidator<T>> mock, T command) where T : class =>
        mock.Setup(v => v.Validate(command))
            .Returns(new FluentValidation.Results.ValidationResult());

    private static void SetupValidationFail<T>(Mock<IValidator<T>> mock, T command, string errorMessage) where T : class =>
        mock.Setup(v => v.Validate(command))
            .Returns(new FluentValidation.Results.ValidationResult(
            [
                new FluentValidation.Results.ValidationFailure("Field", errorMessage)
            ]));

    #endregion
}

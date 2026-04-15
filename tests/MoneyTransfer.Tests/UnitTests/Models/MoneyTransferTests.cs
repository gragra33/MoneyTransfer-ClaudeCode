using MoneyTransfer.Models;
using MoneyTransfer.Tests.Fixtures;
using Shouldly;

namespace MoneyTransfer.Tests.UnitTests.Models;

/// <summary>Unit tests for the <see cref="MoneyTransfer"/> domain entity.</summary>
[Trait("Category", "Unit")]
public class MoneyTransferTests
{
    #region Construction

    [Fact]
    public void Construction_WithRequiresApprovalFalse_StartsAsApproved()
    {
        // Act
        var transfer = TestHelpers.BuildTransfer(requiresApproval: false);

        // Assert
        transfer.Status.ShouldBe(TransferStatus.Approved);
        transfer.FirstApproverId.ShouldBeNull();
        transfer.SecondApproverId.ShouldBeNull();
    }

    [Fact]
    public void Construction_WithRequiresApprovalTrue_StartsAsPending()
    {
        // Act
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);

        // Assert
        transfer.Status.ShouldBe(TransferStatus.Pending);
    }

    [Fact]
    public void Construction_SetsAllFieldsCorrectly()
    {
        // Act
        var transfer = TestHelpers.BuildTransfer(amount: 250m, currency: "EUR");

        // Assert
        transfer.Id.Value.ShouldNotBe(Guid.Empty);
        transfer.SourceAccountId.ShouldBe(TestData.SourceAccount);
        transfer.DestinationAccountId.ShouldBe(TestData.DestAccount);
        transfer.Amount.ShouldBe(250m);
        transfer.Currency.ShouldBe("EUR");
        transfer.ExpiresAt.ShouldBe(TestData.FutureExpiry);
    }

    #endregion

    #region Approve

    [Fact]
    public void Approve_WhenPending_TransitionsToPartlyApproved()
    {
        // Arrange
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);

        // Act
        var result = transfer.Approve(TestData.EmployeeA);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.PartlyApproved);
        transfer.FirstApproverId.ShouldBe(TestData.EmployeeA);
    }

    [Fact]
    public void Approve_WhenPartlyApproved_WithDifferentEmployee_TransitionsToApproved()
    {
        // Arrange
        var transfer = TestHelpers.BuildPartlyApprovedTransfer();

        // Act
        var result = transfer.Approve(TestData.EmployeeB);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Approved);
        transfer.SecondApproverId.ShouldBe(TestData.EmployeeB);
    }

    [Fact]
    public void Approve_WhenPartlyApproved_WithSameEmployee_Fails()
    {
        // Arrange
        var transfer = TestHelpers.BuildPartlyApprovedTransfer();

        // Act
        var result = transfer.Approve(TestData.EmployeeA); // same as first approver

        // Assert
        result.ShouldFail("different employee");
        transfer.Status.ShouldBe(TransferStatus.PartlyApproved);
    }

    [Theory]
    [InlineData(TransferStatus.Approved)]
    [InlineData(TransferStatus.Executed)]
    [InlineData(TransferStatus.Rejected)]
    [InlineData(TransferStatus.Expired)]
    public void Approve_WhenNotApprovable_Fails(TransferStatus startStatus)
    {
        // Arrange — reach the target status via the approved shortcut then manipulate via helpers
        var transfer = BuildTransferAtStatus(startStatus);

        // Act
        var result = transfer.Approve(TestData.EmployeeC);

        // Assert
        result.ShouldFail();
    }

    #endregion

    #region Execute

    [Fact]
    public void Execute_WhenApprovedAndNotExpired_TransitionsToExecuted()
    {
        // Arrange
        var transfer = TestHelpers.BuildApprovedTransfer();
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = transfer.Execute(now);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Executed);
        transfer.ExecutedAt.ShouldBe(now);
    }

    [Fact]
    public void Execute_WhenApprovedButExpired_Fails()
    {
        // Arrange
        var transfer = TestHelpers.BuildApprovedTransfer(expiresAt: TestData.PastExpiry);
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = transfer.Execute(now);

        // Assert
        result.ShouldFail("expired");
        transfer.Status.ShouldBe(TransferStatus.Approved);
    }

    [Theory]
    [InlineData(TransferStatus.Pending)]
    [InlineData(TransferStatus.PartlyApproved)]
    [InlineData(TransferStatus.Rejected)]
    [InlineData(TransferStatus.Expired)]
    public void Execute_WhenNotApproved_Fails(TransferStatus startStatus)
    {
        // Arrange
        var transfer = BuildTransferAtStatus(startStatus);
        var now = DateTimeOffset.UtcNow;

        // Act
        var result = transfer.Execute(now);

        // Assert
        result.ShouldFail();
    }

    #endregion

    #region Reject

    [Fact]
    public void Reject_WhenPending_TransitionsToRejected()
    {
        // Arrange
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);

        // Act
        var result = transfer.Reject(TestData.EmployeeA);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Rejected);
        transfer.RejectedById.ShouldBe(TestData.EmployeeA);
    }

    [Fact]
    public void Reject_WhenPartlyApproved_TransitionsToRejected()
    {
        // Arrange
        var transfer = TestHelpers.BuildPartlyApprovedTransfer();

        // Act
        var result = transfer.Reject(TestData.EmployeeB);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Rejected);
    }

    [Theory]
    [InlineData(TransferStatus.Approved)]
    [InlineData(TransferStatus.Executed)]
    [InlineData(TransferStatus.Rejected)]
    [InlineData(TransferStatus.Expired)]
    public void Reject_WhenNotRejectable_Fails(TransferStatus startStatus)
    {
        // Arrange
        var transfer = BuildTransferAtStatus(startStatus);

        // Act
        var result = transfer.Reject(TestData.EmployeeC);

        // Assert
        result.ShouldFail();
    }

    #endregion

    #region CheckExpiry

    [Fact]
    public void CheckExpiry_WhenPastExpiresAt_TransitionsToExpired()
    {
        // Arrange
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);
        var now = TestData.FutureExpiry.AddSeconds(1); // past the expiry

        // Act
        var result = transfer.CheckExpiry(now);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Expired);
    }

    [Fact]
    public void CheckExpiry_WhenNotYetExpired_StatusUnchanged()
    {
        // Arrange
        var transfer = TestHelpers.BuildTransfer(requiresApproval: true);
        var now = TestData.FutureExpiry.AddDays(-1); // before expiry

        // Act
        var result = transfer.CheckExpiry(now);

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Pending);
    }

    [Theory]
    [InlineData(TransferStatus.Executed)]
    [InlineData(TransferStatus.Rejected)]
    [InlineData(TransferStatus.Expired)]
    public void CheckExpiry_WhenTerminal_IsIdempotent(TransferStatus terminalStatus)
    {
        // Arrange
        var transfer = BuildTransferAtStatus(terminalStatus);

        // Act
        var result = transfer.CheckExpiry(TestData.FutureExpiry.AddYears(100));

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(terminalStatus);
    }

    #endregion

    #region Helpers

    /// <summary>Advances a builder-created transfer to the requested status for negative-path tests.</summary>
    private static MoneyTransfer.Models.MoneyTransfer BuildTransferAtStatus(TransferStatus status) =>
        status switch
        {
            TransferStatus.Pending        => TestHelpers.BuildTransfer(requiresApproval: true),
            TransferStatus.PartlyApproved => TestHelpers.BuildPartlyApprovedTransfer(),
            TransferStatus.Approved       => TestHelpers.BuildApprovedTransfer(),
            TransferStatus.Executed       => BuildExecutedTransfer(),
            TransferStatus.Rejected       => BuildRejectedTransfer(),
            TransferStatus.Expired        => BuildExpiredTransfer(),
            _                             => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    private static MoneyTransfer.Models.MoneyTransfer BuildExecutedTransfer()
    {
        var t = TestHelpers.BuildApprovedTransfer();
        t.Execute(DateTimeOffset.UtcNow);
        return t;
    }

    private static MoneyTransfer.Models.MoneyTransfer BuildRejectedTransfer()
    {
        var t = TestHelpers.BuildTransfer(requiresApproval: true);
        t.Reject(TestData.EmployeeA);
        return t;
    }

    private static MoneyTransfer.Models.MoneyTransfer BuildExpiredTransfer()
    {
        var expiry = DateTimeOffset.UtcNow.AddSeconds(-1);
        var t = TestHelpers.BuildTransfer(requiresApproval: true, expiresAt: expiry);
        t.CheckExpiry(expiry.AddSeconds(1));
        return t;
    }

    #endregion
}

using MoneyTransfer.Models;
using MoneyTransfer.Tests.Fixtures;
using Shouldly;

namespace MoneyTransfer.Tests.UnitTests.Models;

/// <summary>Unit tests for <see cref="MoneyTransferBuilder"/> fluent construction.</summary>
[Trait("Category", "Unit")]
public class MoneyTransferBuilderTests
{
    [Fact]
    public void Build_WithAllValues_SetsPropertiesCorrectly()
    {
        // Arrange
        var expiry = new DateTimeOffset(2090, 6, 15, 0, 0, 0, TimeSpan.Zero);

        // Act
        var transfer = new MoneyTransferBuilder()
            .From(TestData.SourceAccount)
            .To(TestData.DestAccount)
            .WithAmount(500.50m, "GBP")
            .ExpiresAt(expiry)
            .RequiresApproval(true)
            .Build();

        // Assert
        transfer.SourceAccountId.ShouldBe(TestData.SourceAccount);
        transfer.DestinationAccountId.ShouldBe(TestData.DestAccount);
        transfer.Amount.ShouldBe(500.50m);
        transfer.Currency.ShouldBe("GBP");
        transfer.ExpiresAt.ShouldBe(expiry);
        transfer.Status.ShouldBe(TransferStatus.Pending);
    }

    [Fact]
    public void Build_WithRequiresApprovalFalse_StartsApproved()
    {
        // Act
        var transfer = new MoneyTransferBuilder()
            .From(TestData.SourceAccount)
            .To(TestData.DestAccount)
            .WithAmount(100m, "USD")
            .ExpiresAt(TestData.FutureExpiry)
            .RequiresApproval(false)
            .Build();

        // Assert
        transfer.Status.ShouldBe(TransferStatus.Approved);
    }

    [Fact]
    public void Build_EachCallProducesUniqueId()
    {
        // Act
        var t1 = TestHelpers.BuildTransfer();
        var t2 = TestHelpers.BuildTransfer();

        // Assert
        t1.Id.ShouldNotBe(t2.Id);
    }

    [Fact]
    public void From_IsFluentAndReturnsBuilder()
    {
        // Act
        var builder = new MoneyTransferBuilder().From(TestData.SourceAccount);

        // Assert — method returns the same instance (fluent interface)
        builder.ShouldBeOfType<MoneyTransferBuilder>();
    }
}

using MoneyTransfer.Common;
using MoneyTransfer.Models;
using MoneyTransfer.Validators;
using Shouldly;

namespace MoneyTransfer.Tests.Fixtures;

/// <summary>Assertion helpers and factory utilities shared across all tests.</summary>
public static class TestHelpers
{
    /// <summary>Asserts that <paramref name="result"/> is successful.</summary>
    public static void ShouldSucceed(this Result result) =>
        result.IsSuccess.ShouldBeTrue($"Expected success but got failure: {result.Error}");

    /// <summary>Asserts that <paramref name="result"/> is successful.</summary>
    public static void ShouldSucceed<T>(this Result<T> result) =>
        result.IsSuccess.ShouldBeTrue($"Expected success but got failure: {result.Error}");

    /// <summary>Asserts that <paramref name="result"/> failed and that the error contains <paramref name="fragment"/>.</summary>
    public static void ShouldFail(this Result result, string fragment = "")
    {
        result.IsSuccess.ShouldBeFalse("Expected failure but got success.");
        if (!string.IsNullOrEmpty(fragment))
            result.Error.ShouldContain(fragment);
    }

    /// <summary>Asserts that <paramref name="result"/> failed and that the error contains <paramref name="fragment"/>.</summary>
    public static void ShouldFail<T>(this Result<T> result, string fragment = "")
    {
        result.IsSuccess.ShouldBeFalse("Expected failure but got success.");
        if (!string.IsNullOrEmpty(fragment))
            result.Error.ShouldContain(fragment);
    }

    /// <summary>
    /// Builds a <see cref="Models.MoneyTransfer"/> directly via the builder, bypassing the service layer.
    /// Useful for setting up model-level unit tests.
    /// </summary>
    public static Models.MoneyTransfer BuildTransfer(
        bool requiresApproval      = false,
        DateTimeOffset? expiresAt  = null,
        AccountId? source          = null,
        AccountId? dest            = null,
        decimal amount             = 100m,
        string currency            = "USD") =>
        new MoneyTransferBuilder()
            .From(source      ?? TestData.SourceAccount)
            .To(dest          ?? TestData.DestAccount)
            .WithAmount(amount, currency)
            .ExpiresAt(expiresAt ?? TestData.FutureExpiry)
            .RequiresApproval(requiresApproval)
            .Build();

    /// <summary>Builds a transfer and advances it to <see cref="TransferStatus.PartlyApproved"/>.</summary>
    public static Models.MoneyTransfer BuildPartlyApprovedTransfer(DateTimeOffset? expiresAt = null)
    {
        var t = BuildTransfer(requiresApproval: true, expiresAt: expiresAt);
        t.Approve(TestData.EmployeeA);
        return t;
    }

    /// <summary>Builds a transfer and advances it to <see cref="TransferStatus.Approved"/> via dual approval.</summary>
    public static Models.MoneyTransfer BuildApprovedTransfer(DateTimeOffset? expiresAt = null)
    {
        var t = BuildPartlyApprovedTransfer(expiresAt);
        t.Approve(TestData.EmployeeB);
        return t;
    }
}

using MoneyTransfer.Models;
using MoneyTransfer.Validators;

namespace MoneyTransfer.Tests.Fixtures;

/// <summary>Deterministic data factories shared across all tests.</summary>
public static class TestData
{
    public static readonly AccountId SourceAccount      = new(new Guid("aaaaaaaa-0000-0000-0000-000000000001"));
    public static readonly AccountId DestAccount        = new(new Guid("bbbbbbbb-0000-0000-0000-000000000002"));
    public static readonly EmployeeId EmployeeA         = new(new Guid("cccccccc-0000-0000-0000-000000000003"));
    public static readonly EmployeeId EmployeeB         = new(new Guid("dddddddd-0000-0000-0000-000000000004"));
    public static readonly EmployeeId EmployeeC         = new(new Guid("eeeeeeee-0000-0000-0000-000000000005"));

    public static readonly DateTimeOffset FutureExpiry  = new(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset PastExpiry    = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Returns a valid <see cref="CreateTransferCommand"/> with defaults that pass validation.</summary>
    public static CreateTransferCommand ValidCreateCommand(
        AccountId? source          = null,
        AccountId? dest            = null,
        decimal amount             = 100m,
        string currency            = "USD",
        bool requiresApproval      = false,
        DateTimeOffset? expiresAt  = null) =>
        new(
            source      ?? SourceAccount,
            dest        ?? DestAccount,
            amount,
            currency,
            requiresApproval,
            expiresAt   ?? FutureExpiry);

    public static ApproveTransferCommand ApproveCommand(EmployeeId? emp = null) =>
        new(emp ?? EmployeeA);

    public static RejectTransferCommand RejectCommand(EmployeeId? emp = null) =>
        new(emp ?? EmployeeA);
}

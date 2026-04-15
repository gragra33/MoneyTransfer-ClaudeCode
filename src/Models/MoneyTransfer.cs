using MoneyTransfer.Common;

namespace MoneyTransfer.Models;

/// <summary>
/// Domain entity representing a money transfer between two accounts.
/// Constructed exclusively via <see cref="MoneyTransferBuilder"/>.
/// </summary>
public sealed class MoneyTransfer
{
    /// <summary>Initializes a new <see cref="MoneyTransfer"/>.</summary>
    internal MoneyTransfer(
        AccountId sourceAccountId,
        AccountId destinationAccountId,
        decimal amount,
        string currency,
        bool requiresApproval,
        DateTimeOffset expiresAt)
    {
        Id = TransferId.New();
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        Amount = amount;
        Currency = currency;
        ExpiresAt = expiresAt;
        Status = requiresApproval ? TransferStatus.Pending : TransferStatus.Approved;
    }

    /// <summary>Gets the unique public identifier of this transfer.</summary>
    public TransferId Id { get; }

    /// <summary>Gets the identifier of the source account.</summary>
    public AccountId SourceAccountId { get; }

    /// <summary>Gets the identifier of the destination account.</summary>
    public AccountId DestinationAccountId { get; }

    /// <summary>Gets the positive monetary amount to transfer.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the ISO-4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Gets the UTC timestamp after which this transfer expires.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Gets the current lifecycle status of the transfer.</summary>
    public TransferStatus Status { get; private set; }

    /// <summary>Gets the identifier of the employee who gave the first approval, if any.</summary>
    public EmployeeId? FirstApproverId { get; private set; }

    /// <summary>Gets the identifier of the employee who gave the second approval, if any.</summary>
    public EmployeeId? SecondApproverId { get; private set; }

    /// <summary>Gets the identifier of the employee who rejected the transfer, if any.</summary>
    public EmployeeId? RejectedById { get; private set; }

    /// <summary>Gets the UTC timestamp at which the transfer was executed, if applicable.</summary>
    public DateTimeOffset? ExecutedAt { get; private set; }

    /// <summary>
    /// Approves the transfer.
    /// <list type="bullet">
    ///   <item><see cref="TransferStatus.Pending"/> → <see cref="TransferStatus.PartlyApproved"/> (stores first approver).</item>
    ///   <item><see cref="TransferStatus.PartlyApproved"/> + a different employee → <see cref="TransferStatus.Approved"/> (stores second approver).</item>
    /// </list>
    /// </summary>
    /// <param name="employeeId">The identifier of the approving employee.</param>
    /// <returns>
    /// <see cref="Result.Ok()"/> on success; <see cref="Result.Fail(string)"/> if the current status
    /// does not permit approval or the employee already gave the first approval.
    /// </returns>
    public Result Approve(EmployeeId employeeId)
    {
        if (Status == TransferStatus.Pending)
        {
            FirstApproverId = employeeId;
            Status = TransferStatus.PartlyApproved;
            return Result.Ok();
        }

        if (Status == TransferStatus.PartlyApproved && employeeId != FirstApproverId)
        {
            SecondApproverId = employeeId;
            Status = TransferStatus.Approved;
            return Result.Ok();
        }

        return Result.Fail(
            Status == TransferStatus.PartlyApproved
                ? "The second approver must be a different employee from the first."
                : $"Cannot approve a transfer with status '{Status}'.");
    }

    /// <summary>
    /// Executes the transfer. Requires <see cref="TransferStatus.Approved"/> status and
    /// <paramref name="now"/> to be before <see cref="ExpiresAt"/>.
    /// </summary>
    /// <param name="now">The current UTC timestamp, typically from an injected <see cref="TimeProvider"/>.</param>
    /// <returns>
    /// <see cref="Result.Ok()"/> on success; <see cref="Result.Fail(string)"/> if the status is not
    /// <see cref="TransferStatus.Approved"/> or the transfer has expired.
    /// </returns>
    public Result Execute(DateTimeOffset now)
    {
        if (Status != TransferStatus.Approved)
            return Result.Fail($"Cannot execute a transfer with status '{Status}'.");

        if (now >= ExpiresAt)
            return Result.Fail($"Cannot execute: transfer expired at {ExpiresAt:O}.");

        ExecutedAt = now;
        Status = TransferStatus.Executed;
        return Result.Ok();
    }

    /// <summary>
    /// Rejects the transfer. Valid from <see cref="TransferStatus.Pending"/> or
    /// <see cref="TransferStatus.PartlyApproved"/>.
    /// </summary>
    /// <param name="employeeId">The identifier of the employee performing the rejection.</param>
    /// <returns>
    /// <see cref="Result.Ok()"/> on success; <see cref="Result.Fail(string)"/> if the current status
    /// does not permit rejection.
    /// </returns>
    public Result Reject(EmployeeId employeeId)
    {
        if (Status is not (TransferStatus.Pending or TransferStatus.PartlyApproved))
            return Result.Fail($"Cannot reject a transfer with status '{Status}'.");

        RejectedById = employeeId;
        Status = TransferStatus.Rejected;
        return Result.Ok();
    }

    /// <summary>
    /// Idempotently marks the transfer as <see cref="TransferStatus.Expired"/> if
    /// <paramref name="now"/> is on or after <see cref="ExpiresAt"/> and the transfer has not
    /// already reached a terminal state.
    /// </summary>
    /// <param name="now">The current UTC timestamp, typically from an injected <see cref="TimeProvider"/>.</param>
    /// <returns>Always returns <see cref="Result.Ok()"/>.</returns>
    public Result CheckExpiry(DateTimeOffset now)
    {
        if (Status is TransferStatus.Executed or TransferStatus.Expired or TransferStatus.Rejected)
            return Result.Ok();

        if (now >= ExpiresAt)
            Status = TransferStatus.Expired;

        return Result.Ok();
    }
}

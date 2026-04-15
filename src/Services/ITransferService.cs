using MoneyTransfer.Common;
using MoneyTransfer.Models;
using MoneyTransfer.Validators;
using Transfer = MoneyTransfer.Models.MoneyTransfer;

namespace MoneyTransfer.Services;

/// <summary>Orchestrates the full lifecycle of a <see cref="Transfer"/>.</summary>
public interface ITransferService
{
    /// <summary>
    /// Validates <paramref name="command"/> and creates a new <see cref="Transfer"/>.
    /// </summary>
    /// <param name="command">The creation parameters.</param>
    /// <returns>
    /// <see cref="Result{T}"/> carrying the new transfer on success, or a failure with validation errors.
    /// </returns>
    Result<Transfer> Create(CreateTransferCommand command);

    /// <summary>
    /// Validates <paramref name="command"/> and approves <paramref name="transfer"/>.
    /// Automatically checks expiry before applying the approval.
    /// </summary>
    /// <param name="transfer">The transfer to approve.</param>
    /// <param name="command">The approval parameters.</param>
    /// <returns><see cref="Result.Ok()"/> on success; <see cref="Result.Fail(string)"/> otherwise.</returns>
    Result Approve(Transfer transfer, ApproveTransferCommand command);

    /// <summary>Executes <paramref name="transfer"/> using the current UTC time from the injected <see cref="TimeProvider"/>.</summary>
    /// <param name="transfer">The transfer to execute.</param>
    /// <returns><see cref="Result.Ok()"/> on success; <see cref="Result.Fail(string)"/> otherwise.</returns>
    Result Execute(Transfer transfer);

    /// <summary>
    /// Validates <paramref name="command"/> and rejects <paramref name="transfer"/>.
    /// </summary>
    /// <param name="transfer">The transfer to reject.</param>
    /// <param name="command">The rejection parameters.</param>
    /// <returns><see cref="Result.Ok()"/> on success; <see cref="Result.Fail(string)"/> otherwise.</returns>
    Result Reject(Transfer transfer, RejectTransferCommand command);

    /// <summary>
    /// Idempotently checks whether <paramref name="transfer"/> has passed its expiry timestamp
    /// and transitions it to <see cref="TransferStatus.Expired"/> if so.
    /// </summary>
    /// <param name="transfer">The transfer to inspect.</param>
    /// <returns>Always <see cref="Result.Ok()"/>.</returns>
    Result CheckExpiry(Transfer transfer);
}

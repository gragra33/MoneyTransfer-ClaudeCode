using Blazing.Extensions.DependencyInjection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MoneyTransfer.Common;
using MoneyTransfer.Models;
using MoneyTransfer.Validators;
using Transfer = MoneyTransfer.Models.MoneyTransfer;

namespace MoneyTransfer.Services;

/// <summary>
/// Orchestrates transfer lifecycle operations: validates at the boundary via FluentValidation,
/// then delegates to the domain model.
/// </summary>
[AutoRegister(ServiceLifetime.Singleton, typeof(ITransferService))]
public sealed class TransferService(
    TimeProvider timeProvider,
    IValidator<CreateTransferCommand> createValidator,
    IValidator<ApproveTransferCommand> approveValidator,
    IValidator<RejectTransferCommand> rejectValidator) : ITransferService
{
    /// <inheritdoc/>
    public Result<Transfer> Create(CreateTransferCommand command)
    {
        var validation = createValidator.Validate(command);
        if (!validation.IsValid)
            return Result.Fail<Transfer>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var transfer = new MoneyTransferBuilder()
            .From(command.SourceAccountId)
            .To(command.DestinationAccountId)
            .WithAmount(command.Amount, command.Currency)
            .ExpiresAt(command.ExpiresAt)
            .RequiresApproval(command.RequiresApproval)
            .Build();

        return Result.Ok(transfer);
    }

    /// <inheritdoc/>
    public Result Approve(Transfer transfer, ApproveTransferCommand command)
    {
        var validation = approveValidator.Validate(command);
        if (!validation.IsValid)
            return Result.Fail(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        transfer.CheckExpiry(timeProvider.GetUtcNow());
        return transfer.Approve(command.EmployeeId);
    }

    /// <inheritdoc/>
    public Result Execute(Transfer transfer) =>
        transfer.Execute(timeProvider.GetUtcNow());

    /// <inheritdoc/>
    public Result Reject(Transfer transfer, RejectTransferCommand command)
    {
        var validation = rejectValidator.Validate(command);
        if (!validation.IsValid)
            return Result.Fail(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        return transfer.Reject(command.EmployeeId);
    }

    /// <inheritdoc/>
    public Result CheckExpiry(Transfer transfer) =>
        transfer.CheckExpiry(timeProvider.GetUtcNow());
}

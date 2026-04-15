using Blazing.Extensions.DependencyInjection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MoneyTransfer.Models;
using MoneyTransfer.Services;
using MoneyTransfer.Tests.Fixtures;
using MoneyTransfer.Validators;
using Shouldly;

namespace MoneyTransfer.Tests.IntegrationTests;

/// <summary>
/// Integration tests that wire a real DI container (mirroring Program.cs) and exercise
/// end-to-end scenarios through <see cref="ITransferService"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TransferServiceIntegrationTests : IDisposable
{
    private readonly ServiceProvider   _provider;
    private readonly ITransferService  _service;
    private readonly FrozenTimeProvider _time;

    public TransferServiceIntegrationTests()
    {
        _time = new FrozenTimeProvider(new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(_time);
        services.AddValidatorsFromAssemblyContaining<CreateTransferValidator>(ServiceLifetime.Singleton);
        services.Register(typeof(TransferService).Assembly);

        _provider = services.BuildServiceProvider();
        _service  = _provider.GetRequiredService<ITransferService>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Scenario 1 — Happy path: auto-approved transfer executed immediately

    [Fact]
    public void Scenario_AutoApprovedTransfer_ExecutesSuccessfully()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(requiresApproval: false);

        // Act — Create
        var createResult = _service.Create(command);
        createResult.ShouldSucceed();
        var transfer = createResult.Value!;
        transfer.Status.ShouldBe(TransferStatus.Approved);

        // Act — Execute
        var execResult = _service.Execute(transfer);

        // Assert
        execResult.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Executed);
        transfer.ExecutedAt.ShouldNotBeNull();
    }

    #endregion

    #region Scenario 2 — Happy path: dual-approval flow

    [Fact]
    public void Scenario_DualApproval_ExecutesSuccessfully()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(requiresApproval: true);

        // Act — Create
        var createResult = _service.Create(command);
        createResult.ShouldSucceed();
        var transfer = createResult.Value!;
        transfer.Status.ShouldBe(TransferStatus.Pending);

        // Act — First approval
        var approve1 = _service.Approve(transfer, TestData.ApproveCommand(TestData.EmployeeA));
        approve1.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.PartlyApproved);

        // Act — Second approval (different employee)
        var approve2 = _service.Approve(transfer, TestData.ApproveCommand(TestData.EmployeeB));
        approve2.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Approved);

        // Act — Execute
        var execResult = _service.Execute(transfer);
        execResult.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Executed);
    }

    #endregion

    #region Scenario 3 — Same employee cannot give both approvals

    [Fact]
    public void Scenario_SameEmployeeApprovingTwice_Fails()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(requiresApproval: true);
        var transfer = _service.Create(command).Value!;

        _service.Approve(transfer, TestData.ApproveCommand(TestData.EmployeeA));
        transfer.Status.ShouldBe(TransferStatus.PartlyApproved);

        // Act — same employee tries to give second approval
        var result = _service.Approve(transfer, TestData.ApproveCommand(TestData.EmployeeA));

        // Assert
        result.ShouldFail("different employee");
        transfer.Status.ShouldBe(TransferStatus.PartlyApproved);
    }

    #endregion

    #region Scenario 4 — Rejection from partly-approved state

    [Fact]
    public void Scenario_RejectAfterPartialApproval_Succeeds()
    {
        // Arrange
        var command  = TestData.ValidCreateCommand(requiresApproval: true);
        var transfer = _service.Create(command).Value!;

        _service.Approve(transfer, TestData.ApproveCommand(TestData.EmployeeA));

        // Act
        var result = _service.Reject(transfer, TestData.RejectCommand(TestData.EmployeeB));

        // Assert
        result.ShouldSucceed();
        transfer.Status.ShouldBe(TransferStatus.Rejected);
        transfer.RejectedById.ShouldBe(TestData.EmployeeB);
    }

    #endregion

    #region Scenario 5 — Transfer expires before execution

    [Fact]
    public void Scenario_TransferExpiredBeforeExecution_Fails()
    {
        // Arrange — expiry is "now" + 1 hour; we will advance time past it
        var expiresAt = _time.Now.AddHours(1);
        var command   = TestData.ValidCreateCommand(requiresApproval: false, expiresAt: expiresAt);
        var transfer  = _service.Create(command).Value!;

        // Advance "now" past the expiry
        _time.Advance(TimeSpan.FromHours(2));

        // Act
        var result = _service.Execute(transfer);

        // Assert
        result.ShouldFail("expired");
    }

    #endregion

    #region Scenario 6 — Validation failures bubble through the service

    [Fact]
    public void Scenario_CreateWithInvalidCurrency_Fails()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(currency: "FAKE");

        // Act
        var result = _service.Create(command);

        // Assert
        result.ShouldFail("ISO-4217");
    }

    [Fact]
    public void Scenario_CreateWithNegativeAmount_Fails()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(amount: -50m);

        // Act
        var result = _service.Create(command);

        // Assert
        result.ShouldFail("positive");
    }

    [Fact]
    public void Scenario_CreateWithSameSourceAndDest_Fails()
    {
        // Arrange
        var command = TestData.ValidCreateCommand(
            source: TestData.SourceAccount,
            dest:   TestData.SourceAccount);

        // Act
        var result = _service.Create(command);

        // Assert
        result.ShouldFail("different");
    }

    #endregion
}

/// <summary>
/// Controllable <see cref="TimeProvider"/> for integration tests that need to advance time.
/// </summary>
internal sealed class FrozenTimeProvider(DateTimeOffset initial) : TimeProvider
{
    private DateTimeOffset _now = initial;

    /// <summary>Gets the current fake UTC time.</summary>
    public DateTimeOffset Now => _now;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Advances the fake clock by <paramref name="delta"/>.</summary>
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}

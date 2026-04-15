using Blazing.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using MoneyTransfer.Models;
using MoneyTransfer.Services;
using MoneyTransfer.Validators;

namespace MoneyTransfer.Demo;

/// <summary>Runs the five demo scenarios against <see cref="ITransferService"/>.</summary>
[AutoRegister(ServiceLifetime.Singleton, typeof(IDemoRunner))]
internal sealed class DemoRunner(ITransferService svc, DemoTimeProvider timeProvider) : IDemoRunner
{
    private readonly AccountId _source = AccountId.New();
    private readonly AccountId _dest   = AccountId.New();
    private readonly EmployeeId _empA  = EmployeeId.New();
    private readonly EmployeeId _empB  = EmployeeId.New();

    void IDemoRunner.Run()
    {
        Scenario1_AutoApprovedTransfer();
        Scenario2_TwoApprovalTransfer();
        Scenario3_SameEmployeeDoubleApproval();
        Scenario4_RejectFromPending();
        Scenario5_ExpiredTransfer();
    }

    private void Scenario1_AutoApprovedTransfer()
    {
        Console.WriteLine("=== Scenario 1: Auto-approved transfer ===");

        var r = svc.Create(new CreateTransferCommand(_source, _dest, 100m, "USD", false, timeProvider.GetUtcNow().AddDays(1)));
        Print("Create", r.IsSuccess, r.Error);

        var exec = svc.Execute(r.Value!);
        Print("Execute", exec.IsSuccess, exec.Error);
        Console.WriteLine($"  Status: {r.Value!.Status}");           // Executed
    }

    private void Scenario2_TwoApprovalTransfer()
    {
        Console.WriteLine("\n=== Scenario 2: Two-approval transfer ===");

        var r = svc.Create(new CreateTransferCommand(_source, _dest, 500m, "EUR", true, timeProvider.GetUtcNow().AddDays(1)));
        Print("Create", r.IsSuccess, r.Error);

        var appA = svc.Approve(r.Value!, new ApproveTransferCommand(_empA));
        Print("Approve (employee A)", appA.IsSuccess, appA.Error);
        Console.WriteLine($"  Status: {r.Value!.Status}");           // PartlyApproved

        var appB = svc.Approve(r.Value!, new ApproveTransferCommand(_empB));
        Print("Approve (employee B)", appB.IsSuccess, appB.Error);
        Console.WriteLine($"  Status: {r.Value!.Status}");           // Approved

        var exec = svc.Execute(r.Value!);
        Print("Execute", exec.IsSuccess, exec.Error);
        Console.WriteLine($"  Status: {r.Value!.Status}");           // Executed
    }

    private void Scenario3_SameEmployeeDoubleApproval()
    {
        Console.WriteLine("\n=== Scenario 3: Same-employee double-approval ===");

        var r = svc.Create(new CreateTransferCommand(_source, _dest, 250m, "GBP", true, timeProvider.GetUtcNow().AddDays(1)));
        svc.Approve(r.Value!, new ApproveTransferCommand(_empA));

        var dup = svc.Approve(r.Value!, new ApproveTransferCommand(_empA));  // same employee
        Print("Duplicate approval", dup.IsSuccess, dup.Error);               // False + error
    }

    private void Scenario4_RejectFromPending()
    {
        Console.WriteLine("\n=== Scenario 4: Reject from Pending ===");

        var r = svc.Create(new CreateTransferCommand(_source, _dest, 75m, "CHF", true, timeProvider.GetUtcNow().AddDays(1)));
        var rej = svc.Reject(r.Value!, new RejectTransferCommand(_empA));
        Print("Reject", rej.IsSuccess, rej.Error);
        Console.WriteLine($"  Status: {r.Value!.Status}");                   // Rejected
    }

    private void Scenario5_ExpiredTransfer()
    {
        Console.WriteLine("\n=== Scenario 5: Expired transfer ===");

        var r = svc.Create(new CreateTransferCommand(_source, _dest, 1000m, "JPY", false, timeProvider.GetUtcNow().AddMinutes(5)));
        Print("Create", r.IsSuccess, r.Error);
        Console.WriteLine($"  Status before expiry: {r.Value!.Status}");    // Approved

        timeProvider.Advance(TimeSpan.FromMinutes(10));                       // fast-forward past expiry
        svc.CheckExpiry(r.Value!);
        Console.WriteLine($"  Status after expiry:  {r.Value!.Status}");    // Expired
    }

    private static void Print(string label, bool isSuccess, string error)
    {
        var status = isSuccess ? "OK " : "ERR";
        var detail = isSuccess ? string.Empty : $" — {error}";
        Console.WriteLine($"  [{status}] {label}{detail}");
    }
}

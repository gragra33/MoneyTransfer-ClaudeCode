# Use Cases

## Actors

| Actor                     | Description                                                                                |
| ------------------------- | ------------------------------------------------------------------------------------------ |
| **Caller**                | Any code that resolves `ITransferService` — the entry point or a higher-level orchestrator |
| **Employee A**            | The employee who grants first approval                                                     |
| **Employee B**            | A _different_ employee who grants second approval                                          |
| **System (TimeProvider)** | Provides the current UTC timestamp for expiry and execution checks                         |

---

## UC-01 — Create Auto-Approved Transfer

> The caller creates a transfer that does not require manual approval.
> The transfer is immediately in `Approved` state and ready to execute.

```mermaid
sequenceDiagram
    actor Caller
    participant TS as TransferService
    participant V  as CreateTransferValidator
    participant MB as MoneyTransferBuilder
    participant MT as MoneyTransfer

    Caller->>TS: Create(command, RequiresApproval=false)
    TS->>V: Validate(command)
    V-->>TS: Valid
    TS->>MB: From().To().WithAmount().ExpiresAt().RequiresApproval(false).Build()
    MB->>MT: new MoneyTransfer(requiresApproval=false)
    Note over MT: Status = Approved
    MT-->>TS: transfer
    TS-->>Caller: Result.Ok(transfer)
```

---

## UC-02 — Create Transfer Requiring Approval

> The caller creates a transfer that requires sign-off from two different employees.
> The transfer starts in `Pending` state.

```mermaid
sequenceDiagram
    actor Caller
    participant TS as TransferService
    participant V  as CreateTransferValidator
    participant MB as MoneyTransferBuilder
    participant MT as MoneyTransfer

    Caller->>TS: Create(command, RequiresApproval=true)
    TS->>V: Validate(command)
    V-->>TS: Valid
    TS->>MB: .RequiresApproval(true).Build()
    MB->>MT: new MoneyTransfer(requiresApproval=true)
    Note over MT: Status = Pending
    MT-->>TS: transfer
    TS-->>Caller: Result.Ok(transfer)
```

---

## UC-03 — First Approval (Pending → PartlyApproved)

> Employee A approves a pending transfer.

```mermaid
sequenceDiagram
    actor EmpA as Employee A
    participant TS as TransferService
    participant AV as ApproveTransferValidator
    participant MT as MoneyTransfer
    participant TP as TimeProvider

    EmpA->>TS: Approve(transfer, ApproveTransferCommand(empA))
    TS->>AV: Validate(command)
    AV-->>TS: Valid
    TS->>TP: GetUtcNow()
    TP-->>TS: now
    TS->>MT: CheckExpiry(now)
    Note over MT: Status still Pending (not expired)
    TS->>MT: Approve(empA)
    Note over MT: Status = PartlyApproved\nFirstApproverId = empA
    MT-->>TS: Result.Ok()
    TS-->>EmpA: Result.Ok()
```

---

## UC-04 — Second Approval (PartlyApproved → Approved)

> A _different_ employee B approves a partly-approved transfer.

```mermaid
sequenceDiagram
    actor EmpB as Employee B
    participant TS as TransferService
    participant AV as ApproveTransferValidator
    participant MT as MoneyTransfer

    EmpB->>TS: Approve(transfer, ApproveTransferCommand(empB))
    TS->>AV: Validate(command)
    AV-->>TS: Valid
    TS->>MT: CheckExpiry(now)
    TS->>MT: Approve(empB)
    Note over MT: empB ≠ FirstApproverId ✓\nStatus = Approved\nSecondApproverId = empB
    MT-->>TS: Result.Ok()
    TS-->>EmpB: Result.Ok()
```

---

## UC-05 — Execute Transfer

> An approved transfer is settled before its expiry timestamp.

```mermaid
sequenceDiagram
    actor Caller
    participant TS as TransferService
    participant MT as MoneyTransfer
    participant TP as TimeProvider

    Caller->>TS: Execute(transfer)
    TS->>TP: GetUtcNow()
    TP-->>TS: now
    TS->>MT: Execute(now)
    alt Status == Approved AND now < ExpiresAt
        Note over MT: Status = Executed\nExecutedAt = now
        MT-->>TS: Result.Ok()
        TS-->>Caller: Result.Ok()
    else Wrong status or expired
        MT-->>TS: Result.Fail(reason)
        TS-->>Caller: Result.Fail(reason)
    end
```

---

## UC-06 — Reject Transfer

> An employee rejects a transfer that is `Pending` or `PartlyApproved`.

```mermaid
sequenceDiagram
    actor Emp as Employee
    participant TS as TransferService
    participant RV as RejectTransferValidator
    participant MT as MoneyTransfer

    Emp->>TS: Reject(transfer, RejectTransferCommand(emp))
    TS->>RV: Validate(command)
    RV-->>TS: Valid
    TS->>MT: Reject(emp)
    alt Status is Pending or PartlyApproved
        Note over MT: Status = Rejected\nRejectedById = emp
        MT-->>TS: Result.Ok()
        TS-->>Emp: Result.Ok()
    else Any other status
        MT-->>TS: Result.Fail("Cannot reject...")
        TS-->>Emp: Result.Fail(reason)
    end
```

---

## UC-07 — Check Expiry

> The system checks whether a transfer has passed its expiry timestamp.
> This operation is idempotent and never fails.

```mermaid
sequenceDiagram
    participant Caller
    participant TS as TransferService
    participant MT as MoneyTransfer
    participant TP as TimeProvider

    Caller->>TS: CheckExpiry(transfer)
    TS->>TP: GetUtcNow()
    TP-->>TS: now
    TS->>MT: CheckExpiry(now)
    alt Already terminal (Executed / Expired / Rejected)
        Note over MT: No change
    else now >= ExpiresAt
        Note over MT: Status = Expired
    else now < ExpiresAt
        Note over MT: No change
    end
    MT-->>TS: Result.Ok()
    TS-->>Caller: Result.Ok()
```

---

## UC-08 — Validation Failure

> Any command that fails FluentValidation returns a `Result.Fail` before domain logic runs.

```mermaid
sequenceDiagram
    actor Caller
    participant TS as TransferService
    participant V  as Validator

    Caller->>TS: Create / Approve / Reject with invalid command
    TS->>V: Validate(command)
    V-->>TS: Invalid — one or more errors
    Note over TS: Joins error messages with semicolons
    TS-->>Caller: Result.Fail
    Note over Caller: Domain model never touched
```

---

## UC-09 — Same-Employee Double Approval (Guard)

> The same employee attempts to provide both approvals.
> The model rejects the second attempt.

```mermaid
sequenceDiagram
    actor EmpA as Employee A
    participant TS as TransferService
    participant MT as MoneyTransfer

    EmpA->>TS: Approve(transfer, empA)   [first time]
    TS->>MT: Approve(empA)
    Note over MT: Status = PartlyApproved\nFirstApproverId = empA

    EmpA->>TS: Approve(transfer, empA)   [second time]
    TS->>MT: Approve(empA)
    Note over MT: empA == FirstApproverId → guard fails
    MT-->>TS: Result.Fail("The second approver must be a different employee...")
    TS-->>EmpA: Result.Fail(reason)
```

---

## Business Rules Summary

| Rule                                                                    | Enforced In                                           |
| ----------------------------------------------------------------------- | ----------------------------------------------------- |
| Amount must be positive                                                 | `CreateTransferValidator`                             |
| Currency must be valid ISO-4217                                         | `CreateTransferValidator`                             |
| Source ≠ Destination account                                            | `CreateTransferValidator`                             |
| Expiry must be in the future                                            | `CreateTransferValidator`                             |
| Employee ID must not be empty                                           | `ApproveTransferValidator`, `RejectTransferValidator` |
| Auto-approved transfer starts `Approved`                                | `MoneyTransfer` constructor                           |
| Approval-required transfer starts `Pending`                             | `MoneyTransfer` constructor                           |
| First approval: `Pending` → `PartlyApproved`                            | `MoneyTransfer.Approve`                               |
| Second approval: `PartlyApproved` → `Approved`, different employee only | `MoneyTransfer.Approve`                               |
| Execution: `Approved` + now before expiry only                          | `MoneyTransfer.Execute`                               |
| Rejection: `Pending` or `PartlyApproved` only                           | `MoneyTransfer.Reject`                                |
| Expiry check: idempotent, never fails                                   | `MoneyTransfer.CheckExpiry`                           |
| No exceptions thrown anywhere                                           | All layers                                            |

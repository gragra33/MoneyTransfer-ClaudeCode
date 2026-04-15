# Architecture

## Overview

MoneyTransfer is a **.NET 10 / C# 14** console application that models a
money-transfer lifecycle. It is structured in four layers:

| Layer          | Namespace                  | Responsibility                                     |
| -------------- | -------------------------- | -------------------------------------------------- |
| **Common**     | `MoneyTransfer.Common`     | Shared primitives (`Result<T>`)                    |
| **Models**     | `MoneyTransfer.Models`     | Domain entity, IDs, status enum, fluent builder    |
| **Validators** | `MoneyTransfer.Validators` | FluentValidation command records + validators      |
| **Services**   | `MoneyTransfer.Services`   | Service interface and DI-registered implementation |

---

## Component Diagram

```mermaid
graph TD
    subgraph Entry["Entry Point"]
        P["Program.cs\n(ServiceCollection + demo)"]
    end

    subgraph Services["MoneyTransfer.Services"]
        I["ITransferService"]
        S["TransferService\n[AutoRegister Singleton]"]
    end

    subgraph Validators["MoneyTransfer.Validators"]
        CV["CreateTransferValidator\n+ CreateTransferCommand"]
        AV["ApproveTransferValidator\n+ ApproveTransferCommand"]
        RV["RejectTransferValidator\n+ RejectTransferCommand"]
    end

    subgraph Models["MoneyTransfer.Models"]
        MT["MoneyTransfer\n(sealed, internal ctor)"]
        MB["MoneyTransferBuilder\n(fluent)"]
        IDS["Ids\nTransferId · AccountId · EmployeeId"]
        TS["TransferStatus\n(enum)"]
    end

    subgraph Common["MoneyTransfer.Common"]
        R["Result / Result&lt;T&gt;"]
    end

    subgraph DI["Dependency Injection"]
        TP["TimeProvider"]
        FV["FluentValidation\nAddValidatorsFromAssemblyContaining"]
        BR["Blazing.Extensions.DI\nservices.Register()"]
    end

    P --> I
    I --> S
    S --> CV
    S --> AV
    S --> RV
    S --> MB
    MB --> MT
    MT --> TS
    MT --> IDS
    MT --> R
    S --> R
    TP --> S
    TP --> CV
    FV --> CV
    FV --> AV
    FV --> RV
    BR --> S
```

---

## Package Dependencies

```mermaid
graph LR
    proj["MoneyTransfer.csproj\nnet10.0"]
    fv["FluentValidation"]
    fvdi["FluentValidation\n.DependencyInjectionExtensions"]
    bdi["Blazing.Extensions\n.DependencyInjection"]

    proj --> fv
    proj --> fvdi
    proj --> bdi
```

---

## Class Diagram

```mermaid
classDiagram
    class Result {
        +bool IsSuccess
        +string Error
        +Ok() Result
        +Fail(string) Result
        +Ok~T~(T value) Result~T~
        +Fail~T~(string) Result~T~
    }

    class Result~T~ {
        +bool IsSuccess
        +T? Value
        +string Error
        +Ok(T value) Result~T~
        +Fail(string) Result~T~
    }

    class TransferId {
        +Guid Value
        +New() TransferId
    }

    class AccountId {
        +Guid Value
        +New() AccountId
    }

    class EmployeeId {
        +Guid Value
        +New() EmployeeId
    }

    class TransferStatus {
        <<enumeration>>
        Pending
        PartlyApproved
        Approved
        Executed
        Expired
        Rejected
    }

    class MoneyTransfer {
        +TransferId Id
        +AccountId SourceAccountId
        +AccountId DestinationAccountId
        +decimal Amount
        +string Currency
        +DateTimeOffset ExpiresAt
        +TransferStatus Status
        +EmployeeId? FirstApproverId
        +EmployeeId? SecondApproverId
        +EmployeeId? RejectedById
        +DateTimeOffset? ExecutedAt
        +Approve(EmployeeId) Result
        +Execute(DateTimeOffset) Result
        +Reject(EmployeeId) Result
        +CheckExpiry(DateTimeOffset) Result
    }

    class MoneyTransferBuilder {
        +From(AccountId) MoneyTransferBuilder
        +To(AccountId) MoneyTransferBuilder
        +WithAmount(decimal, string) MoneyTransferBuilder
        +ExpiresAt(DateTimeOffset) MoneyTransferBuilder
        +RequiresApproval(bool) MoneyTransferBuilder
        ~Build() MoneyTransfer
    }

    class CreateTransferCommand {
        <<record>>
        +AccountId SourceAccountId
        +AccountId DestinationAccountId
        +decimal Amount
        +string Currency
        +bool RequiresApproval
        +DateTimeOffset ExpiresAt
    }

    class ApproveTransferCommand {
        <<record>>
        +EmployeeId EmployeeId
    }

    class RejectTransferCommand {
        <<record>>
        +EmployeeId EmployeeId
    }

    class ITransferService {
        <<interface>>
        +Create(CreateTransferCommand) Result~MoneyTransfer~
        +Approve(MoneyTransfer, ApproveTransferCommand) Result
        +Execute(MoneyTransfer) Result
        +Reject(MoneyTransfer, RejectTransferCommand) Result
        +CheckExpiry(MoneyTransfer) Result
    }

    class TransferService {
        -TimeProvider timeProvider
        -IValidator~CreateTransferCommand~ createValidator
        -IValidator~ApproveTransferCommand~ approveValidator
        -IValidator~RejectTransferCommand~ rejectValidator
        +Create(CreateTransferCommand) Result~MoneyTransfer~
        +Approve(MoneyTransfer, ApproveTransferCommand) Result
        +Execute(MoneyTransfer) Result
        +Reject(MoneyTransfer, RejectTransferCommand) Result
        +CheckExpiry(MoneyTransfer) Result
    }

    ITransferService <|.. TransferService
    MoneyTransferBuilder ..> MoneyTransfer : builds
    MoneyTransfer --> TransferStatus
    MoneyTransfer --> TransferId
    MoneyTransfer --> AccountId
    MoneyTransfer --> EmployeeId
    MoneyTransfer ..> Result
    TransferService --> MoneyTransferBuilder
    TransferService ..> Result
```

---

## Design Patterns

| Pattern                       | Where Applied                           | Rationale                                                                                 |
| ----------------------------- | --------------------------------------- | ----------------------------------------------------------------------------------------- |
| **Fluent Builder**            | `MoneyTransferBuilder`                  | Clean construction DSL; `internal Build()` enforces construction only through the service |
| **Result / Railway-oriented** | `Result` / `Result<T>`                  | Zero exceptions; every failure is an explicit typed return value                          |
| **Guard-clause transitions**  | `MoneyTransfer` methods                 | Two-line checks; readable, no state machine overhead                                      |
| **Boundary Validation**       | `TransferService` + FluentValidation    | Validates commands before any domain logic runs; domain model stays clean                 |
| **Strongly Typed IDs**        | `TransferId`, `AccountId`, `EmployeeId` | Prevents parameter mix-up at compile time                                                 |
| **[AutoRegister] DI**         | `TransferService`                       | Minimal registration boilerplate; assembly-scan wiring                                    |
| **Injected time**             | `TimeProvider`                          | Deterministic testing; no `DateTime.UtcNow` in production code                            |

---

## Key Constraints

- **No exceptions thrown** anywhere in the domain or service layer
- **Validation first** — every `TransferService` method validates its command before touching the model
- **`internal` constructor** — `MoneyTransfer` can only be created inside the assembly via `MoneyTransferBuilder`
- **ISO-4217 currency** — validated against a `FrozenSet<string>` for O(1) allocation-free lookup
- **`TimeProvider` injection** — expiry comparisons always use injected time, never ambient `UtcNow`

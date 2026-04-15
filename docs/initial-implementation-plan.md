# Initial Implementation — Plan

## Overview

This is a .NET 10 / C# 14 console application modelling a money transfer domain. It provides a
strongly-typed, exception-free transfer lifecycle (create → approve → execute / expire / reject)
wired through `Blazing.Extensions.DependencyInjection`.

**Goals:**

- Model all six transfer states with correct business rules
- Enforce validation at the service boundary via FluentValidation _before_ any business logic runs
- Return `Result<T>` from every fallible operation — zero exceptions thrown
- Provide a testable, DI-composed entry point in `Program.cs`

**Success Criteria:**

- All state transitions enforce correct guard clauses and return `Result.Fail` on violations
- Build passes with zero warnings; all public members carry XML documentation
- `Program.cs` demo exercises the full happy path and key rejection paths

---

## Architecture

### Technology Stack

- **.NET 10.0 / C# 14** — `LangVersion: latest`, nullable reference types enabled
- **FluentValidation** — `AbstractValidator<T>`, `AddValidatorsFromAssemblyContaining<Program>()`, validated before any business logic
- **Blazing.Extensions.DependencyInjection** — `ApplicationHost`, `[AutoRegister]`, `services.Register()` assembly scan
- **`TimeProvider`** — injected into `TransferService`; passed as `DateTimeOffset now` to model methods

### Design Patterns

| Concern           | Pattern                                     | Rationale                                                              |
| ----------------- | ------------------------------------------- | ---------------------------------------------------------------------- |
| Construction      | **Fluent Builder**                          | Separates object construction from business logic; readable call sites |
| Error propagation | **`Result<T>`**                             | No exceptions; callers are forced to handle failure paths              |
| State transitions | **Guard clauses**                           | Two-line checks per method — no state machine, no verbosity            |
| Validation        | **FluentValidation**                        | Boundary validation before service delegates to model                  |
| DI                | **`[AutoRegister]`**                        | Minimal registration boilerplate in a console host                     |
| IDs               | **Strongly typed `readonly record struct`** | Prevents `AccountId`/`EmployeeId` mix-up at compile time               |
| Currency          | **ISO-4217 `FrozenSet<string>`**            | Allocation-free lookup in validator                                    |

### Component Relationships

```
Program.cs (ApplicationHost)
  └─▶ TransferService : ITransferService          [AutoRegister Singleton]
        ├─▶ TimeProvider                           (injected)
        ├─▶ CreateTransferValidator                (FluentValidation)
        ├─▶ ApproveTransferValidator
        ├─▶ RejectTransferValidator
        └─▶ MoneyTransferBuilder
              └─▶ MoneyTransfer  (internal ctor)
                    ├─▶ Approve(EmployeeId)      → Result
                    ├─▶ Execute(DateTimeOffset)  → Result
                    ├─▶ Reject(EmployeeId)       → Result
                    └─▶ CheckExpiry(DateTimeOffset) → Result
```

### State Transition Logic (guard clauses — no state machine)

| Method                | Valid pre-condition                     | Outcome                                    | On failure                     |
| --------------------- | --------------------------------------- | ------------------------------------------ | ------------------------------ |
| `Approve(employeeId)` | `Pending`                               | `PartlyApproved`, stores `FirstApproverId` | `Result.Fail`                  |
| `Approve(employeeId)` | `PartlyApproved` + _different_ employee | `Approved`, stores `SecondApproverId`      | `Result.Fail` if same employee |
| `Execute(now)`        | `Approved` + `now < ExpiresAt`          | `Executed`, stores `ExecutedAt`            | `Result.Fail`                  |
| `Reject(employeeId)`  | `Pending` or `PartlyApproved`           | `Rejected`, stores `RejectedById`          | `Result.Fail`                  |
| `CheckExpiry(now)`    | Not terminal + `now >= ExpiresAt`       | `Expired`                                  | `Result.Ok` (idempotent)       |

### Data Flow

```
Caller
  └─▶ ITransferService.Create(CreateTransferCommand)
        ├─▶ CreateTransferValidator.Validate()    ← fails fast, returns Result.Fail with errors
        └─▶ new MoneyTransferBuilder()
              .From(sourceAccountId)
              .To(destinationAccountId)
              .WithAmount(amount, currency)
              .ExpiresAt(expiresAt)
              .RequiresApproval(requiresApproval)
              .Build()                            ← internal ctor; Status set here
                    └─▶ Result<MoneyTransfer>.Ok(transfer)
```

---

## Folder & Files

### Directory Structure

```
MoneyTransfer/
├── MoneyTransfer.sln
├── src/
│   ├── MoneyTransfer.csproj                 Console, net10.0, LangVersion=latest
│   ├── Program.cs                           ApplicationHost DI setup + demo
│   ├── Common/
│   │   └── Result.cs                        Result (non-generic) + Result<T> sealed classes
│   ├── Models/
│   │   ├── Ids.cs                           TransferId, AccountId, EmployeeId  (readonly record struct)
│   │   ├── TransferStatus.cs                enum: Pending, PartlyApproved, Approved, Executed, Expired, Rejected
│   │   ├── MoneyTransfer.cs                 sealed class — core entity + transition methods
│   │   └── MoneyTransferBuilder.cs          fluent builder → internal Build() → MoneyTransfer
│   ├── Validators/
│   │   ├── CreateTransferValidator.cs       CreateTransferCommand record + AbstractValidator (co-located)
│   │   ├── ApproveTransferValidator.cs      ApproveTransferCommand record + AbstractValidator (co-located)
│   │   └── RejectTransferValidator.cs       RejectTransferCommand record + AbstractValidator (co-located)
│   └── Services/
│       ├── ITransferService.cs              interface
│       └── TransferService.cs               [AutoRegister(Singleton, typeof(ITransferService))]
└── tests/
    └── MoneyTransfer.Tests/
        ├── MoneyTransfer.Tests.csproj        xUnit, net10.0; references src/MoneyTransfer.csproj
        ├── GlobalUsings.cs                   global using Xunit;
        ├── Fixtures/
        │   ├── TestData.cs                   Shared test data factories
        │   └── TestHelpers.cs               Builder helpers and assertion utilities
        ├── UnitTests/
        │   ├── Common/
        │   │   └── ResultTests.cs            Result / Result<T> unit tests
        │   ├── Models/
        │   │   ├── MoneyTransferTests.cs     State transition and guard-clause tests
        │   │   └── MoneyTransferBuilderTests.cs  Fluent builder construction tests
        │   ├── Services/
        │   │   └── TransferServiceTests.cs   Service orchestration tests (Moq)
        │   └── Validators/
        │       ├── CreateTransferValidatorTests.cs  CreateTransferCommand validation tests
        │       └── ApproveRejectValidatorTests.cs   Approve/Reject command validation tests
        └── IntegrationTests/
            └── TransferServiceIntegrationTests.cs   End-to-end lifecycle scenarios
```

### File Responsibilities

| File                                                | Purpose                                                                   |
| --------------------------------------------------- | ------------------------------------------------------------------------- |
| `src/Common/Result.cs`                              | Discriminated union for success/failure; no exceptions escape             |
| `src/Models/Ids.cs`                                 | Strongly typed ID wrappers preventing parameter mix-up                    |
| `src/Models/TransferStatus.cs`                      | Enum for all six lifecycle states                                         |
| `src/Models/MoneyTransfer.cs`                       | Domain entity; construction via internal ctor; guard-clause transitions   |
| `src/Models/MoneyTransferBuilder.cs`                | Fluent DSL; accumulates fields; delegates construction to `MoneyTransfer` |
| `src/Validators/CreateTransferValidator.cs`         | Validates amount > 0, ISO-4217 currency, non-empty GUIDs, future expiry   |
| `src/Validators/ApproveTransferValidator.cs`        | Validates EmployeeId non-empty                                            |
| `src/Validators/RejectTransferValidator.cs`         | Validates EmployeeId non-empty                                            |
| `src/Services/ITransferService.cs`                  | Public contract; returns `Result` / `Result<MoneyTransfer>`               |
| `src/Services/TransferService.cs`                   | Orchestrates: validate → build/delegate → return result                   |
| `src/Program.cs`                                    | Wires DI; runs demo scenarios                                             |
| `tests/MoneyTransfer.Tests/Fixtures/TestData.cs`    | Shared test data factories (valid commands, IDs)                          |
| `tests/MoneyTransfer.Tests/Fixtures/TestHelpers.cs` | Builder convenience helpers and Shouldly assertion utilities              |
| `tests/MoneyTransfer.Tests/UnitTests/…`             | Isolated unit tests per layer (Common, Models, Services, Validators)      |
| `tests/MoneyTransfer.Tests/IntegrationTests/…`      | End-to-end lifecycle scenarios wired through real DI container            |

---

## Code Snippets

### Common/Result.cs

```csharp
namespace MoneyTransfer.Common;

/// <summary>Non-generic result for operations with no return value.</summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public string Error { get; }

    private Result(bool isSuccess, string error) => (IsSuccess, Error) = (isSuccess, error);

    public static Result Ok() => new(true, string.Empty);
    public static Result Fail(string error) => new(false, error);
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);
    public static Result<T> Fail<T>(string error) => Result<T>.Fail(error);
}

/// <summary>Generic result carrying a value on success.</summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string Error { get; }

    private Result(bool isSuccess, T? value, string error) =>
        (IsSuccess, Value, Error) = (isSuccess, value, error);

    public static Result<T> Ok(T value) => new(true, value, string.Empty);
    public static Result<T> Fail(string error) => new(false, default, error);
}
```

### Models/Ids.cs

```csharp
namespace MoneyTransfer.Models;

/// <summary>Strongly typed transfer identifier.</summary>
public readonly record struct TransferId(Guid Value)
{
    public static TransferId New() => new(Guid.NewGuid());
}

/// <summary>Strongly typed account identifier.</summary>
public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.NewGuid());
}

/// <summary>Strongly typed employee identifier.</summary>
public readonly record struct EmployeeId(Guid Value)
{
    public static EmployeeId New() => new(Guid.NewGuid());
}
```

### Models/TransferStatus.cs

```csharp
namespace MoneyTransfer.Models;

/// <summary>Lifecycle states of a money transfer.</summary>
public enum TransferStatus
{
    Pending,
    PartlyApproved,
    Approved,
    Executed,
    Expired,
    Rejected
}
```

### Models/MoneyTransfer.cs (key structure)

```csharp
namespace MoneyTransfer.Models;

/// <summary>Domain entity representing a money transfer.</summary>
public sealed class MoneyTransfer
{
    // Construction only via MoneyTransferBuilder
    internal MoneyTransfer(
        AccountId sourceAccountId, AccountId destinationAccountId,
        decimal amount, string currency,
        bool requiresApproval, DateTimeOffset expiresAt)
    {
        Id             = TransferId.New();
        SourceAccount  = sourceAccountId;
        DestAccount    = destinationAccountId;
        Amount         = amount;
        Currency       = currency;
        ExpiresAt      = expiresAt;
        Status         = requiresApproval ? TransferStatus.Pending : TransferStatus.Approved;
    }

    public TransferId    Id            { get; }
    public AccountId     SourceAccount { get; }
    public AccountId     DestAccount   { get; }
    public decimal       Amount        { get; }
    public string        Currency      { get; }
    public TransferStatus Status       { get; private set; }
    public DateTimeOffset ExpiresAt    { get; }
    public EmployeeId?   FirstApproverId  { get; private set; }
    public EmployeeId?   SecondApproverId { get; private set; }
    public EmployeeId?   RejectedById     { get; private set; }
    public DateTimeOffset? ExecutedAt     { get; private set; }

    /// <summary>Approves the transfer. Pending → PartlyApproved or PartlyApproved → Approved.</summary>
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

        return Result.Fail($"Cannot approve a transfer with status '{Status}'.");
    }

    /// <summary>Executes the transfer if Approved and not yet expired.</summary>
    public Result Execute(DateTimeOffset now)
    {
        if (Status != TransferStatus.Approved || now >= ExpiresAt)
            return Result.Fail($"Cannot execute: status='{Status}', now={now:O}, expiresAt={ExpiresAt:O}.");

        ExecutedAt = now;
        Status = TransferStatus.Executed;
        return Result.Ok();
    }

    /// <summary>Rejects the transfer. Valid from Pending or PartlyApproved.</summary>
    public Result Reject(EmployeeId employeeId)
    {
        if (Status is not (TransferStatus.Pending or TransferStatus.PartlyApproved))
            return Result.Fail($"Cannot reject a transfer with status '{Status}'.");

        RejectedById = employeeId;
        Status = TransferStatus.Rejected;
        return Result.Ok();
    }

    /// <summary>Idempotently marks the transfer Expired if past ExpiresAt and not yet terminal.</summary>
    public Result CheckExpiry(DateTimeOffset now)
    {
        if (Status is TransferStatus.Executed or TransferStatus.Expired or TransferStatus.Rejected)
            return Result.Ok();

        if (now >= ExpiresAt)
            Status = TransferStatus.Expired;

        return Result.Ok();
    }
}
```

### Models/MoneyTransferBuilder.cs

```csharp
namespace MoneyTransfer.Models;

/// <summary>Fluent builder for <see cref="MoneyTransfer"/>.</summary>
public sealed class MoneyTransferBuilder
{
    private AccountId     _source;
    private AccountId     _dest;
    private decimal       _amount;
    private string        _currency  = string.Empty;
    private DateTimeOffset _expiresAt;
    private bool          _requiresApproval;

    /// <summary>Sets the source account.</summary>
    public MoneyTransferBuilder From(AccountId source)        { _source           = source;           return this; }
    /// <summary>Sets the destination account.</summary>
    public MoneyTransferBuilder To(AccountId dest)            { _dest             = dest;             return this; }
    /// <summary>Sets the monetary amount and ISO-4217 currency code.</summary>
    public MoneyTransferBuilder WithAmount(decimal amount, string currency)
                                                              { _amount = amount; _currency = currency; return this; }
    /// <summary>Sets the expiration timestamp (UTC).</summary>
    public MoneyTransferBuilder ExpiresAt(DateTimeOffset at)  { _expiresAt        = at;               return this; }
    /// <summary>Controls whether two-employee approval is required.</summary>
    public MoneyTransferBuilder RequiresApproval(bool value)  { _requiresApproval = value;            return this; }

    /// <summary>Constructs the <see cref="MoneyTransfer"/> instance.</summary>
    internal MoneyTransfer Build() =>
        new(_source, _dest, _amount, _currency, _requiresApproval, _expiresAt);
}
```

### Validators/CreateTransferValidator.cs (key structure)

```csharp
namespace MoneyTransfer.Validators;

/// <summary>Command for creating a new transfer.</summary>
public sealed record CreateTransferCommand(
    AccountId SourceAccountId,
    AccountId DestinationAccountId,
    decimal Amount,
    string Currency,
    bool RequiresApproval,
    DateTimeOffset ExpiresAt);

/// <summary>Validates <see cref="CreateTransferCommand"/>.</summary>
public sealed class CreateTransferValidator : AbstractValidator<CreateTransferCommand>
{
    // ISO-4217 major currency codes (FrozenSet for O(1) lookup, zero allocation)
    private static readonly FrozenSet<string> ValidCurrencies = new[]
    {
        "AED","AFN","ALL","AMD","ANG","AOA","ARS","AUD","AWG","AZN",
        "BAM","BBD","BDT","BGN","BHD","BIF","BMD","BND","BOB","BRL",
        "BSD","BTN","BWP","BYN","BZD","CAD","CDF","CHF","CLP","CNY",
        "COP","CRC","CUP","CVE","CZK","DJF","DKK","DOP","DZD","EGP",
        "ERN","ETB","EUR","FJD","FKP","FOK","GBP","GEL","GGP","GHS",
        "GIP","GMD","GNF","GTQ","GYD","HKD","HNL","HRK","HTG","HUF",
        "IDR","ILS","IMP","INR","IQD","IRR","ISK","JEP","JMD","JOD",
        "JPY","KES","KGS","KHR","KID","KMF","KRW","KWD","KYD","KZT",
        "LAK","LBP","LKR","LRD","LSL","LYD","MAD","MDL","MGA","MKD",
        "MMK","MNT","MOP","MRU","MUR","MVR","MWK","MXN","MYR","MZN",
        "NAD","NGN","NIO","NOK","NPR","NZD","OMR","PAB","PEN","PGK",
        "PHP","PKR","PLN","PYG","QAR","RON","RSD","RUB","RWF","SAR",
        "SBD","SCR","SDG","SEK","SGD","SHP","SLE","SLL","SOS","SRD",
        "SSP","STN","SYP","SZL","THB","TJS","TMT","TND","TOP","TRY",
        "TTD","TVD","TWD","TZS","UAH","UGX","USD","UYU","UZS","VES",
        "VND","VUV","WST","XAF","XCD","XOF","XPF","YER","ZAR","ZMW","ZWL"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public CreateTransferValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.SourceAccountId.Value).NotEmpty().WithMessage("Source account ID must not be empty.");
        RuleFor(x => x.DestinationAccountId.Value).NotEmpty().WithMessage("Destination account ID must not be empty.");
        RuleFor(x => x.SourceAccountId).NotEqual(x => x.DestinationAccountId)
            .WithMessage("Source and destination accounts must differ.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be positive.");
        RuleFor(x => x.Currency).Must(c => ValidCurrencies.Contains(c))
            .WithMessage(x => $"'{x.Currency}' is not a valid ISO-4217 currency code.");
        RuleFor(x => x.ExpiresAt)
            .Must(e => e > timeProvider.GetUtcNow())
            .WithMessage("Expiration must be in the future.");
    }
}
```

### Services/TransferService.cs (key structure)

```csharp
namespace MoneyTransfer.Services;

/// <summary>Orchestrates transfer lifecycle operations.</summary>
[AutoRegister(ServiceLifetime.Singleton, typeof(ITransferService))]
public sealed class TransferService(
    TimeProvider timeProvider,
    CreateTransferValidator createValidator,
    ApproveTransferValidator approveValidator,
    RejectTransferValidator rejectValidator) : ITransferService
{
    public Result<MoneyTransfer> Create(CreateTransferCommand command)
    {
        var validation = createValidator.Validate(command);
        if (!validation.IsValid)
            return Result.Fail<MoneyTransfer>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        var transfer = new MoneyTransferBuilder()
            .From(command.SourceAccountId)
            .To(command.DestinationAccountId)
            .WithAmount(command.Amount, command.Currency)
            .ExpiresAt(command.ExpiresAt)
            .RequiresApproval(command.RequiresApproval)
            .Build();

        return Result.Ok(transfer);
    }

    public Result Approve(MoneyTransfer transfer, ApproveTransferCommand command)
    {
        var validation = approveValidator.Validate(command);
        if (!validation.IsValid)
            return Result.Fail(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        transfer.CheckExpiry(timeProvider.GetUtcNow());
        return transfer.Approve(command.EmployeeId);
    }

    public Result Execute(MoneyTransfer transfer) =>
        transfer.Execute(timeProvider.GetUtcNow());

    public Result Reject(MoneyTransfer transfer, RejectTransferCommand command)
    {
        var validation = rejectValidator.Validate(command);
        if (!validation.IsValid)
            return Result.Fail(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));

        return transfer.Reject(command.EmployeeId);
    }

    public Result CheckExpiry(MoneyTransfer transfer) =>
        transfer.CheckExpiry(timeProvider.GetUtcNow());
}
```

### Program.cs (DI wiring)

```csharp
var host = new ApplicationHost();
host.ConfigureServices(services =>
{
    services.AddSingleton(TimeProvider.System);
    services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Singleton);
    services.Register(); // scans [AutoRegister] in this assembly
});

var svc = host.GetRequiredService<ITransferService>();
// ... demo runs
```

---

## Phases

### Phase 1 — Foundation

**Goal:** Project skeleton, shared types, and domain primitives.

**Deliverables:**

- `MoneyTransfer.csproj` configured with all required packages
- `Common/Result.cs` — `Result` and `Result<T>` sealed classes
- `Models/Ids.cs` — `TransferId`, `AccountId`, `EmployeeId`
- `Models/TransferStatus.cs` — `TransferStatus` enum

**Dependencies:** None

---

### Phase 2 — Core Domain Model

**Goal:** Implement the entity and its builder.

**Deliverables:**

- `Models/MoneyTransfer.cs` with internal ctor and all four transition methods
- `Models/MoneyTransferBuilder.cs` with fluent API

**Dependencies:** Phase 1 complete

---

### Phase 3 — Validation + Service Layer

**Goal:** Enforce boundary validation and orchestrate the lifecycle.

**Deliverables:**

- `Validators/CreateTransferValidator.cs` — command record + validator
- `Validators/ApproveTransferValidator.cs` — command record + validator
- `Validators/RejectTransferValidator.cs` — command record + validator
- `Services/ITransferService.cs` — public contract
- `Services/TransferService.cs` — `[AutoRegister]`, primary ctor, validates-then-delegates

**Dependencies:** Phase 2 complete

---

### Phase 4 — DI Wiring + Demo

**Goal:** Compose the application and verify all paths.

**Deliverables:**

- `src/Program.cs` — `ApplicationHost`, DI registration, demo scenarios
- Build passes with zero warnings

**Dependencies:** Phase 3 complete

---

### Phase 5 — Tests

**Goal:** Full unit and integration test coverage for every layer.

**Deliverables:**

- `tests/MoneyTransfer.Tests/MoneyTransfer.Tests.csproj` — xUnit test project targeting `net10.0`
- `tests/MoneyTransfer.Tests/GlobalUsings.cs` — `global using Xunit;`
- `tests/MoneyTransfer.Tests/Fixtures/TestData.cs` — shared test data factories
- `tests/MoneyTransfer.Tests/Fixtures/TestHelpers.cs` — builder helpers and Shouldly assertion utilities
- `tests/MoneyTransfer.Tests/UnitTests/Common/ResultTests.cs`
- `tests/MoneyTransfer.Tests/UnitTests/Models/MoneyTransferTests.cs`
- `tests/MoneyTransfer.Tests/UnitTests/Models/MoneyTransferBuilderTests.cs`
- `tests/MoneyTransfer.Tests/UnitTests/Services/TransferServiceTests.cs`
- `tests/MoneyTransfer.Tests/UnitTests/Validators/CreateTransferValidatorTests.cs`
- `tests/MoneyTransfer.Tests/UnitTests/Validators/ApproveRejectValidatorTests.cs`
- `tests/MoneyTransfer.Tests/IntegrationTests/TransferServiceIntegrationTests.cs`
- `MoneyTransfer.sln` updated to include both projects
- `src/MoneyTransfer.csproj` has `InternalsVisibleTo` for `MoneyTransfer.Tests` (allows access to `internal Build()`)
- 80 tests — all passing

**Dependencies:** Phase 4 complete

---

## Steps

### Phase 1 — Foundation

#### Step 1: Create MoneyTransfer.csproj

Create `MoneyTransfer.csproj` targeting `net10.0` with:

- `<LangVersion>latest</LangVersion>`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`
- `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- Package references:
    - `FluentValidation`
    - `FluentValidation.DependencyInjectionExtensions`
    - `Blazing.Extensions.DependencyInjection`

#### Step 2: Create Common/Result.cs

1. Create `Common/Result.cs` with `namespace MoneyTransfer.Common`
2. Implement sealed `Result` (non-generic): `IsSuccess`, `Error`, static `Ok()`, `Fail(string)`, `Ok<T>(T)`, `Fail<T>(string)` convenience overloads
3. Implement sealed `Result<T>`: `IsSuccess`, `Value`, `Error`, static `Ok(T)`, `Fail(string)`
4. No exception throwing anywhere in this type

#### Step 3: Create Models/Ids.cs

1. Create `Models/Ids.cs` with `namespace MoneyTransfer.Models`
2. Three `readonly record struct` types: `TransferId`, `AccountId`, `EmployeeId`
3. Each wraps a single `Guid Value` and exposes a static `New()` factory

#### Step 4: Create Models/TransferStatus.cs

1. Create `Models/TransferStatus.cs`
2. Six members: `Pending`, `PartlyApproved`, `Approved`, `Executed`, `Expired`, `Rejected`

---

### Phase 2 — Core Domain Model

#### Step 5: Create Models/MoneyTransfer.cs

1. Create `Models/MoneyTransfer.cs`
2. `internal` primary constructor accepting: `AccountId sourceAccountId`, `AccountId destinationAccountId`, `decimal amount`, `string currency`, `bool requiresApproval`, `DateTimeOffset expiresAt`
3. Set `Status = requiresApproval ? TransferStatus.Pending : TransferStatus.Approved` in the ctor body
4. Expose all properties with `{ get; private set; }` or `{ get; }` as appropriate
5. Implement `Approve(EmployeeId)`, `Execute(DateTimeOffset)`, `Reject(EmployeeId)`, `CheckExpiry(DateTimeOffset)` per the guard-clause table in Architecture
6. All methods return `Result` — no exceptions
7. Add XML documentation on class and every public member

#### Step 6: Create Models/MoneyTransferBuilder.cs

1. Create `Models/MoneyTransferBuilder.cs`
2. Private fields for each constructor parameter
3. Fluent methods `From`, `To`, `WithAmount`, `ExpiresAt`, `RequiresApproval` each returning `this`
4. `internal MoneyTransfer Build()` — calls `new MoneyTransfer(…)` with accumulated fields
5. XML docs on all public members

---

### Phase 3 — Validation + Service Layer

#### Step 7: Create Validators/CreateTransferValidator.cs

1. Create `Validators/CreateTransferValidator.cs`
2. Co-locate `sealed record CreateTransferCommand(AccountId SourceAccountId, AccountId DestinationAccountId, decimal Amount, string Currency, bool RequiresApproval, DateTimeOffset ExpiresAt)` in same file
3. Implement `sealed class CreateTransferValidator : AbstractValidator<CreateTransferCommand>`
4. Primary constructor injects `TimeProvider timeProvider`
5. Rules:
    - `SourceAccountId.Value` not empty
    - `DestinationAccountId.Value` not empty
    - Source ≠ Destination
    - `Amount > 0`
    - `Currency` must be in ISO-4217 `FrozenSet<string>` (static field)
    - `ExpiresAt > timeProvider.GetUtcNow()`
6. XML docs on record and class

#### Step 8: Create Validators/ApproveTransferValidator.cs

1. Co-locate `sealed record ApproveTransferCommand(EmployeeId EmployeeId)` in same file
2. `sealed class ApproveTransferValidator : AbstractValidator<ApproveTransferCommand>`
3. Rule: `EmployeeId.Value` not empty

#### Step 9: Create Validators/RejectTransferValidator.cs

1. Co-locate `sealed record RejectTransferCommand(EmployeeId EmployeeId)` in same file
2. `sealed class RejectTransferValidator : AbstractValidator<RejectTransferCommand>`
3. Rule: `EmployeeId.Value` not empty

#### Step 10: Create Services/ITransferService.cs

1. Create `Services/ITransferService.cs`
2. Define:
    - `Result<MoneyTransfer> Create(CreateTransferCommand command)`
    - `Result Approve(MoneyTransfer transfer, ApproveTransferCommand command)`
    - `Result Execute(MoneyTransfer transfer)`
    - `Result Reject(MoneyTransfer transfer, RejectTransferCommand command)`
    - `Result CheckExpiry(MoneyTransfer transfer)`
3. XML docs on interface and every member

#### Step 11: Create Services/TransferService.cs

1. Create `Services/TransferService.cs`
2. `[AutoRegister(ServiceLifetime.Singleton, typeof(ITransferService))]`
3. Primary constructor injecting: `TimeProvider`, `CreateTransferValidator`, `ApproveTransferValidator`, `RejectTransferValidator`
4. Each method: validate first → return `Result.Fail` with joined errors if invalid → delegate to builder/model
5. `Execute` and `CheckExpiry` have no command record — invoke model method directly with `timeProvider.GetUtcNow()`
6. XML docs

---

### Phase 4 — DI Wiring + Demo

#### Step 12: Create Program.cs

1. Create `Program.cs` with top-level statements
2. Construct `ApplicationHost` from `Blazing.Extensions.DependencyInjection`
3. Register:
    - `services.AddSingleton(TimeProvider.System)`
    - `services.AddValidatorsFromAssemblyContaining<Program>(ServiceLifetime.Singleton)`
    - `services.Register()` — scans `[AutoRegister]` in calling assembly
4. Resolve `ITransferService`
5. Demonstrate five scenarios:
    - **Auto-approved transfer** → `Create` (RequiresApproval=false) → `Execute` → assert `Executed`
    - **Two-approval transfer** → `Create` → `Approve(A)` → `PartlyApproved` → `Approve(B)` → `Approved` → `Execute` → `Executed`
    - **Same-employee double approval** → expect `Result.Fail`
    - **Reject from Pending** → assert `Rejected`
    - **Expired transfer** → set `ExpiresAt` in the past → `CheckExpiry` → assert `Expired`
6. Print each scenario result to console

#### Step 13: Build and Verify

```powershell
cd c:\wip\NET10\MoneyTransfer\src
dotnet build
```

Resolve all warnings/errors until `dotnet build` exits with code 0 and zero warnings.

---

### Phase 5 — Tests

#### Step 14: Create Test Project

1. Create `tests/MoneyTransfer.Tests/MoneyTransfer.Tests.csproj` targeting `net10.0`, `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`
2. Add package references: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Shouldly`, `Moq`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, `Microsoft.Extensions.DependencyInjection`, `coverlet.collector`, `coverlet.msbuild`
3. Add `<ProjectReference Include="..\..\src\MoneyTransfer.csproj" />`
4. Register test project in `MoneyTransfer.sln` via `dotnet sln add`

#### Step 15: Add InternalsVisibleTo

Add `<AssemblyAttribute>` in `src/MoneyTransfer.csproj` to expose internals to the test project:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
    <_Parameter1>MoneyTransfer.Tests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

This grants test access to `internal MoneyTransferBuilder.Build()`.

#### Step 16: Create Fixtures

1. Create `tests/MoneyTransfer.Tests/GlobalUsings.cs` — `global using Xunit;`
2. Create `tests/MoneyTransfer.Tests/Fixtures/TestData.cs` — static factory methods for valid `CreateTransferCommand`, `ApproveTransferCommand`, `RejectTransferCommand`, `AccountId`, and `EmployeeId` values
3. Create `tests/MoneyTransfer.Tests/Fixtures/TestHelpers.cs` — builder helpers (`BuildTransfer`, `BuildApprovedTransfer`) and shared assertion utilities

#### Step 17: Write Unit Tests — Common

Create `tests/MoneyTransfer.Tests/UnitTests/Common/ResultTests.cs`:

- `Result.Ok()` / `Result.Fail()` — `IsSuccess`, `Error` values
- `Result<T>.Ok(value)` / `Result<T>.Fail(error)` — `IsSuccess`, `Value`, `Error` values
- `Result.Ok<T>()` / `Result.Fail<T>()` convenience overloads

#### Step 18: Write Unit Tests — Models

Create `tests/MoneyTransfer.Tests/UnitTests/Models/MoneyTransferTests.cs`:

- All five state transitions via guard-clause table (happy paths + invariant violations)
- `Approve` — same employee second call returns `Result.Fail`
- `Execute` — past expiry returns `Result.Fail`
- `CheckExpiry` — idempotent; terminal states unchanged

Create `tests/MoneyTransfer.Tests/UnitTests/Models/MoneyTransferBuilderTests.cs`:

- Default build produces `Pending` when `RequiresApproval = true`
- Default build produces `Approved` when `RequiresApproval = false`
- All fluent setter methods propagate correctly to the built entity

#### Step 19: Write Unit Tests — Validators

Create `tests/MoneyTransfer.Tests/UnitTests/Validators/CreateTransferValidatorTests.cs`:

- Valid command passes
- `Amount ≤ 0` fails
- Invalid currency code fails (including custom ISO-4217 message check)
- Empty source / destination `AccountId` fails
- Source equals destination fails
- `ExpiresAt` in the past fails

Create `tests/MoneyTransfer.Tests/UnitTests/Validators/ApproveRejectValidatorTests.cs`:

- Valid `ApproveTransferCommand` passes
- Empty `EmployeeId` in approve command fails
- Valid `RejectTransferCommand` passes
- Empty `EmployeeId` in reject command fails

#### Step 20: Write Unit Tests — Services

Create `tests/MoneyTransfer.Tests/UnitTests/Services/TransferServiceTests.cs`:

- `Create` with valid command returns `Result.Ok` with correct entity
- `Create` with invalid command returns `Result.Fail` (validator fires before builder)
- `Approve` with invalid command returns `Result.Fail` (validator fires)
- `Approve` happy path delegates to model
- `Reject` happy path delegates to model
- `Execute` delegates to model
- `CheckExpiry` delegates to model
- `TimeProvider` is used for all time-based calls (injected via Moq substitute)

#### Step 21: Write Integration Tests

Create `tests/MoneyTransfer.Tests/IntegrationTests/TransferServiceIntegrationTests.cs`:

- Wire `ITransferService` through a real `ServiceCollection` (no mocks)
- Full happy path: auto-approved transfer → `Execute` → `Executed`
- Full happy path: dual-approval transfer → `Approve(A)` → `Approve(B)` → `Execute` → `Executed`
- Same-employee second approval fails
- Reject from `Pending` → `Rejected`
- Reject from `PartlyApproved` → `Rejected`
- Expired transfer → `CheckExpiry` → `Expired`
- Execute after expiry returns `Result.Fail`
- Create with invalid command returns validation errors

#### Step 22: Run Tests

```powershell
dotnet test c:\wip\NET10\MoneyTransfer\MoneyTransfer.sln --logger "console;verbosity=minimal"
```

All tests must pass: `Failed: 0, Passed: 80, Skipped: 0`.

---

## Checklist

### Phase 1 — Foundation

- [x] `src/MoneyTransfer.csproj` targets `net10.0`, `LangVersion=latest`, nullable enabled
- [x] `TreatWarningsAsErrors` set in project
- [x] `GenerateDocumentationFile` set in project
- [x] All three packages referenced: `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`, `Blazing.Extensions.DependencyInjection`
- [x] `src/Common/Result.cs` — `Result` and `Result<T>` implemented; no exceptions thrown
- [x] `src/Models/Ids.cs` — `TransferId`, `AccountId`, `EmployeeId` as `readonly record struct` with `New()` factory
- [x] `src/Models/TransferStatus.cs` — all six states present

### Phase 2 — Core Domain Model

- [x] `MoneyTransfer` ctor is `internal` (cannot be constructed outside assembly)
- [x] Initial `Status` is `Approved` when `requiresApproval = false`, `Pending` when `true`
- [x] `Approve` — Pending → PartlyApproved stores `FirstApproverId`
- [x] `Approve` — PartlyApproved + different employee → Approved stores `SecondApproverId`
- [x] `Approve` — same employee second call returns `Result.Fail`
- [x] `Approve` — called on any other status returns `Result.Fail`
- [x] `Execute` — Approved + `now < ExpiresAt` → Executed stores `ExecutedAt`
- [x] `Execute` — expired or wrong status returns `Result.Fail`
- [x] `Reject` — Pending or PartlyApproved → Rejected stores `RejectedById`
- [x] `Reject` — any other status returns `Result.Fail`
- [x] `CheckExpiry` — idempotent; terminal states unchanged; sets Expired when `now >= ExpiresAt`
- [x] `MoneyTransferBuilder` fluent methods all return `this`
- [x] `MoneyTransferBuilder.Build()` is `internal`

### Phase 3 — Validation + Service Layer

- [x] `CreateTransferValidator` validates: amount > 0, valid ISO-4217 currency, non-empty IDs, source ≠ destination, future expiry
- [x] ISO-4217 lookup uses `FrozenSet<string>` (static field, zero allocation)
- [x] `ApproveTransferValidator` validates `EmployeeId.Value` not empty
- [x] `RejectTransferValidator` validates `EmployeeId.Value` not empty
- [x] `ITransferService` defines all five methods returning `Result` / `Result<MoneyTransfer>`
- [x] `TransferService` decorated with `[AutoRegister(ServiceLifetime.Singleton, typeof(ITransferService))]`
- [x] Every `TransferService` method validates before touching the model
- [x] `TransferService` uses `TimeProvider.GetUtcNow()` — never `DateTime.UtcNow` or `DateTimeOffset.UtcNow` directly

### Phase 4 — DI Wiring + Demo

- [x] `src/Program.cs` registers `TimeProvider.System` as singleton
- [x] `AddValidatorsFromAssemblyContaining<Program>()` called with `ServiceLifetime.Singleton`
- [x] `services.Register()` scans `[AutoRegister]` types
- [x] `ITransferService` resolved from DI (not `new TransferService(...)`)
- [x] Demo exercises auto-approved happy path → `Status == Executed`
- [x] Demo exercises two-approval happy path → `Status == Executed`
- [x] Demo verifies same-employee second approval returns `Result.Fail`
- [x] Demo verifies reject from `Pending` returns `Status == Rejected`
- [x] Demo verifies expired transfer `CheckExpiry` sets `Status == Expired`

### Phase 5 — Tests

- [x] `tests/MoneyTransfer.Tests/MoneyTransfer.Tests.csproj` created and registered in `MoneyTransfer.sln`
- [x] `ProjectReference` points to `..\..\src\MoneyTransfer.csproj`
- [x] `src/MoneyTransfer.csproj` has `InternalsVisibleTo` for `MoneyTransfer.Tests`
- [x] `GlobalUsings.cs` — `global using Xunit;`
- [x] `Fixtures/TestData.cs` — shared valid command factories
- [x] `Fixtures/TestHelpers.cs` — builder helpers and assertion utilities
- [x] `UnitTests/Common/ResultTests.cs` — Result / Result<T> unit tests
- [x] `UnitTests/Models/MoneyTransferTests.cs` — state transition and guard-clause tests
- [x] `UnitTests/Models/MoneyTransferBuilderTests.cs` — fluent builder construction tests
- [x] `UnitTests/Services/TransferServiceTests.cs` — service orchestration tests (Moq)
- [x] `UnitTests/Validators/CreateTransferValidatorTests.cs` — CreateTransferCommand validation tests
- [x] `UnitTests/Validators/ApproveRejectValidatorTests.cs` — Approve/Reject validation tests
- [x] `IntegrationTests/TransferServiceIntegrationTests.cs` — end-to-end lifecycle scenarios
- [x] `dotnet test MoneyTransfer.sln` → Failed: 0, Passed: 80, Skipped: 0

### Code Quality

- [x] No `throw` statements anywhere in the codebase
- [x] No `new Regex(...)` — no regex needed here; `FrozenSet` used for currency lookup
- [x] XML documentation on every public type, method, and property
- [x] Nullable reference types respected; `!` operator not used
- [x] C# 14 primary constructors used throughout (no manual backing-field boilerplate)
- [x] No redundant `using` directives
- [x] `dotnet build` exits with code 0, zero warnings, zero errors

---

## Progress Updates

### 2026-04-16 — Session 1 (Initial Implementation)

- ✅ Completed: Phase 1 — Foundation (`Result<T>`, `Ids`, `TransferStatus`, project file)
- ✅ Completed: Phase 2 — Core Domain Model (`MoneyTransfer`, `MoneyTransferBuilder`)
- ✅ Completed: Phase 3 — Validation + Service Layer (all three validators + `ITransferService` + `TransferService`)
- ✅ Completed: Phase 4 — DI Wiring + Demo (`Program.cs` wired, all five scenarios exercised)
- ✅ Completed: Docs — `docs/architecture.md`, `docs/use-cases.md`, `docs/flow.md`
- 📋 Next: Add unit and integration tests (Phase 5)

### 2026-04-16 — Session 2 (Tests + src/ restructure)

- ✅ Completed: Source files moved into `src/` folder (`Common/`, `Models/`, `Validators/`, `Services/`, `Program.cs`, `MoneyTransfer.csproj`)
- ✅ Completed: `MoneyTransfer.sln` updated — `src\MoneyTransfer.csproj` + test project registered
- ✅ Completed: Phase 5 — Test project created (`tests/MoneyTransfer.Tests/`)
  - `MoneyTransfer.Tests.csproj` with xUnit, Shouldly, Moq, coverlet
  - `GlobalUsings.cs` — `global using Xunit;`
  - `Fixtures/TestData.cs` + `Fixtures/TestHelpers.cs`
  - 6 unit test files + 1 integration test file
- ✅ Completed: `InternalsVisibleTo` added to `src/MoneyTransfer.csproj` (grants test project access to `internal Build()`)
- ✅ Completed: `dotnet test MoneyTransfer.sln` → **Failed: 0, Passed: 80, Skipped: 0, Duration: ~200 ms**
- 📋 Next: None — all phases complete

# Flow

## Transfer Lifecycle State Machine

```mermaid
stateDiagram-v2
    [*] --> Pending : Create (RequiresApproval = true)
    [*] --> Approved : Create (RequiresApproval = false)

    Pending --> PartlyApproved : Approve (Employee A)
    Pending --> Rejected : Reject (any employee)
    Pending --> Expired : CheckExpiry (now ≥ ExpiresAt)

    PartlyApproved --> Approved : Approve (Employee B ≠ Employee A)
    PartlyApproved --> Rejected : Reject (any employee)
    PartlyApproved --> Expired : CheckExpiry (now ≥ ExpiresAt)

    Approved --> Executed : Execute (now < ExpiresAt)
    Approved --> Expired : CheckExpiry (now ≥ ExpiresAt)

    Executed --> [*]
    Expired --> [*]
    Rejected --> [*]
```

---

## Happy Path — Auto-Approved Transfer

```mermaid
flowchart TD
    A([Start]) --> B["Create transfer\nRequiresApproval = false"]
    B --> C{Validation OK?}
    C -- No --> Z1([Result.Fail — validation errors])
    C -- Yes --> D["Build MoneyTransfer\nStatus = Approved"]
    D --> E["Execute transfer\nnow < ExpiresAt?"]
    E -- No --> Z2([Result.Fail — expired])
    E -- Yes --> F["Status = Executed\nExecutedAt = now"]
    F --> G([Result.Ok])
```

---

## Happy Path — Dual-Approval Transfer

```mermaid
flowchart TD
    A([Start]) --> B["Create transfer\nRequiresApproval = true"]
    B --> C{Validation OK?}
    C -- No --> Z1([Result.Fail — validation errors])
    C -- Yes --> D["Build MoneyTransfer\nStatus = Pending"]

    D --> E["Approve — Employee A"]
    E --> F{Validation OK?}
    F -- No --> Z2([Result.Fail — validation errors])
    F -- Yes --> G{CheckExpiry}
    G -- Expired --> Z3([Status = Expired])
    G -- Not expired --> H["Status = PartlyApproved\nFirstApproverId = A"]

    H --> I["Approve — Employee B"]
    I --> J{Validation OK?}
    J -- No --> Z4([Result.Fail — validation errors])
    J -- Yes --> K{B ≠ A?}
    K -- No  --> Z5([Result.Fail — same employee])
    K -- Yes --> L["Status = Approved\nSecondApproverId = B"]

    L --> M["Execute transfer\nnow < ExpiresAt?"]
    M -- No --> Z6([Result.Fail — expired])
    M -- Yes --> N["Status = Executed\nExecutedAt = now"]
    N --> O([Result.Ok])
```

---

## Rejection Flow

```mermaid
flowchart TD
    A([Transfer in Pending or PartlyApproved]) --> B["Reject(employeeId)"]
    B --> C{Validation OK?}
    C -- No --> Z1([Result.Fail — validation errors])
    C -- Yes --> D{Status is Pending\nor PartlyApproved?}
    D -- No --> Z2([Result.Fail — wrong status])
    D -- Yes --> E["Status = Rejected\nRejectedById = employee"]
    E --> F([Result.Ok])
```

---

## Expiry Flow

```mermaid
flowchart TD
    A([CheckExpiry called]) --> B{Already terminal?\nExecuted / Expired / Rejected}
    B -- Yes --> C([Result.Ok — no change])
    B -- No  --> D{now ≥ ExpiresAt?}
    D -- No  --> E([Result.Ok — no change])
    D -- Yes --> F["Status = Expired"]
    F --> G([Result.Ok])
```

---

## Service Validation Flow (All Operations)

```mermaid
flowchart LR
    subgraph ServiceBoundary["TransferService (boundary)"]
        direction TB
        V["FluentValidation\nValidate(command)"]
        G{IsValid?}
        D["Delegate to\ndomain model"]
        E["Return\nResult.Fail\n(joined errors)"]
    end

    Caller -->|"command"| V
    V --> G
    G -- Yes --> D
    G -- No  --> E
    D -->|"Result"| Caller
    E -->|"Result.Fail"| Caller
```

---

## Data Flow — Create Transfer

```mermaid
flowchart LR
    Caller -->|"CreateTransferCommand"| TS["TransferService\n.Create()"]
    TS -->|"Validate()"| CV["CreateTransferValidator"]
    CV -->|"ValidationResult"| TS
    TS -->|"From/To/WithAmount\n/ExpiresAt/RequiresApproval\n.Build()"| MB["MoneyTransferBuilder"]
    MB -->|"new MoneyTransfer(…)"| MT["MoneyTransfer\n(internal ctor)"]
    MT -->|"instance"| MB
    MB -->|"transfer"| TS
    TS -->|"Result.Ok(transfer)"| Caller
```

---

## Guard Clause Decision Table

| State on entry                             | Operation          | Condition                | Outcome                                   |
| ------------------------------------------ | ------------------ | ------------------------ | ----------------------------------------- |
| `Pending`                                  | `Approve(emp)`     | —                        | `PartlyApproved`; store `FirstApproverId` |
| `PartlyApproved`                           | `Approve(emp)`     | `emp ≠ FirstApproverId`  | `Approved`; store `SecondApproverId`      |
| `PartlyApproved`                           | `Approve(emp)`     | `emp == FirstApproverId` | `Result.Fail`                             |
| Any other                                  | `Approve(emp)`     | —                        | `Result.Fail`                             |
| `Approved`                                 | `Execute(now)`     | `now < ExpiresAt`        | `Executed`; store `ExecutedAt`            |
| `Approved`                                 | `Execute(now)`     | `now ≥ ExpiresAt`        | `Result.Fail`                             |
| Any other                                  | `Execute(now)`     | —                        | `Result.Fail`                             |
| `Pending` or `PartlyApproved`              | `Reject(emp)`      | —                        | `Rejected`; store `RejectedById`          |
| Any other                                  | `Reject(emp)`      | —                        | `Result.Fail`                             |
| Terminal (`Executed`/`Expired`/`Rejected`) | `CheckExpiry(now)` | —                        | `Result.Ok` (no change)                   |
| Non-terminal                               | `CheckExpiry(now)` | `now ≥ ExpiresAt`        | `Expired`                                 |
| Non-terminal                               | `CheckExpiry(now)` | `now < ExpiresAt`        | `Result.Ok` (no change)                   |

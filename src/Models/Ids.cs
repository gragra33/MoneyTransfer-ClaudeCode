namespace MoneyTransfer.Models;

/// <summary>Strongly typed identifier for a money transfer.</summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct TransferId(Guid Value)
{
    /// <summary>Creates a new <see cref="TransferId"/> with a randomly generated GUID.</summary>
    public static TransferId New() => new(Guid.NewGuid());
}

/// <summary>Strongly typed identifier for a bank account.</summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct AccountId(Guid Value)
{
    /// <summary>Creates a new <see cref="AccountId"/> with a randomly generated GUID.</summary>
    public static AccountId New() => new(Guid.NewGuid());
}

/// <summary>Strongly typed identifier for an employee.</summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct EmployeeId(Guid Value)
{
    /// <summary>Creates a new <see cref="EmployeeId"/> with a randomly generated GUID.</summary>
    public static EmployeeId New() => new(Guid.NewGuid());
}

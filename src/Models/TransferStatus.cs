namespace MoneyTransfer.Models;

/// <summary>Lifecycle states of a money transfer.</summary>
public enum TransferStatus
{
    /// <summary>Transfer is awaiting the first approval.</summary>
    Pending,

    /// <summary>Transfer has received one of the two required approvals.</summary>
    PartlyApproved,

    /// <summary>Transfer is fully approved and may be executed.</summary>
    Approved,

    /// <summary>Transfer has been executed and settlement is complete.</summary>
    Executed,

    /// <summary>Transfer was not executed before its expiration timestamp.</summary>
    Expired,

    /// <summary>Transfer was rejected by an employee.</summary>
    Rejected
}

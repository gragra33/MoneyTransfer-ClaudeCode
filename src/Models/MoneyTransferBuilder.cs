namespace MoneyTransfer.Models;

/// <summary>
/// Fluent builder for constructing a <see cref="MoneyTransfer"/> instance.
/// Call <see cref="From"/>, <see cref="To"/>, <see cref="WithAmount"/>,
/// <see cref="ExpiresAt"/>, and <see cref="RequiresApproval"/> before calling
/// <see cref="Build"/>.
/// </summary>
public sealed class MoneyTransferBuilder
{
    private AccountId _source;
    private AccountId _dest;
    private decimal _amount;
    private string _currency = string.Empty;
    private DateTimeOffset _expiresAt;
    private bool _requiresApproval;

    /// <summary>Sets the source account identifier.</summary>
    /// <param name="source">The account from which funds are debited.</param>
    public MoneyTransferBuilder From(AccountId source) { _source = source; return this; }

    /// <summary>Sets the destination account identifier.</summary>
    /// <param name="dest">The account to which funds are credited.</param>
    public MoneyTransferBuilder To(AccountId dest) { _dest = dest; return this; }

    /// <summary>Sets the monetary amount and ISO-4217 currency code.</summary>
    /// <param name="amount">A strictly positive decimal amount.</param>
    /// <param name="currency">A valid ISO-4217 currency code (e.g. <c>"USD"</c>).</param>
    public MoneyTransferBuilder WithAmount(decimal amount, string currency)
    {
        _amount = amount;
        _currency = currency;
        return this;
    }

    /// <summary>Sets the UTC timestamp after which the transfer expires.</summary>
    /// <param name="at">A future <see cref="DateTimeOffset"/> in UTC.</param>
    public MoneyTransferBuilder ExpiresAt(DateTimeOffset at) { _expiresAt = at; return this; }

    /// <summary>Controls whether two-employee approval is required before execution.</summary>
    /// <param name="value">
    /// <c>true</c> to require dual approval (initial status <see cref="TransferStatus.Pending"/>);
    /// <c>false</c> for auto-approval (initial status <see cref="TransferStatus.Approved"/>).
    /// </param>
    public MoneyTransferBuilder RequiresApproval(bool value) { _requiresApproval = value; return this; }

    /// <summary>
    /// Constructs the <see cref="MoneyTransfer"/> from the accumulated values.
    /// The caller is responsible for validating all inputs before calling this method.
    /// </summary>
    internal MoneyTransfer Build() =>
        new(_source, _dest, _amount, _currency, _requiresApproval, _expiresAt);
}

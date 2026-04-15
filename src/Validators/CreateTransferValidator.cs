using System.Collections.Frozen;
using FluentValidation;
using MoneyTransfer.Models;

namespace MoneyTransfer.Validators;

/// <summary>Command for creating a new money transfer.</summary>
/// <param name="SourceAccountId">The account from which funds are debited.</param>
/// <param name="DestinationAccountId">The account to which funds are credited.</param>
/// <param name="Amount">A strictly positive decimal monetary amount.</param>
/// <param name="Currency">A valid ISO-4217 currency code.</param>
/// <param name="RequiresApproval">
/// <c>true</c> if two-employee approval is required; <c>false</c> for auto-approval.
/// </param>
/// <param name="ExpiresAt">The UTC timestamp after which the transfer expires.</param>
public sealed record CreateTransferCommand(
    AccountId SourceAccountId,
    AccountId DestinationAccountId,
    decimal Amount,
    string Currency,
    bool RequiresApproval,
    DateTimeOffset ExpiresAt);

/// <summary>Validates a <see cref="CreateTransferCommand"/> before any domain logic runs.</summary>
public sealed class CreateTransferValidator : AbstractValidator<CreateTransferCommand>
{
    // Full ISO-4217 active currency codes — FrozenSet gives O(1) lookup with zero allocation.
    private static readonly FrozenSet<string> ValidCurrencies = new[]
    {
        "AED","AFN","ALL","AMD","ANG","AOA","ARS","AUD","AWG","AZN",
        "BAM","BBD","BDT","BGN","BHD","BIF","BMD","BND","BOB","BRL",
        "BSD","BTN","BWP","BYN","BZD","CAD","CDF","CHF","CLP","CNY",
        "COP","CRC","CUP","CVE","CZK","DJF","DKK","DOP","DZD","EGP",
        "ERN","ETB","EUR","FJD","FKP","FOK","GBP","GEL","GGP","GHS",
        "GIP","GMD","GNF","GTQ","GYD","HKD","HNL","HTG","HUF",
        "IDR","ILS","IMP","INR","IQD","IRR","ISK","JEP","JMD","JOD",
        "JPY","KES","KGS","KHR","KID","KMF","KRW","KWD","KYD","KZT",
        "LAK","LBP","LKR","LRD","LSL","LYD","MAD","MDL","MGA","MKD",
        "MMK","MNT","MOP","MRU","MUR","MVR","MWK","MXN","MYR","MZN",
        "NAD","NGN","NIO","NOK","NPR","NZD","OMR","PAB","PEN","PGK",
        "PHP","PKR","PLN","PYG","QAR","RON","RSD","RUB","RWF","SAR",
        "SBD","SCR","SDG","SEK","SGD","SHP","SLE","SOS","SRD",
        "SSP","STN","SYP","SZL","THB","TJS","TMT","TND","TOP","TRY",
        "TTD","TVD","TWD","TZS","UAH","UGX","USD","UYU","UZS","VES",
        "VND","VUV","WST","XAF","XCD","XOF","XPF","YER","ZAR","ZMW","ZWL"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new <see cref="CreateTransferValidator"/>.</summary>
    /// <param name="timeProvider">Provides the current UTC time for expiry validation.</param>
    public CreateTransferValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.SourceAccountId.Value)
            .NotEmpty()
            .WithMessage("Source account ID must not be empty.");

        RuleFor(x => x.DestinationAccountId.Value)
            .NotEmpty()
            .WithMessage("Destination account ID must not be empty.");

        RuleFor(x => x.SourceAccountId)
            .NotEqual(x => x.DestinationAccountId)
            .WithMessage("Source and destination accounts must be different.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be a positive value.");

        RuleFor(x => x.Currency)
            .Must(c => ValidCurrencies.Contains(c))
            .WithMessage(x => $"'{x.Currency}' is not a recognised ISO-4217 currency code.");

        RuleFor(x => x.ExpiresAt)
            .Must(e => e > timeProvider.GetUtcNow())
            .WithMessage("Expiration timestamp must be in the future.");
    }
}

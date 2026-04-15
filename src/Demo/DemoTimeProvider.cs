namespace MoneyTransfer.Demo;

/// <summary>Provides deterministic time control for demo scenarios.</summary>
internal sealed class DemoTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    /// <summary>Advances the current demo time by the specified duration.</summary>
    /// <param name="duration">The amount of time to advance.</param>
    public void Advance(TimeSpan duration) => _now += duration;

    /// <summary>Gets the current UTC time used by the demo.</summary>
    /// <returns>The current demo UTC timestamp.</returns>
    public override DateTimeOffset GetUtcNow() => _now;
}

namespace NServiceBusContrib.WarmUp.Tests;

/// <summary>A <see cref="TimeProvider"/> with a manually controllable current time.</summary>
sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}

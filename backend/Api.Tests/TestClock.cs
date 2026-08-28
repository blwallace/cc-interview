namespace Api.Tests;

/// <summary>
/// A <see cref="TimeProvider"/> whose "now" is fixed and settable, so rules that depend on the
/// current time (no booking in the past, "active" reservations) are testable without sleeping.
/// </summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

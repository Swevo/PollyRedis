namespace PollyRedis.Tests;

/// <summary>
/// Helpers to build a <see cref="ResilientRedisDatabase"/> wired to a mock <see cref="IDatabase"/>
/// with fast (zero/minimal) delays so tests complete quickly.
/// </summary>
internal static class TestFactory
{
    /// <summary>Fast options: zero delays, high CB throughput to avoid accidental tripping.</summary>
    public static PollyRedisOptions FastOptions(Action<PollyRedisOptions>? configure = null)
    {
        var options = new PollyRedisOptions
        {
            BaseDelay = TimeSpan.Zero,
            MaxDelay = TimeSpan.Zero,
            CommandTimeout = TimeSpan.FromSeconds(10),
            CircuitBreakerMinimumThroughput = 100,
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10),
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(1),
        };
        configure?.Invoke(options);
        return options;
    }

    public static ResilientRedisDatabase Create(IDatabase db, Action<PollyRedisOptions>? configure = null)
        => new(db, FastOptions(configure));
}

namespace PollyRedis;

/// <summary>
/// Extension methods for <see cref="IConnectionMultiplexer"/> to create a
/// <see cref="IResilientRedisDatabase"/> with Polly v8 resilience.
/// </summary>
public static class ConnectionMultiplexerExtensions
{
    /// <summary>
    /// Returns an <see cref="IResilientRedisDatabase"/> that wraps the default
    /// <see cref="IDatabase"/> with retry, circuit breaker, and timeout.
    /// </summary>
    /// <param name="multiplexer">The Redis connection multiplexer.</param>
    /// <param name="configure">Optional delegate to customise resilience options.</param>
    /// <param name="db">The database index to use. Defaults to -1 (server default).</param>
    public static IResilientRedisDatabase GetResilientDatabase(
        this IConnectionMultiplexer multiplexer,
        Action<PollyRedisOptions>? configure = null,
        int db = -1)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);

        var options = new PollyRedisOptions();
        configure?.Invoke(options);

        return new ResilientRedisDatabase(multiplexer.GetDatabase(db), options);
    }
}

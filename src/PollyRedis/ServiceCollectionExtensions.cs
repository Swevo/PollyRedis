namespace PollyRedis;

/// <summary>
/// Extension methods for registering <see cref="IResilientRedisDatabase"/> in
/// the <see cref="IServiceCollection"/> dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IResilientRedisDatabase"/> as a singleton by wrapping the
    /// <see cref="IConnectionMultiplexer"/> already registered in the container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional delegate to customise resilience options.</param>
    /// <param name="db">The Redis database index to use. Defaults to -1 (server default).</param>
    /// <remarks>
    /// Requires <see cref="IConnectionMultiplexer"/> to be registered before calling this method
    /// (e.g. via <c>services.AddSingleton&lt;IConnectionMultiplexer&gt;(ConnectionMultiplexer.Connect(...))</c>).
    /// </remarks>
    public static IServiceCollection AddPollyRedis(
        this IServiceCollection services,
        Action<PollyRedisOptions>? configure = null,
        int db = -1)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddSingleton<IResilientRedisDatabase>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            return multiplexer.GetResilientDatabase(configure, db);
        });
    }

    /// <summary>
    /// Registers <see cref="IResilientRedisDatabase"/> as a singleton using the provided
    /// <see cref="IConnectionMultiplexer"/> instance directly.
    /// </summary>
    public static IServiceCollection AddPollyRedis(
        this IServiceCollection services,
        IConnectionMultiplexer multiplexer,
        Action<PollyRedisOptions>? configure = null,
        int db = -1)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(multiplexer);

        return services.AddSingleton<IResilientRedisDatabase>(
            multiplexer.GetResilientDatabase(configure, db));
    }
}

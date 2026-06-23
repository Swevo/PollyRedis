namespace PollyRedis;

/// <summary>
/// Wraps an <see cref="IDatabase"/> with a Polly v8 resilience pipeline:
/// retry with exponential back-off, circuit breaker, and per-command timeout.
/// </summary>
public sealed class ResilientRedisDatabase : IResilientRedisDatabase
{
    private readonly IDatabase _db;
    private readonly ResiliencePipeline _pipeline;

    /// <summary>
    /// Creates a new <see cref="ResilientRedisDatabase"/> with the given database and options.
    /// </summary>
    public ResilientRedisDatabase(IDatabase db, PollyRedisOptions options)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(options);
        _db = db;
        _pipeline = BuildPipeline(options);
    }

    // ── Core execution ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<T> ExecuteAsync<T>(Func<IDatabase, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return _pipeline.ExecuteAsync(async ct =>
        {
            // WaitAsync propagates task exceptions normally; throws TaskCanceledException
            // (caught by Polly as TimeoutRejectedException) when the timeout token fires.
            var task = operation(_db);
            await task.WaitAsync(ct).ConfigureAwait(false);
            return task.Result;
        }, cancellationToken).AsTask();
    }

    // ── String ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.StringGetAsync(key, flags));

    /// <inheritdoc />
    public Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.StringSetAsync(key, value,
            expiry.HasValue ? (Expiration)expiry.Value : Expiration.Default,
            flags: flags));

    /// <inheritdoc />
    public Task<long> StringIncrementAsync(RedisKey key, long value = 1L, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.StringIncrementAsync(key, value, flags));

    /// <inheritdoc />
    public Task<long> StringDecrementAsync(RedisKey key, long value = 1L, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.StringDecrementAsync(key, value, flags));

    // ── Key ───────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<bool> KeyExistsAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.KeyExistsAsync(key, flags));

    /// <inheritdoc />
    public Task<bool> KeyDeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.KeyDeleteAsync(key, flags));

    /// <inheritdoc />
    public Task<bool> KeyExpireAsync(RedisKey key, TimeSpan? expiry, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.KeyExpireAsync(key, expiry, flags: flags));

    // ── Hash ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<RedisValue> HashGetAsync(RedisKey key, RedisValue hashField, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.HashGetAsync(key, hashField, flags));

    /// <inheritdoc />
    public Task<bool> HashSetAsync(RedisKey key, RedisValue hashField, RedisValue value, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.HashSetAsync(key, hashField, value, flags: flags));

    /// <inheritdoc />
    public Task<HashEntry[]> HashGetAllAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.HashGetAllAsync(key, flags));

    /// <inheritdoc />
    public Task<bool> HashDeleteAsync(RedisKey key, RedisValue hashField, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.HashDeleteAsync(key, hashField, flags));

    // ── List ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<long> ListLeftPushAsync(RedisKey key, RedisValue value, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.ListLeftPushAsync(key, value, flags: flags));

    /// <inheritdoc />
    public Task<RedisValue> ListRightPopAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.ListRightPopAsync(key, flags));

    /// <inheritdoc />
    public Task<long> ListLengthAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.ListLengthAsync(key, flags));

    // ── Set ───────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<bool> SetAddAsync(RedisKey key, RedisValue value, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.SetAddAsync(key, value, flags));

    /// <inheritdoc />
    public Task<bool> SetRemoveAsync(RedisKey key, RedisValue value, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.SetRemoveAsync(key, value, flags));

    /// <inheritdoc />
    public Task<bool> SetContainsAsync(RedisKey key, RedisValue value, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.SetContainsAsync(key, value, flags));

    // ── Sorted Set ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<bool> SortedSetAddAsync(RedisKey key, RedisValue member, double score, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.SortedSetAddAsync(key, member, score, flags: flags));

    /// <inheritdoc />
    public Task<double?> SortedSetScoreAsync(RedisKey key, RedisValue member, CommandFlags flags = CommandFlags.None)
        => ExecuteAsync(db => db.SortedSetScoreAsync(key, member, flags));

    // ── Pipeline builder ──────────────────────────────────────────────────────

    private static ResiliencePipeline BuildPipeline(PollyRedisOptions options)
    {
        var predicateBuilder = new PredicateBuilder()
            .Handle<RedisConnectionException>()
            .Handle<RedisTimeoutException>();

        foreach (var type in options.AdditionalTransientExceptions)
            predicateBuilder.Handle<Exception>(ex => type.IsInstanceOfType(ex));

        var builder = new ResiliencePipelineBuilder();

        if (options.MaxRetries >= 1)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = predicateBuilder,
                MaxRetryAttempts = options.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = options.BaseDelay,
                MaxDelay = options.MaxDelay,
            });
        }

        builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = predicateBuilder,
                FailureRatio = options.CircuitBreakerFailureRatio,
                MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                BreakDuration = options.CircuitBreakerBreakDuration,
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.CommandTimeout,
            });

        return builder.Build();
    }
}

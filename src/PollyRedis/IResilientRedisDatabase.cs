namespace PollyRedis;

/// <summary>
/// A resilient facade over <see cref="IDatabase"/> that wraps every command in a
/// Polly v8 pipeline (retry, circuit breaker, timeout).
/// </summary>
public interface IResilientRedisDatabase
{
    // ── String ────────────────────────────────────────────────────────────────

    /// <inheritdoc cref="IDatabaseAsync.StringGetAsync(RedisKey,CommandFlags)"/>
    Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.StringSetAsync(RedisKey,RedisValue,TimeSpan?,bool,When,CommandFlags)"/>
    Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry = null, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.StringIncrementAsync(RedisKey,long,CommandFlags)"/>
    Task<long> StringIncrementAsync(RedisKey key, long value = 1L, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.StringDecrementAsync(RedisKey,long,CommandFlags)"/>
    Task<long> StringDecrementAsync(RedisKey key, long value = 1L, CommandFlags flags = CommandFlags.None);

    // ── Key ───────────────────────────────────────────────────────────────────

    /// <inheritdoc cref="IDatabaseAsync.KeyExistsAsync(RedisKey,CommandFlags)"/>
    Task<bool> KeyExistsAsync(RedisKey key, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.KeyDeleteAsync(RedisKey,CommandFlags)"/>
    Task<bool> KeyDeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.KeyExpireAsync(RedisKey,TimeSpan?,ExpireWhen,CommandFlags)"/>
    Task<bool> KeyExpireAsync(RedisKey key, TimeSpan? expiry, CommandFlags flags = CommandFlags.None);

    // ── Hash ──────────────────────────────────────────────────────────────────

    /// <inheritdoc cref="IDatabaseAsync.HashGetAsync(RedisKey,RedisValue,CommandFlags)"/>
    Task<RedisValue> HashGetAsync(RedisKey key, RedisValue hashField, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.HashSetAsync(RedisKey,RedisValue,RedisValue,When,CommandFlags)"/>
    Task<bool> HashSetAsync(RedisKey key, RedisValue hashField, RedisValue value, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.HashGetAllAsync(RedisKey,CommandFlags)"/>
    Task<HashEntry[]> HashGetAllAsync(RedisKey key, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.HashDeleteAsync(RedisKey,RedisValue,CommandFlags)"/>
    Task<bool> HashDeleteAsync(RedisKey key, RedisValue hashField, CommandFlags flags = CommandFlags.None);

    // ── List ──────────────────────────────────────────────────────────────────

    /// <inheritdoc cref="IDatabaseAsync.ListLeftPushAsync(RedisKey,RedisValue,When,CommandFlags)"/>
    Task<long> ListLeftPushAsync(RedisKey key, RedisValue value, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.ListRightPopAsync(RedisKey,CommandFlags)"/>
    Task<RedisValue> ListRightPopAsync(RedisKey key, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.ListLengthAsync(RedisKey,CommandFlags)"/>
    Task<long> ListLengthAsync(RedisKey key, CommandFlags flags = CommandFlags.None);

    // ── Set ───────────────────────────────────────────────────────────────────

    /// <inheritdoc cref="IDatabaseAsync.SetAddAsync(RedisKey,RedisValue,CommandFlags)"/>
    Task<bool> SetAddAsync(RedisKey key, RedisValue value, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.SetRemoveAsync(RedisKey,RedisValue,CommandFlags)"/>
    Task<bool> SetRemoveAsync(RedisKey key, RedisValue value, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.SetContainsAsync(RedisKey,RedisValue,CommandFlags)"/>
    Task<bool> SetContainsAsync(RedisKey key, RedisValue value, CommandFlags flags = CommandFlags.None);

    // ── Sorted Set ────────────────────────────────────────────────────────────

    /// <inheritdoc cref="IDatabaseAsync.SortedSetAddAsync(RedisKey,RedisValue,double,SortedSetWhen,CommandFlags)"/>
    Task<bool> SortedSetAddAsync(RedisKey key, RedisValue member, double score, CommandFlags flags = CommandFlags.None);

    /// <inheritdoc cref="IDatabaseAsync.SortedSetScoreAsync(RedisKey,RedisValue,CommandFlags)"/>
    Task<double?> SortedSetScoreAsync(RedisKey key, RedisValue member, CommandFlags flags = CommandFlags.None);

    // ── Escape hatch ──────────────────────────────────────────────────────────

    /// <summary>
    /// Executes any arbitrary operation against the underlying <see cref="IDatabase"/>
    /// wrapped in the Polly resilience pipeline.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<IDatabase, Task<T>> operation, CancellationToken cancellationToken = default);
}

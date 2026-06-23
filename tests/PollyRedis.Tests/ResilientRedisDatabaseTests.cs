namespace PollyRedis.Tests;

public class ResilientRedisDatabaseTests
{
    // ── ExecuteAsync — success ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SuccessOnFirstAttempt_ReturnsValue()
    {
        var db = Substitute.For<IDatabase>();
        var sut = TestFactory.Create(db);

        var result = await sut.ExecuteAsync(_ => Task.FromResult(42));

        result.Should().Be(42);
    }

    // ── Retry on RedisConnectionException ─────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RetriesOnConnectionException_Succeeds()
    {
        int calls = 0;
        var db = Substitute.For<IDatabase>();
        var sut = TestFactory.Create(db, o => o.MaxRetries = 3);

        var result = await sut.ExecuteAsync<string>(_ =>
        {
            calls++;
            if (calls < 3) throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test");
            return Task.FromResult("ok");
        });

        result.Should().Be("ok");
        calls.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsRetries_ThrowsConnectionException()
    {
        var db = Substitute.For<IDatabase>();
        var sut = TestFactory.Create(db, o =>
        {
            o.MaxRetries = 2;
            o.CircuitBreakerMinimumThroughput = 100;
        });

        var act = () => sut.ExecuteAsync<string>(_ =>
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test"));

        await act.Should().ThrowAsync<RedisConnectionException>();
    }

    // ── Retry on RedisTimeoutException ─────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RetriesOnTimeoutException_Succeeds()
    {
        int calls = 0;
        var db = Substitute.For<IDatabase>();
        var sut = TestFactory.Create(db, o => o.MaxRetries = 3);

        var result = await sut.ExecuteAsync<string>(_ =>
        {
            calls++;
            if (calls < 2) throw new RedisTimeoutException("timeout", CommandStatus.Unknown);
            return Task.FromResult("done");
        });

        result.Should().Be("done");
        calls.Should().Be(2);
    }

    // ── Non-transient exception — not retried ──────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NonTransientException_NotRetried()
    {
        int calls = 0;
        var db = Substitute.For<IDatabase>();
        var sut = TestFactory.Create(db, o => o.MaxRetries = 3);

        var act = () => sut.ExecuteAsync<string>(_ =>
        {
            calls++;
            throw new RedisServerException("WRONGTYPE");
        });

        await act.Should().ThrowAsync<RedisServerException>();
        calls.Should().Be(1); // no retry
    }

    // ── Additional transient exceptions ───────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AdditionalTransientException_IsRetried()
    {
        int calls = 0;
        var db = Substitute.For<IDatabase>();
        var sut = TestFactory.Create(db, o =>
        {
            o.MaxRetries = 2;
            o.AdditionalTransientExceptions.Add(typeof(InvalidOperationException));
            o.CircuitBreakerMinimumThroughput = 100;
        });

        var result = await sut.ExecuteAsync<string>(_ =>
        {
            calls++;
            if (calls < 2) throw new InvalidOperationException("transient");
            return Task.FromResult("ok");
        });

        result.Should().Be("ok");
        calls.Should().Be(2);
    }

    // ── Circuit breaker ────────────────────────────────────────────────────

    [Fact]
    public async Task CircuitBreaker_OpensAfterThreshold()
    {
        var db = Substitute.For<IDatabase>();
        var sut = TestFactory.Create(db, o =>
        {
            o.MaxRetries = 0;
            o.CircuitBreakerMinimumThroughput = 3;
            o.CircuitBreakerFailureRatio = 0.5;
            o.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10);
            o.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(10);
        });

        var exceptions = new List<Exception>();
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await sut.ExecuteAsync<string>(_ =>
                    throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test"));
            }
            catch (Exception ex) { exceptions.Add(ex); }
        }

        exceptions.Should().Contain(e => e is BrokenCircuitException);
    }

    // ── Timeout ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Timeout_Throws()
    {
        var db = Substitute.For<IDatabase>();
        var options = new PollyRedisOptions
        {
            MaxRetries = 0,
            CommandTimeout = TimeSpan.FromMilliseconds(50),
            CircuitBreakerMinimumThroughput = 100,
        };
        var sut = new ResilientRedisDatabase(db, options);

        var act = () => sut.ExecuteAsync<string>(async _ =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return "never";
        });

        await act.Should().ThrowAsync<TimeoutRejectedException>();
    }

    // ── StringGet / StringSet (verify delegation) ──────────────────────────

    [Fact]
    public async Task StringGetAsync_DelegatesToDatabase()
    {
        var db = Substitute.For<IDatabase>();
        db.StringGetAsync("mykey", CommandFlags.None).Returns(Task.FromResult<RedisValue>("hello"));
        var sut = TestFactory.Create(db);

        var result = await sut.StringGetAsync("mykey");

        result.Should().Be("hello");
        await db.Received(1).StringGetAsync("mykey", CommandFlags.None);
    }

    [Fact]
    public async Task StringSetAsync_DelegatesToDatabase()
    {
        var db = Substitute.For<IDatabase>();
        db.StringSetAsync("k", "v", Expiration.Default, flags: CommandFlags.None).Returns(Task.FromResult(true));
        var sut = TestFactory.Create(db);

        var result = await sut.StringSetAsync("k", "v");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task StringIncrementAsync_DelegatesToDatabase()
    {
        var db = Substitute.For<IDatabase>();
        db.StringIncrementAsync("counter", 1L, CommandFlags.None).Returns(Task.FromResult(5L));
        var sut = TestFactory.Create(db);

        var result = await sut.StringIncrementAsync("counter");

        result.Should().Be(5L);
    }

    // ── Key operations ─────────────────────────────────────────────────────

    [Fact]
    public async Task KeyExistsAsync_DelegatesToDatabase()
    {
        var db = Substitute.For<IDatabase>();
        db.KeyExistsAsync("k", CommandFlags.None).Returns(Task.FromResult(true));
        var sut = TestFactory.Create(db);

        var result = await sut.KeyExistsAsync("k");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task KeyDeleteAsync_DelegatesToDatabase()
    {
        var db = Substitute.For<IDatabase>();
        db.KeyDeleteAsync("k", CommandFlags.None).Returns(Task.FromResult(true));
        var sut = TestFactory.Create(db);

        var result = await sut.KeyDeleteAsync("k");

        result.Should().BeTrue();
    }

    // ── Hash operations ────────────────────────────────────────────────────

    [Fact]
    public async Task HashGetAsync_DelegatesToDatabase()
    {
        var db = Substitute.For<IDatabase>();
        db.HashGetAsync("hk", "field", CommandFlags.None).Returns(Task.FromResult<RedisValue>("val"));
        var sut = TestFactory.Create(db);

        var result = await sut.HashGetAsync("hk", "field");

        result.Should().Be("val");
    }

    [Fact]
    public async Task HashGetAllAsync_DelegatesToDatabase()
    {
        var db = Substitute.For<IDatabase>();
        var entries = new[] { new HashEntry("f", "v") };
        db.HashGetAllAsync("hk", CommandFlags.None).Returns(Task.FromResult(entries));
        var sut = TestFactory.Create(db);

        var result = await sut.HashGetAllAsync("hk");

        result.Should().BeEquivalentTo(entries);
    }

    // ── Null guard ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullDatabase_Throws()
    {
        Action act = () => new ResilientRedisDatabase(null!, new PollyRedisOptions());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var db = Substitute.For<IDatabase>();
        Action act = () => new ResilientRedisDatabase(db, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullOperation_Throws()
    {
        var db = Substitute.For<IDatabase>();
        var sut = TestFactory.Create(db);
        var act = () => sut.ExecuteAsync<string>(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── MaxRetries = 0 skips retry ────────────────────────────────────────

    [Fact]
    public async Task MaxRetries_Zero_NoRetry()
    {
        int calls = 0;
        var db = Substitute.For<IDatabase>();
        var sut = TestFactory.Create(db, o =>
        {
            o.MaxRetries = 0;
            o.CircuitBreakerMinimumThroughput = 100;
        });

        var act = () => sut.ExecuteAsync<string>(_ =>
        {
            calls++;
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test");
        });

        await act.Should().ThrowAsync<RedisConnectionException>();
        calls.Should().Be(1);
    }
}

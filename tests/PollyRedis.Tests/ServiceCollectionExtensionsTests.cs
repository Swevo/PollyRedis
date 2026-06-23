namespace PollyRedis.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPollyRedis_WithMultiplexer_RegistersIResilientRedisDatabase()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase(-1).Returns(Substitute.For<IDatabase>());

        var services = new ServiceCollection();
        services.AddPollyRedis(multiplexer);

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<IResilientRedisDatabase>();

        db.Should().NotBeNull();
        db.Should().BeOfType<ResilientRedisDatabase>();
    }

    [Fact]
    public void AddPollyRedis_WithMultiplexerAndOptions_AppliesOptions()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase(-1).Returns(Substitute.For<IDatabase>());

        var services = new ServiceCollection();
        int capturedRetries = 0;
        services.AddPollyRedis(multiplexer, o =>
        {
            o.MaxRetries = 7;
            capturedRetries = o.MaxRetries;
        });

        services.BuildServiceProvider().GetRequiredService<IResilientRedisDatabase>()
            .Should().NotBeNull();

        capturedRetries.Should().Be(7);
    }

    [Fact]
    public void AddPollyRedis_ResolvesFromRegisteredMultiplexer()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase(-1).Returns(Substitute.For<IDatabase>());

        var services = new ServiceCollection();
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddPollyRedis();

        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<IResilientRedisDatabase>();

        db.Should().NotBeNull();
    }

    [Fact]
    public void AddPollyRedis_NullServices_Throws()
    {
        IServiceCollection services = null!;
        Action act = () => services.AddPollyRedis();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPollyRedis_NullMultiplexer_Throws()
    {
        var services = new ServiceCollection();
        Action act = () => services.AddPollyRedis((IConnectionMultiplexer)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetResilientDatabase_NullMultiplexer_Throws()
    {
        IConnectionMultiplexer multiplexer = null!;
        Action act = () => multiplexer.GetResilientDatabase();
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetResilientDatabase_ReturnsResilientRedisDatabase()
    {
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.GetDatabase(-1).Returns(Substitute.For<IDatabase>());

        var db = multiplexer.GetResilientDatabase();

        db.Should().NotBeNull().And.BeOfType<ResilientRedisDatabase>();
    }
}

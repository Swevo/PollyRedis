# PollyRedis

[![NuGet](https://img.shields.io/nuget/v/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis)
[![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis)
[![CI](https://github.com/Swevo/PollyRedis/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyRedis/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Polly v8 resilience for StackExchange.Redis.**  
Automatic retry with exponential back-off on `RedisConnectionException` and `RedisTimeoutException`, circuit breaker to stop hammering a degraded cluster, and per-command timeout — wired up in one line via `IServiceCollection` or `IConnectionMultiplexer`.

---

## Why PollyRedis?

Redis is often the first thing that fails under load, and StackExchange.Redis's built-in retry is limited. Production Redis clusters experience:

| Scenario | Without PollyRedis | With PollyRedis |
|---|---|---|
| **Redis node restart** | Requests fail immediately | Retries with exponential back-off |
| **Transient network blip** | Command throws `RedisConnectionException` | Retried transparently |
| **Redis timeout** | `RedisTimeoutException` propagates | Retried up to `MaxRetries` times |
| **Sustained outage** | Every command waits for timeout | Circuit opens; fails fast immediately |
| **Slow Redis** | Commands block indefinitely | Per-command timeout enforced |

---

## Installation

```bash
dotnet add package PollyRedis
```

---

## Quick Start

### With dependency injection

```csharp
// Program.cs
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));

builder.Services.AddPollyRedis();

// Inject IResilientRedisDatabase wherever you need Redis
public class CacheService(IResilientRedisDatabase db)
{
    public Task<RedisValue> GetAsync(string key) => db.StringGetAsync(key);
    public Task SetAsync(string key, string value, TimeSpan ttl)
        => db.StringSetAsync(key, value, ttl);
}
```

### Direct extension on IConnectionMultiplexer

```csharp
var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
IResilientRedisDatabase db = multiplexer.GetResilientDatabase(options =>
{
    options.MaxRetries = 4;
    options.CommandTimeout = TimeSpan.FromSeconds(3);
});

var value = await db.StringGetAsync("my-key");
```

---

## Configuration

```csharp
builder.Services.AddPollyRedis(options =>
{
    // Retry
    options.MaxRetries = 3;                                  // default: 3
    options.BaseDelay = TimeSpan.FromMilliseconds(200);      // default: 200 ms
    options.MaxDelay = TimeSpan.FromSeconds(10);             // default: 10 s

    // Circuit breaker
    options.CircuitBreakerMinimumThroughput = 5;             // default: 5
    options.CircuitBreakerFailureRatio = 0.5;                // default: 0.5 (50%)
    options.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30);

    // Timeout (per command)
    options.CommandTimeout = TimeSpan.FromSeconds(5);        // default: 5 s

    // Add extra exception types to treat as transient
    options.AdditionalTransientExceptions.Add(typeof(SocketException));
});
```

---

## Supported Operations

`IResilientRedisDatabase` covers the most common Redis commands:

| Category | Methods |
|---|---|
| **String** | `StringGetAsync`, `StringSetAsync`, `StringIncrementAsync`, `StringDecrementAsync` |
| **Key** | `KeyExistsAsync`, `KeyDeleteAsync`, `KeyExpireAsync` |
| **Hash** | `HashGetAsync`, `HashSetAsync`, `HashGetAllAsync`, `HashDeleteAsync` |
| **List** | `ListLeftPushAsync`, `ListRightPopAsync`, `ListLengthAsync` |
| **Set** | `SetAddAsync`, `SetRemoveAsync`, `SetContainsAsync` |
| **Sorted Set** | `SortedSetAddAsync`, `SortedSetScoreAsync` |
| **Escape hatch** | `ExecuteAsync<T>(Func<IDatabase, Task<T>>)` for any command |

### Custom command (escape hatch)

```csharp
// Any IDatabase operation wrapped in the Polly pipeline
var result = await db.ExecuteAsync(d => d.StringGetRangeAsync("key", 0, -1));
```

---

## Pipeline order

```
Command
  └─► Retry (exponential back-off on connection/timeout errors)
        └─► Circuit Breaker (opens on sustained failures)
              └─► Timeout (per-command deadline)
                    └─► IDatabase → Redis
```

---

## Comparison

| | Raw SE.Redis | SE.Redis built-in retry | **PollyRedis** |
|---|:---:|:---:|:---:|
| Retry on `RedisConnectionException` | ❌ | ⚠️ limited | ✅ |
| Retry on `RedisTimeoutException` | ❌ | ⚠️ limited | ✅ |
| Exponential back-off with jitter | ❌ | ❌ | ✅ |
| Circuit breaker | ❌ | ❌ | ✅ |
| Per-command timeout | ❌ | ❌ | ✅ |
| DI integration | ❌ | ❌ | ✅ |
| Custom transient exceptions | ❌ | ❌ | ✅ |
| Polly v8 pipeline | ❌ | ❌ | ✅ |

---

## Related Packages

| Package | Downloads | Description |
|---|---|---|
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | [![Downloads](https://img.shields.io/nuget/dt/PollyChaos.svg)](https://www.nuget.org/packages/PollyChaos) | Chaos engineering / fault injection for Polly v8 |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 pipelines for MediatR request handlers |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience for EF Core queries and SaveChanges |
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI API calls |
| [PollySignalR](https://www.nuget.org/packages/PollySignalR) | [![Downloads](https://img.shields.io/nuget/dt/PollySignalR.svg)](https://www.nuget.org/packages/PollySignalR) | Polly v8 exponential back-off reconnect policy for SignalR HubConnection |
| [PollyGrpc](https://www.nuget.org/packages/PollyGrpc) | Polly v8 resilience (retry, CB, timeout) for gRPC .NET clients via Interceptor |
| [PollyKafka](https://www.nuget.org/packages/PollyKafka) | Polly v8 resilience (retry, CB, timeout) for Confluent.Kafka producers and consumers |
| [PollyAzureEventHub](https://github.com/Swevo/PollyAzureEventHub) | Polly v8 for Azure Event Hubs |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience (retry, CB, timeout) for Azure Service Bus senders and receivers |
| [PollyElasticsearch](https://github.com/Swevo/PollyElasticsearch) | Polly v8 for Elastic.Clients.Elasticsearch |
| [PollyAzureKeyVault](https://github.com/Swevo/PollyAzureKeyVault) | Polly v8 for Azure Key Vault |
| [PollySendGrid](https://github.com/Swevo/PollySendGrid) | Polly v8 for SendGrid |
| [PollyMassTransit](https://github.com/Swevo/PollyMassTransit) | Polly v8 for MassTransit |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Custom back-off strategies for Polly v8 |

---

| [PollyRabbitMQ](https://www.nuget.org/packages/PollyRabbitMQ) | Polly v8 resilience for RabbitMQ.Client channels |

## License

MIT

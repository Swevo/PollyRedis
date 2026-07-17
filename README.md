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
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers — expose circuit-breaker state (Closed, HalfOpen, Open, Isolated) as /health endpoint responses |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Backoff delay strategies for Polly v8 resilience pipelines |
| [PollyGrpc](https://www.nuget.org/packages/PollyGrpc) | [![Downloads](https://img.shields.io/nuget/dt/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc) | Polly v8 resilience interceptor for gRPC |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience pipelines for Entity Framework Core — wrap every EF Core query and SaveChanges with retry, timeout and circuit-breaker via a single AddPollyResilience() call |
| [PollyRabbitMQ](https://www.nuget.org/packages/PollyRabbitMQ) | [![Downloads](https://img.shields.io/nuget/dt/PollyRabbitMQ.svg)](https://www.nuget.org/packages/PollyRabbitMQ) | Polly v8 resilience for RabbitMQ.Client v7+ — retry, circuit-breaker, and timeout for IChannel operations, with built-in RabbitMqTransientErrors predicate covering AlreadyClosedException, BrokerUnreachableException, OperationInterruptedException, and ConnectFailureException |
| [PollyMailKit](https://www.nuget.org/packages/PollyMailKit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMailKit.svg)](https://www.nuget.org/packages/PollyMailKit) | Polly v8 resilience pipelines for MailKit — retry, timeout, and circuit-breaker for SmtpClient.SendAsync and any MailKit SMTP operation |
| [PollyMassTransit](https://www.nuget.org/packages/PollyMassTransit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMassTransit.svg)](https://www.nuget.org/packages/PollyMassTransit) | Polly v8 resilience pipelines for MassTransit — retry, timeout, and circuit-breaker for IBus.Publish and ISendEndpointProvider.Send |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI API calls |
| [PollyAzureEventHub](https://www.nuget.org/packages/PollyAzureEventHub) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureEventHub.svg)](https://www.nuget.org/packages/PollyAzureEventHub) | Polly v8 resilience pipelines for Azure Event Hubs — retry, timeout, and circuit-breaker for EventHubProducerClient and EventHubConsumerClient |
| [PollySignalR](https://www.nuget.org/packages/PollySignalR) | [![Downloads](https://img.shields.io/nuget/dt/PollySignalR.svg)](https://www.nuget.org/packages/PollySignalR) | Polly v8 reconnect policy for SignalR |
| [PollyElasticsearch](https://www.nuget.org/packages/PollyElasticsearch) | [![Downloads](https://img.shields.io/nuget/dt/PollyElasticsearch.svg)](https://www.nuget.org/packages/PollyElasticsearch) | Polly v8 resilience pipelines for Elastic.Clients.Elasticsearch 8+ — retry, timeout, and circuit-breaker for any Elasticsearch operation, plus a built-in ElasticTransientErrors predicate covering rate limiting (429), service unavailability (503), gateway timeouts (504), and connection failures |
| [PollyHangfire](https://www.nuget.org/packages/PollyHangfire) | [![Downloads](https://img.shields.io/nuget/dt/PollyHangfire.svg)](https://www.nuget.org/packages/PollyHangfire) | Polly v8 resilience pipelines for Hangfire — retry, timeout, and circuit-breaker for IBackgroundJobClient.Enqueue and Schedule |
| [PollySendGrid](https://www.nuget.org/packages/PollySendGrid) | [![Downloads](https://img.shields.io/nuget/dt/PollySendGrid.svg)](https://www.nuget.org/packages/PollySendGrid) | Polly v8 resilience pipelines for SendGrid — retry, timeout, and circuit-breaker for ISendGridClient.SendEmailAsync |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 resilience pipelines for MediatR — add retry, timeout, circuit-breaker, rate-limiting, hedging, and chaos engineering to any MediatR request handler with a single line of DI registration |
| [PollyAzureKeyVault](https://www.nuget.org/packages/PollyAzureKeyVault) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureKeyVault.svg)](https://www.nuget.org/packages/PollyAzureKeyVault) | Polly v8 resilience pipelines for Azure Key Vault — retry, timeout, and circuit-breaker for SecretClient, KeyClient, and CertificateClient |
| [PollyAzureQueueStorage](https://www.nuget.org/packages/PollyAzureQueueStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureQueueStorage.svg)](https://www.nuget.org/packages/PollyAzureQueueStorage) | Polly v8 resilience pipelines for Azure Queue Storage — retry, timeout, and circuit-breaker for Azure.Storage.Queues QueueClient |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience for Azure Service Bus — retry, circuit breaker, and timeout for sending and receiving messages |
| [PollyKafka](https://www.nuget.org/packages/PollyKafka) | [![Downloads](https://img.shields.io/nuget/dt/PollyKafka.svg)](https://www.nuget.org/packages/PollyKafka) | Polly v8 resilience for Confluent.Kafka — retry, circuit breaker, and timeout for producers and consumers |
| [PollyAzureTableStorage](https://www.nuget.org/packages/PollyAzureTableStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureTableStorage.svg)](https://www.nuget.org/packages/PollyAzureTableStorage) | Polly v8 resilience pipelines for Azure Table Storage — retry, timeout, and circuit-breaker for Azure.Data.Tables TableClient |
| [PollyChaos](https://www.nuget.org/packages/PollyChaos) | [![Downloads](https://img.shields.io/nuget/dt/PollyChaos.svg)](https://www.nuget.org/packages/PollyChaos) | Chaos engineering and fault-injection resilience strategies for Polly v8 pipelines |

## 💼 Need .NET consulting?

The author of this package is available for consulting on **Polly v8 resilience**, **Azure cloud architecture**, and **clean .NET design**.

**[→ solidqualitysolutions.com](https://www.solidqualitysolutions.com/)** · **[LinkedIn](https://www.linkedin.com/in/justbannister/)**
## License

MIT

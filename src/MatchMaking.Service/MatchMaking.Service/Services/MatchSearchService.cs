using Confluent.Kafka;
using StackExchange.Redis;
using System.Text.Json;

namespace MatchMaking.Service.Services;

public sealed record MatchRequestMessage(string UserId);

public class MatchSearchService(
    IConnectionMultiplexer redis,
    ILogger<MatchSearchService> logger,
    IConfiguration config)
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly ProducerConfig _kafkaConfig = new() { BootstrapServers = config["Kafka:BootstrapServers"] };

    public async Task EnqueueMatchRequestAsync(string userId)
    {
        var message = new MatchRequestMessage(userId);
        var json = JsonSerializer.Serialize(message);

        using var producer = new ProducerBuilder<Null, string>(_kafkaConfig).Build();
        await producer.ProduceAsync("matchmaking.request", new Message<Null, string> { Value = json });

        logger.LogInformation("User {UserId} queued for matchmaking", userId);
    }
}
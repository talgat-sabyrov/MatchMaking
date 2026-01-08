using Confluent.Kafka;
using StackExchange.Redis;
using System.Text.Json;

namespace MatchMaking.Service.Services;

public sealed record MatchCompleteMessage(string MatchId, List<string> UserIds);

public class MatchCompleteConsumer(
    IConnectionMultiplexer redis,
    IConfiguration config,
    ILogger<MatchCompleteConsumer> logger) : BackgroundService
{
    private readonly ConsumerConfig _consumerConfig = new()
    {
        BootstrapServers = config["Kafka:BootstrapServers"],
        GroupId = "matchmaking.service.complete",
        AutoOffsetReset = AutoOffsetReset.Earliest
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(_consumerConfig).Build();
        consumer.Subscribe("matchmaking.complete");

        logger.LogInformation("MatchCompleteConsumer started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var cr = consumer.Consume(stoppingToken);
                var message = JsonSerializer.Deserialize<MatchCompleteMessage>(cr.Message.Value)!;

                var db = redis.GetDatabase();
                foreach (var userId in message.UserIds)
                {
                    await db.StringSetAsync($"match:{userId}", JsonSerializer.Serialize(new
                    {
                        matchId = message.MatchId,
                        userIds = message.UserIds
                    }));
                    await db.KeyExpireAsync($"match:{userId}", TimeSpan.FromHours(24));
                }

                logger.LogInformation("Match {MatchId} completed with {Count} players", message.MatchId, message.UserIds.Count);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MatchCompleteConsumer");
        }
    }
}
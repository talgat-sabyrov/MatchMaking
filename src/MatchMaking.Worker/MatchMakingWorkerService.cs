using Confluent.Kafka;
using StackExchange.Redis;
using System.Text.Json;

namespace MatchMaking.Worker;

public sealed record MatchRequest(string UserId);

public class MatchMakingWorkerService(
    IConnectionMultiplexer redis,
    IConfiguration config,
    ILogger<MatchMakingWorkerService> logger) : BackgroundService
{
    private readonly int _playersPerMatch = config.GetValue("MatchMaking:PlayersPerMatch", 3);
    private readonly ConsumerConfig _consumerConfig = new()
    {
        BootstrapServers = config["Kafka:BootstrapServers"],
        GroupId = "matchmaking.worker.group",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false
    };

    private readonly ProducerConfig _producerConfig = new()
    {
        BootstrapServers = config["Kafka:BootstrapServers"]
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<Ignore, string>(_consumerConfig).Build();
        consumer.Subscribe("matchmaking.request");

        var pendingKey = "matchmaking:pending";
        var db = redis.GetDatabase();

        logger.LogInformation("Worker started. Waiting for players... (need {_playersPerMatch})", _playersPerMatch);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var cr = consumer.Consume(stoppingToken);
                var request = JsonSerializer.Deserialize<MatchRequest>(cr.Message.Value)!;

                var added = await db.SetAddAsync(pendingKey, request.UserId);
                if (added)
                    logger.LogInformation("Player {UserId} added to queue", request.UserId);

                consumer.Commit(cr);

                var count = await db.SetLengthAsync(pendingKey);
                if (count >= _playersPerMatch)
                {
                    var userIds = await db.SetPopAsync(pendingKey, _playersPerMatch);
                    var matchId = Guid.NewGuid().ToString();

                    var completeMessage = new { matchId, userIds = userIds.Select(x => x.ToString()).ToList() };
                    var json = JsonSerializer.Serialize(completeMessage);

                    using var producer = new ProducerBuilder<Null, string>(_producerConfig).Build();
                    await producer.ProduceAsync("matchmaking.complete", new Message<Null, string> { Value = json });

                    logger.LogWarning("MATCH CREATED! MatchId={MatchId} Players={Players}", matchId, string.Join(", ", completeMessage.userIds));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Worker error");
        }
    }
}
using StackExchange.Redis;
using System.Text.Json;

namespace MatchMaking.Service.Services;

public record MatchInfo(string MatchId, List<string> UserIds);
public record MatchResponse(string matchId, List<string> userIds);

public class MatchInfoService(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<MatchInfo?> GetLatestMatchAsync(string userId)
    {
        var redisValue = await _db.StringGetAsync($"match:{userId}");

        if (redisValue.IsNullOrEmpty)
            return null;

        string jsonString = redisValue;

        var response = JsonSerializer.Deserialize<MatchResponse>(jsonString);

        return response is null
            ? null
            : new MatchInfo(response.matchId, response.userIds);
    }
}
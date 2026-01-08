using MatchMaking.Service;
using MatchMaking.Service.Services;
using StackExchange.Redis;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(
    builder.Configuration.GetValue<string>("Redis:ConnectionString")!));

builder.Services.AddHostedService<MatchCompleteConsumer>();

// Rate limiting: 1 request per 100ms per userId
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = 429;

    rateLimiterOptions.AddPolicy("MatchSearchPerUser", httpContext =>
    {
        var userId = httpContext.Request.Query["userId"].ToString();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return RateLimitPartition.GetNoLimiter("no-userid");
        }

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: userId,
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 1,     
                TokensPerPeriod = 1,
                ReplenishmentPeriod = TimeSpan.FromMilliseconds(100), 
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseRateLimiter();

var api = app.MapGroup("/api/matchmaking").RequireRateLimiting("MatchSearch");

api.MapPost("/search", async (string userId, MatchSearchService service) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest();

    await service.EnqueueMatchRequestAsync(userId);
    return Results.NoContent();
})
.WithName("SearchMatch")
.WithOpenApi();

// GET /api/matchmaking/match?userId=abc
api.MapGet("/match", async (string userId, MatchInfoService infoService) =>
{
    if (string.IsNullOrWhiteSpace(userId))
        return Results.BadRequest();

    var match = await infoService.GetLatestMatchAsync(userId);
    return match is not null ? Results.Ok(match) : Results.NotFound();
})
.WithName("GetMatch")
.WithOpenApi();

app.Run();
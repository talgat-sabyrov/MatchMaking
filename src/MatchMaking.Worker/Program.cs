using MatchMaking.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<MatchMakingWorkerService>();

var host = builder.Build();

await host.RunAsync();
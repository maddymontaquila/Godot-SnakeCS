#:sdk Aspire.AppHost.Sdk@13.4.6
#:package Aspire.Hosting.Go@13.4.6-preview.1.26319.6
#:package Aspire.Hosting.JavaScript@13.4.6

using Aspire.Hosting.Go;

var builder = DistributedApplication.CreateBuilder(args);

var scoreApi = builder.AddExecutable("score-api", "dotnet", "Services/ScoreService", "run", "--no-launch-profile")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

var matchmaking = builder.AddGoApp("matchmaking-api", "Services/MatchmakingService")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

var leaderboard = builder.AddNodeApp("leaderboard-api", "Services/LeaderboardService", "src/server.ts")
    .WithNpm()
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("SCORE_API_URL", scoreApi.GetEndpoint("http"))
    .WaitFor(scoreApi)
    .WithExternalHttpEndpoints();

builder.AddExecutable("godot-snake-client", "dotnet", ".", "build", "Snakes.csproj")
    .WithEnvironment("SCORE_API_URL", scoreApi.GetEndpoint("http"))
    .WithEnvironment("MATCHMAKING_API_URL", matchmaking.GetEndpoint("http"))
    .WithEnvironment("LEADERBOARD_API_URL", leaderboard.GetEndpoint("http"))
    .WaitFor(scoreApi)
    .WaitFor(matchmaking)
    .WaitFor(leaderboard);

builder.Build().Run();

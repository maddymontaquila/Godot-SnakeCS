using Microsoft.VisualBasic;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var leaderboard = builder.AddProject<LeaderboardAPI>("leaderboard");

builder.AddGodot("../Snakes/Snakes.csproj", "snakes")
    .WithReference(leaderboard);



builder.Build().Run();

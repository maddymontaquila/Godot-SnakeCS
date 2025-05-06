using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddGodot("../Snakes/Snakes.csproj", "snakes");

builder.AddProject<LeaderboardAPI>("leaderboard");

builder.Build().Run();

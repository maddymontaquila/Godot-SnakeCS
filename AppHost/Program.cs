var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("pg")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin();
var leaderboardDB = db.AddDatabase("leaderboard-db");

var leaderboard = builder.AddProject<Projects.LeaderboardAPI>("leaderboard")
    .WithReference(leaderboardDB)
    .WaitFor(leaderboardDB);

builder.AddGodot("../Snakes/Snakes.csproj", "snakes")
    .WithReference(leaderboard);

builder.Build().Run();

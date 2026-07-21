using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://localhost:{port}");
}

var app = builder.Build();

var scores = new ConcurrentQueue<ScoreEntry>();
scores.Enqueue(new ScoreEntry("Maddy", 42, "classic", DateTimeOffset.UtcNow.AddMinutes(-12)));
scores.Enqueue(new ScoreEntry("Clint", 37, "xbox-context", DateTimeOffset.UtcNow.AddMinutes(-7)));
scores.Enqueue(new ScoreEntry("Nick", 35, "godot-demo", DateTimeOffset.UtcNow.AddMinutes(-3)));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "score-api" }));

app.MapGet("/scores", () => scores
    .OrderByDescending(score => score.Points)
    .ThenByDescending(score => score.PlayedAt)
    .Take(10));

app.MapPost("/scores", (NewScore score) =>
{
    if (string.IsNullOrWhiteSpace(score.Player))
    {
        return Results.BadRequest(new { error = "Player is required." });
    }

    var entry = new ScoreEntry(
        score.Player.Trim(),
        Math.Max(0, score.Points),
        string.IsNullOrWhiteSpace(score.Mode) ? "classic" : score.Mode.Trim(),
        DateTimeOffset.UtcNow);

    scores.Enqueue(entry);
    return Results.Created($"/scores/{Uri.EscapeDataString(entry.Player)}", entry);
});

app.MapGet("/leaderboard", () => new
{
    generatedAt = DateTimeOffset.UtcNow,
    leaders = scores.OrderByDescending(score => score.Points).Take(3)
});

app.Run();

record NewScore(string Player, int Points, string? Mode);

record ScoreEntry(string Player, int Points, string Mode, DateTimeOffset PlayedAt);

using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("Npgsql"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddOtlpExporter());
    builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddOtlpExporter());
}

if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
{
    builder.WebHost.UseUrls($"http://localhost:{port}");
}

var app = builder.Build();

var connectionString = builder.Configuration.GetConnectionString("scores")
    ?? throw new InvalidOperationException("Connection string 'scores' is required.");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.ConfigureTracing(_ => { });
var dataSource = dataSourceBuilder.Build();
var legacyScoreDataPath = Path.Combine(app.Environment.ContentRootPath, "data", "scores.json");
await ScoreStore.InitializeAsync(dataSource, legacyScoreDataPath);
app.Lifetime.ApplicationStopping.Register(dataSource.Dispose);
var scores = new ScoreStore(dataSource);

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "score-api" }));

app.MapGet("/scores", async () => await scores.GetTopScoresAsync(10));

app.MapPost("/scores", async (NewScore score) =>
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

    await scores.AddAsync(entry);
    return Results.Created($"/scores/{Uri.EscapeDataString(entry.Player)}", entry);
});

app.MapPost("/demo-scores", async () =>
{
    var seededCount = await scores.SeedDemoScoresAsync();
    return Results.Ok(new { seededCount });
});

app.MapPost("/scores/clear", async () =>
{
    var clearedCount = await scores.ClearAsync();
    return Results.Ok(new { clearedCount });
});

app.MapGet("/leaderboard", async () => new
{
    generatedAt = DateTimeOffset.UtcNow,
    leaders = await scores.GetTopScoresAsync(3)
});

app.Run();

record NewScore(string Player, int Points, string? Mode);

record ScoreEntry(string Player, int Points, string Mode, DateTimeOffset PlayedAt);

sealed class ScoreStore(NpgsqlDataSource dataSource)
{
	private readonly NpgsqlDataSource _dataSource = dataSource;

	public static async Task InitializeAsync(NpgsqlDataSource dataSource, string legacyScoreDataPath)
	{
		await using var command = dataSource.CreateCommand(
			"""
			CREATE TABLE IF NOT EXISTS scores (
				id BIGSERIAL PRIMARY KEY,
				player TEXT NOT NULL,
				points INTEGER NOT NULL CHECK (points >= 0),
				mode TEXT NOT NULL,
				played_at TIMESTAMPTZ NOT NULL,
				is_demo BOOLEAN NOT NULL DEFAULT FALSE
			);

			ALTER TABLE scores ADD COLUMN IF NOT EXISTS is_demo BOOLEAN NOT NULL DEFAULT FALSE;

			CREATE INDEX IF NOT EXISTS scores_leaderboard_idx
				ON scores (points DESC, played_at DESC);

			CREATE UNIQUE INDEX IF NOT EXISTS scores_demo_player_idx
				ON scores (player) WHERE is_demo;
			""");
		await command.ExecuteNonQueryAsync();

		if (!File.Exists(legacyScoreDataPath))
		{
			return;
		}

		await using var legacyScoreStream = File.OpenRead(legacyScoreDataPath);
		var legacyScores = await JsonSerializer.DeserializeAsync<List<ScoreEntry>>(
			legacyScoreStream,
			new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
		if (legacyScores.Count == 0)
		{
			return;
		}

		await using var connection = await dataSource.OpenConnectionAsync();
		await using var transaction = await connection.BeginTransactionAsync();
		await using var countCommand = new NpgsqlCommand("SELECT COUNT(*) FROM scores;", connection, transaction);
		if (Convert.ToInt64(await countCommand.ExecuteScalarAsync()) != 0)
		{
			return;
		}

		foreach (var legacyScore in legacyScores)
		{
			await using var insertCommand = new NpgsqlCommand(
				"""
				INSERT INTO scores (player, points, mode, played_at)
				VALUES ($1, $2, $3, $4);
				""",
				connection,
				transaction);
			insertCommand.Parameters.AddWithValue(legacyScore.Player);
			insertCommand.Parameters.AddWithValue(legacyScore.Points);
			insertCommand.Parameters.AddWithValue(legacyScore.Mode);
			insertCommand.Parameters.AddWithValue(legacyScore.PlayedAt);
			await insertCommand.ExecuteNonQueryAsync();
		}

		await transaction.CommitAsync();
	}

	public async Task<IReadOnlyList<ScoreEntry>> GetTopScoresAsync(int count)
	{
		await using var connection = await _dataSource.OpenConnectionAsync();
		await using var command = new NpgsqlCommand(
			"""
			SELECT player, points, mode, played_at
			FROM scores
			ORDER BY points DESC, played_at DESC
			LIMIT $1;
			""",
			connection);
		command.Parameters.AddWithValue(count);

		var scores = new List<ScoreEntry>();
		await using var reader = await command.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			scores.Add(new ScoreEntry(
				reader.GetString(0),
				reader.GetInt32(1),
				reader.GetString(2),
				new DateTimeOffset(reader.GetDateTime(3).ToUniversalTime())));
		}

		return scores;
	}

	public async Task AddAsync(ScoreEntry entry)
	{
		await using var connection = await _dataSource.OpenConnectionAsync();
		await using var command = new NpgsqlCommand(
			"""
			INSERT INTO scores (player, points, mode, played_at)
			VALUES ($1, $2, $3, $4);
			""",
			connection);
		command.Parameters.AddWithValue(entry.Player);
		command.Parameters.AddWithValue(entry.Points);
		command.Parameters.AddWithValue(entry.Mode);
		command.Parameters.AddWithValue(entry.PlayedAt);
		await command.ExecuteNonQueryAsync();
	}

	public async Task<int> ClearAsync()
	{
		await using var connection = await _dataSource.OpenConnectionAsync();
		await using var command = new NpgsqlCommand("DELETE FROM scores;", connection);
		return await command.ExecuteNonQueryAsync();
	}

	public async Task<int> SeedDemoScoresAsync()
	{
		var demoScores = new[]
		{
			new ScoreEntry("Aspire Demo Bot", 120, "demo", DateTimeOffset.UtcNow),
			new ScoreEntry("Demo Gem Glider", 90, "demo", DateTimeOffset.UtcNow),
			new ScoreEntry("Demo Pixel Python", 70, "demo", DateTimeOffset.UtcNow)
		};

		await using var connection = await _dataSource.OpenConnectionAsync();
		await using var transaction = await connection.BeginTransactionAsync();
		foreach (var demoScore in demoScores)
		{
			await using var command = new NpgsqlCommand(
				"""
				INSERT INTO scores (player, points, mode, played_at, is_demo)
				VALUES ($1, $2, $3, $4, TRUE)
				ON CONFLICT (player) WHERE is_demo
				DO UPDATE SET
					points = EXCLUDED.points,
					mode = EXCLUDED.mode,
					played_at = EXCLUDED.played_at;
				""",
				connection,
				transaction);
			command.Parameters.AddWithValue(demoScore.Player);
			command.Parameters.AddWithValue(demoScore.Points);
			command.Parameters.AddWithValue(demoScore.Mode);
			command.Parameters.AddWithValue(demoScore.PlayedAt);
			await command.ExecuteNonQueryAsync();
		}

		await transaction.CommitAsync();
		return demoScores.Length;
	}
}

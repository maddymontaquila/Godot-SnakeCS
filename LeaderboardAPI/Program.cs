using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddServiceDefaults();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", 
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

builder.AddNpgsqlDataSource("leaderboard-db");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Initialize the database
using (var connection = app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnection())
{
    using var command = connection.CreateCommand();
    command.CommandText = @"
        CREATE TABLE IF NOT EXISTS leaderboard (
            id SERIAL PRIMARY KEY,
            player_name TEXT NOT NULL,
            score INT NOT NULL,
            date TIMESTAMP NOT NULL
        );
    ";
    command.ExecuteNonQuery();
}

// Endpoint to add a new score
app.MapPost("/leaderboard", async (LeaderboardEntry entry, NpgsqlDataSource dataSource) =>
{
    entry.Date = DateTime.Now;
    
    using var connection = await dataSource.OpenConnectionAsync();
    using var command = connection.CreateCommand();
    command.CommandText = "INSERT INTO leaderboard (player_name, score, date) VALUES (@PlayerName, @Score, @Date) RETURNING id";
    command.Parameters.AddWithValue("@PlayerName", entry.PlayerName);
    command.Parameters.AddWithValue("@Score", entry.Score);
    command.Parameters.AddWithValue("@Date", entry.Date);
    
    var id = await command.ExecuteScalarAsync();
    
    return Results.Created($"/leaderboard/{entry.PlayerName}", entry);
})
.WithName("AddScore");

// Endpoint to get all scores
app.MapGet("/leaderboard", async (NpgsqlDataSource dataSource) =>
{
    var leaderboardEntries = new List<LeaderboardEntry>();
    
    using var connection = await dataSource.OpenConnectionAsync();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT player_name, score, date FROM leaderboard ORDER BY score DESC";
    
    using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        leaderboardEntries.Add(new LeaderboardEntry
        {
            PlayerName = reader.GetString(0),
            Score = reader.GetInt32(1),
            Date = reader.GetDateTime(2)
        });
    }
    
    return leaderboardEntries;
})
.WithName("GetLeaderboard");

// Original weather forecast endpoint (kept for reference)
var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

// Model for leaderboard entries
record LeaderboardEntry
{
    public string PlayerName { get; set; } = "Anonymous";
    public int Score { get; set; }
    public DateTime Date { get; set; }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

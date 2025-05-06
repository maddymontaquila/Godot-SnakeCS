var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", 
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// In-memory leaderboard storage
var leaderboard = new List<LeaderboardEntry>();

// Endpoint to add a new score
app.MapPost("/leaderboard", (LeaderboardEntry entry) =>
{
    entry.Date = DateTime.Now;
    leaderboard.Add(entry);
    return Results.Created($"/leaderboard/{entry.PlayerName}", entry);
})
.WithName("AddScore");

// Endpoint to get all scores
app.MapGet("/leaderboard", () =>
{
    return leaderboard.OrderByDescending(e => e.Score).ToList();
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

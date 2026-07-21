using Godot;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Snake;
public partial class Snake : Node2D
{
	private static readonly ActivitySource ActivitySource = new("GodotSnake.Client");
	private static readonly Random rnd = new();
	private static readonly System.Net.Http.HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
	private int _snakeBodySize;
	private Vector2I _gameSize;
	
	// Scenes
	private Apple _apple;
	private SnakeBody _snakeBody;
	private Label _demoStatus;
	private Label _scoreLabel;
	private Control _gameOverPopup;
	private Label _gameOverScore;
	private LineEdit _playerName;
	private TracerProvider _tracerProvider;
	public override void _Ready()
	{
		_tracerProvider = CreateTracerProvider();
		using var activity = ActivitySource.StartActivity("game.start");
		_snakeBodySize = 40;
		_gameSize = new (15, 8);

		_snakeBody = GetNode<SnakeBody>("SnakeBody");
		_snakeBody.Position = new Vector2(0,0);

		_apple = GetNode("Apple") as Apple;
		SpawnApple();

		_snakeBody.GameOver += OnGameOver;
		_snakeBody.AppleEaten += SpawnApple;
		_snakeBody.AppleEaten += UpdateScore;
		_snakeBody.AppleEaten += RecordAppleEaten;

		_demoStatus = GetNode<Label>("DemoUi/Panel/DemoStatus");
		_scoreLabel = GetNode<Label>("DemoUi/ScoreLabel");
		_gameOverPopup = GetNode<Control>("DemoUi/GameOverPopup");
		_gameOverScore = GetNode<Label>("DemoUi/GameOverPopup/FinalScore");
		_playerName = GetNode<LineEdit>("DemoUi/Panel/PlayerName");
		_playerName.Text = System.Environment.UserName;
		UpdateScore();
		GetNode<Button>("DemoUi/Panel/MatchmakingButton").Pressed += DemoMatchmaking;
		GetNode<Button>("DemoUi/Panel/DemoOpponentButton").Pressed += DemoOpponent;
		GetNode<Button>("DemoUi/Panel/LeaderboardButton").Pressed += DemoLeaderboard;
		GetNode<Button>("DemoUi/Panel/ScoreButton").Pressed += DemoScore;
		GetNode<Button>("DemoUi/GameOverPopup/NewGameButton").Pressed += StartNewGame;
	}

	public override void _Process(double delta)
	{
		_snakeBody.ApplePosition = _apple?.Position;
	}

	public override void _ExitTree()
	{
		_tracerProvider?.Dispose();
	}

	public void OnGameOver() {
		using var activity = ActivitySource.StartActivity("game.over");
		activity?.SetTag("game.score", CurrentScore);
		if (_apple is not null){
			_apple.QueueFree();
			_apple = null;
		}

		_gameOverScore.Text = $"FINAL SCORE: {CurrentScore}";
		_gameOverPopup.Visible = true;
	}

	private void StartNewGame()
	{
		GetTree().ReloadCurrentScene();
	}

	private void SpawnApple()
	{
		var position = GetAvailableApplePosition();
		if (_apple is null)
		{
			_apple = new Apple();
			AddChild(_apple);
		}

		_apple.Position = position;
		_apple.QueueRedraw();
	}

	private Vector2 GetAvailableApplePosition()
	{
		for (var attempt = 0; attempt < _gameSize.X * _gameSize.Y; attempt++)
		{
			var position = new Vector2(
				rnd.Next(_gameSize.X) * _snakeBodySize,
				rnd.Next(_gameSize.Y) * _snakeBodySize);

			if (!_snakeBody.IsOccupied(position))
			{
				return position;
			}
		}

		return Vector2.Zero;
	}

	private async void DemoMatchmaking()
	{
		using var activity = ActivitySource.StartActivity("demo.matchmaking.join");
		var serviceUrl = GetServiceUrl("MATCHMAKING_API_URL");
		var player = GetPlayerName();
		if (serviceUrl is null || player is null)
		{
			return;
		}

		try
		{
			SetDemoStatus("Joining match queue...");
			var request = new { player, mode = "classic" };
			using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
			using var response = await httpClient.PostAsync($"{serviceUrl}/match", content);
			response.EnsureSuccessStatusCode();
			using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			ShowMatchStatus(document.RootElement);
		}
		catch (HttpRequestException)
		{
			SetDemoStatus("MATCHMAKING OFFLINE\nCheck the Aspire stack.");
		}
		catch (TaskCanceledException)
		{
			SetDemoStatus("MATCHMAKING TIMED OUT");
		}
		catch (JsonException)
		{
			SetDemoStatus("MATCHMAKING RESPONSE INVALID");
		}
	}

	private async void DemoOpponent()
	{
		using var activity = ActivitySource.StartActivity("demo.matchmaking.demo-opponent");
		var serviceUrl = GetServiceUrl("MATCHMAKING_API_URL");
		var player = GetPlayerName();
		if (serviceUrl is null || player is null)
		{
			return;
		}

		try
		{
			SetDemoStatus("Adding demo opponent...");
			var request = new { player, mode = "classic" };
			using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
			using var response = await httpClient.PostAsync($"{serviceUrl}/demo-opponent", content);
			response.EnsureSuccessStatusCode();
			using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
			ShowMatchStatus(document.RootElement);
		}
		catch (HttpRequestException)
		{
			SetDemoStatus("JOIN A MATCH FIRST\nthen add the demo opponent.");
		}
		catch (TaskCanceledException)
		{
			SetDemoStatus("DEMO OPPONENT TIMED OUT");
		}
		catch (JsonException)
		{
			SetDemoStatus("DEMO OPPONENT RESPONSE INVALID");
		}
	}

	private async void DemoLeaderboard()
	{
		using var activity = ActivitySource.StartActivity("demo.leaderboard.load");
		var serviceUrl = GetServiceUrl("LEADERBOARD_API_URL");
		if (serviceUrl is null)
		{
			return;
		}

		try
		{
			SetDemoStatus("Loading leaderboard...");
			using var document = JsonDocument.Parse(await httpClient.GetStringAsync($"{serviceUrl}/leaderboard"));
			var leaders = document.RootElement.GetProperty("leaders");
			var lines = new List<string> { "TOP SNAKES" };

			foreach (var leader in leaders.EnumerateArray().Take(3))
			{
				lines.Add($"{leader.GetProperty("player").GetString()}: {leader.GetProperty("points").GetInt32()}");
			}

			SetDemoStatus(string.Join("\n", lines));
		}
		catch (HttpRequestException)
		{
			SetDemoStatus("LEADERBOARD OFFLINE\nCheck the Aspire stack.");
		}
		catch (TaskCanceledException)
		{
			SetDemoStatus("LEADERBOARD TIMED OUT");
		}
		catch (JsonException)
		{
			SetDemoStatus("LEADERBOARD RESPONSE INVALID");
		}
	}

	private async void DemoScore()
	{
		using var activity = ActivitySource.StartActivity("demo.score.submit");
		var serviceUrl = GetServiceUrl("SCORE_API_URL");
		var player = GetPlayerName();
		if (serviceUrl is null || player is null)
		{
			return;
		}

		try
		{
			SetDemoStatus("Posting pixel score...");
			var score = new { player, points = CurrentScore, mode = "classic" };
			using var content = new StringContent(JsonSerializer.Serialize(score), Encoding.UTF8, "application/json");
			using var response = await httpClient.PostAsync($"{serviceUrl}/scores", content);
			response.EnsureSuccessStatusCode();
			SetDemoStatus($"SCORE POSTED\n{player}: {score.points}");
		}
		catch (HttpRequestException)
		{
			SetDemoStatus("SCORE API OFFLINE\nCheck the Aspire stack.");
		}
		catch (TaskCanceledException)
		{
			SetDemoStatus("SCORE API TIMED OUT");
		}
	}

	private string GetServiceUrl(string environmentVariable)
	{
		var serviceUrl = System.Environment.GetEnvironmentVariable(environmentVariable)?.TrimEnd('/');
		if (!string.IsNullOrWhiteSpace(serviceUrl))
		{
			return serviceUrl;
		}

		SetDemoStatus("RUN THROUGH ASPIRE\nfor live service demos.");
		return null;
	}

	private string GetPlayerName()
	{
		var player = _playerName.Text.Trim();
		if (!string.IsNullOrWhiteSpace(player))
		{
			return player;
		}

		SetDemoStatus("ENTER A PLAYER NAME");
		return null;
	}

	private int CurrentScore => Math.Max(0, _snakeBody.Length - 2) * 10;

	private void UpdateScore()
	{
		_scoreLabel.Text = $"SCORE: {CurrentScore}";
	}

	private void RecordAppleEaten()
	{
		using var activity = ActivitySource.StartActivity("game.apple.eaten");
		activity?.SetTag("game.score", CurrentScore);
	}

	private static TracerProvider CreateTracerProvider()
	{
		var tracerBuilder = Sdk.CreateTracerProviderBuilder()
			.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("godot-snake-client"))
			.AddSource(ActivitySource.Name)
			.AddHttpClientInstrumentation();

		if (!string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
		{
			tracerBuilder.AddOtlpExporter();
		}

		return tracerBuilder.Build();
	}

	private void ShowMatchStatus(JsonElement match)
	{
		var ticket = match.GetProperty("ticketId").GetString();
		var status = match.GetProperty("status").GetString();
		if (status == "matched")
		{
			var opponent = match.GetProperty("opponent").GetString();
			if (opponent == "Aspire Demo Bot")
			{
				SetDemoStatus("DEMO MATCH FOUND\nvs Aspire Demo Bot\nLeaderboard seeded");
				return;
			}

			SetDemoStatus($"MATCH FOUND\nvs {opponent}\nTicket {ticket}");
			return;
		}

		SetDemoStatus($"QUEUED FOR MATCH\nWaiting for player 2\nTicket {ticket}");
	}

	private void SetDemoStatus(string text)
	{
		_demoStatus.Text = text;
	}

}

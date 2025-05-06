using Godot;
using System;
using System.Timers;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;

namespace Snake;
public partial class Snake : Node2D
{
	// We could use a Godot Timer too.
	private System.Timers.Timer timer;
	// To generate random numbers.
	private static readonly Random rnd = new();
	private int _snakeBodySize;
	private Vector2I _gameSize;
	private int _score = 0;
	private static readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();
	
	// Scenes
	private Apple _apple;
	private SnakeBody _snakeBody;
	public override void _Ready()
	{
		_snakeBodySize = 40;
		_gameSize = new (15, 8);

		_snakeBody = GetNode<SnakeBody>("SnakeBody");
		_snakeBody.Position = new Vector2(0,0);

		_apple = GetNode("Apple") as Apple;
		_apple.Position = new Vector2(
			rnd.Next(_gameSize.X) * _snakeBodySize, 
			rnd.Next(_gameSize.Y) * _snakeBodySize);

		timer = new System.Timers.Timer(10000);
		timer.Elapsed += NewApple;
		timer.AutoReset = true;
		timer.Start();

		 // Set up API client base address
		_httpClient.BaseAddress = new Uri(System.Environment.GetEnvironmentVariable("services__leaderboard__https__0"));

		// We connect to the SnakeBody's GameOver Signal using C# 
		// Lambda expression works too.
		_snakeBody.GameOver += OnGameOver;
		_snakeBody.AppleEaten += OnAppleEaten;
		
	}

	public override void _Process(double delta)
	{
		if(_apple is not null){
			if(_snakeBody.TryEat(_apple)){
				RemoveChild(_apple);
				_apple = null;
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
    	{
        	if (keyEvent.Keycode == Key.L)
			SubmitScoreToLeaderboard();	
		}
	}

	private async Task SubmitScoreToLeaderboard()
	{
		try
		{
			var leaderboardEntry = new LeaderboardEntry
			{
				PlayerName = "Player", // Could be improved with a name input dialog
				Score = _score
			};

			var response = await _httpClient.PostAsJsonAsync("/leaderboard", leaderboardEntry);
			
			if (response.IsSuccessStatusCode)
			{
				GD.Print("Score submitted to leaderboard successfully!");
			}
			else
			{
				GD.Print($"Failed to submit score: {response.StatusCode}");
			}
		}
		catch (Exception ex)
		{
			GD.Print($"Error submitting score: {ex.Message}");
		}
	}

	public void OnAppleEaten()
	{
		_score += 10;
		GD.Print($"Score: {_score}");
	}

	public void OnGameOver() {
			timer.Stop();
			if (_apple is not null){
				RemoveChild(_apple);
			}
			GD.Print($"Game Over! Final Score: {_score}");
	}

	public void NewApple(object src , ElapsedEventArgs e)
	{
		if(_apple is not null){
			// Use CallDeferred to remove child from main thread instead of direct removal
			CallDeferred("remove_child", _apple);
		}
		_apple = new Apple
		{
			Position = new Vector2(rnd.Next(0, 15) * 40, rnd.Next(0, 8) * 40)
		};
		
		// Using Call Deferred to align to main thread,
		// please read function documentation
		CallDeferred("add_child", _apple);
	}
}

// Define the model for the leaderboard entry
public class LeaderboardEntry
{
	public string PlayerName { get; set; } = "Anonymous";
	public int Score { get; set; }
	public DateTime Date { get; set; }
}

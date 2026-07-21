using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Snake;
public partial class SnakeBody : Sprite2D
{
	[Signal]
	public delegate void GameOverEventHandler();

	[Signal]
	public delegate void AppleEatenEventHandler();

	private double _time = 0;
	private enum Direction
	{
		LEFT,
		RIGHT,
		UP,
		DOWN
	};

	private Direction _direction;
	private List<Rect2> _body;
	private bool _crash = false;
	private bool _gameOverEmitted = false;
	private double _deathElapsed = 0;
	private Texture2D _bodyTexture;
	private Texture2D _headTexture;
	public Vector2? ApplePosition { get; set; }
	public int Length => _body.Count;
	public override void _Ready()
	{
		_direction = Direction.RIGHT;
		_body = new List<Rect2>
		{
			new Rect2(0, 0, 40, 40),
			new Rect2(40, 0, 40, 40)
		};
		_bodyTexture = GD.Load<Texture2D>("res://Assets/snake-body.svg");
		_headTexture = GD.Load<Texture2D>("res://Assets/snake-head.svg");
		ZIndex = 1;
	}

	public override void _Draw()
	{
		for (var index = _body.Count - 1; index >= 0; index--)
		{
			var rect = _body[index];
			if (index == 0)
			{
				DrawSetTransform(rect.GetCenter(), GetHeadRotation(), Vector2.One);
				DrawTextureRect(_headTexture, new Rect2(-20, -20, 40, 40), false);
				DrawSetTransform(Vector2.Zero, 0, Vector2.One);
			}
			else
			{
				DrawTextureRect(_bodyTexture, rect, false);
			}
		}

		if (_crash)
		{
			var flash = Mathf.PingPong((float)_deathElapsed * 12, 1);
			var deathColor = new Color(1, 0.2f, 0.72f, 0.45f + flash * 0.5f);
			foreach (var rect in _body)
			{
				DrawRect(new Rect2(rect.Position + new Vector2(4, 4), rect.Size - new Vector2(8, 8)), deathColor);
			}
		}
	}

	private float GetHeadRotation()
	{
		return _direction switch
		{
			Direction.RIGHT => 0,
			Direction.DOWN => Mathf.Pi / 2,
			Direction.LEFT => Mathf.Pi,
			_ => -Mathf.Pi / 2
		};
	}

	public bool IsOccupied(Vector2 position)
	{
		return _body.Any(rect => rect.Position == position);
	}

	public bool Crash()
	{
		return _body.Skip(1).Any( t => {
			return t.Position.X == _body[0].Position.X && t.Position.Y == _body[0].Position.Y;
		});
	}

	public override void _Process(double delta)
	{
		if (_crash)
		{
			_deathElapsed += delta;
			QueueRedraw();
			if (_deathElapsed >= 0.75 && !_gameOverEmitted)
			{
				_gameOverEmitted = true;
				EmitSignal(SignalName.GameOver);
			}

			return;
		}

		_time += delta;
		if(_time > 0.5){
			var translation = _direction switch
			{
				Direction.RIGHT => new Vector2(40, 0),
				Direction.LEFT => new Vector2(-40, 0),
				Direction.UP => new Vector2(0, -40),
				_ => new Vector2(0, 40),
			};
			if (_body.Count > 0){
				var newRect = new Rect2(_body[0].Position, _body[0].Size);
				newRect.Position += translation;
				if(newRect.Position.X < 0){
					newRect.Position = new Vector2(600, newRect.Position.Y);
				}
				if(newRect.Position.X > 600){
					newRect.Position = new Vector2(0, newRect.Position.Y);
				}
				if(newRect.Position.Y < 0){
					newRect.Position = new Vector2(newRect.Position.X, 320);
				}    
				if(newRect.Position.Y > 320){
					newRect.Position = new Vector2(newRect.Position.X, 0);
				}

				var ateApple = ApplePosition is Vector2 applePosition && newRect.Position == applePosition;
				_body.Insert(0, newRect);
				if(!ateApple){
					_body.RemoveAt(_body.Count-1);
				}
				else{
					GD.Print("Eat Apple!");
					EmitSignal(SignalName.AppleEaten);
				}
				if(Crash()){
					GD.Print("CRASH! Game Over");
					_crash = true;
					_deathElapsed = 0;
					QueueRedraw();
				}
			}
			if (!_crash){
				QueueRedraw();
			}
			_time = 0;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (_crash || GetViewport().GuiGetFocusOwner() is LineEdit)
		{
			return;
		}

		if(@event.IsAction("ui_left") && _direction != Direction.RIGHT)
		{
			_direction = Direction.LEFT;
			return;
		}
		if(@event.IsAction("ui_right") && _direction != Direction.LEFT)
		{
			_direction = Direction.RIGHT;
			return;
		}
		if(@event.IsAction("ui_up") && _direction != Direction.DOWN)
		{
			_direction = Direction.UP;
			return;
		}
		if(@event.IsAction("ui_down") && _direction != Direction.UP)
		{
			_direction = Direction.DOWN;
			return;
		}
	}
}

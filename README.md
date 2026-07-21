# Snake C# + Aspire

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE)

A pixel-art Godot Snake game backed by an Aspire-orchestrated score API, PostgreSQL leaderboard, matchmaking service, Reveal.js demo deck, and end-to-end OpenTelemetry.

## Requirements

- [.NET 10 SDK](https://get.dot.net)
- [Godot Engine .NET 4.7.1](https://godotengine.org/download/windows)
- [Aspire CLI](https://aspire.dev)
- [Go](https://go.dev) and [Node.js](https://nodejs.org)

## Run

Everything starts through Aspire:

```powershell
$env:GODOT_EXECUTABLE = "C:\path\to\Godot_v4.7.1-stable_mono_win64_console.exe"
$env:GODOT_HEADLESS = "false" # Launch the playable Godot window.
aspire run
```

`GODOT_EXECUTABLE` is optional: without it, the Godot resource builds `Snakes.csproj` instead. Aspire discovers the file-based AppHost from `aspire.config.json`; the dashboard prints its URL when the stack starts.

| Resource | Purpose |
| --- | --- |
| `postgres` / `scores` | Persistent PostgreSQL score store |
| `score-api` | C# score and leaderboard API |
| `matchmaking-api` | Go queue and demo-opponent matcher |
| `leaderboard-api` | Node.js leaderboard proxy |
| `slides` | Reveal.js presentation, linked as **slides** in the dashboard |
| `godot-snake-client` | Playable Godot client |

## Demo

Play to earn score, then use **Find Match** and **Demo Opponent** to pair with Aspire Demo Bot and seed labeled demo scores. Open **Leaderboard** to display persisted results. The dashboard’s **Clear leaderboard** action on `leaderboard-api` removes all scores after confirmation.

Godot, matchmaking, leaderboard, and Postgres-backed scoring export OpenTelemetry to the Aspire dashboard. For one connected trace, use **Find Match**, **Demo Opponent**, then **Leaderboard** and open **Traces**.

![Snake C# screenshot](screenshot-1.jpg?raw=true "Godot C# screenshot")

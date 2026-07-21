# Snake C# + Aspire

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE)
[![Godot v4.0](https://img.shields.io/badge/Godot-v4.0-blue.svg)](https://github.com/ramaureirac/godot-tactical-rpg/tree/release/godot-v4.0)

Basic snake game sample made with C# for Godot 4.0+, now paired with a modern Aspire AppHost that models the backend stack a game usually grows around: scores, leaderboards, matchmaking, and the local orchestration needed to debug them together.

It has a main scene with three nodes:

![Scenes](scenes.jpg)

Each one of the nodes has a script in C# to implement the game logic.

### Requirements

- [.NET 10 SDK](https://get.dot.net) for the Aspire AppHost and services
- [.NET 8+ SDK](https://get.dot.net) for the Godot C# project
- [Godot Engine .NET 4.7.1](https://godotengine.org/download/windows)
- [Aspire CLI](https://aspire.dev)
- [Go](https://go.dev)
- [Node.js](https://nodejs.org)
- [Visual Studio Code](https://code.visualstudio.com/)
  - [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

Remember to modify **[.vscode/launch.json](.vscode/launch.json)** and change **{path_to_godot}** to your installation path to enable debugging on Visual Studio Code.

### Aspire demo

The file-based **[AppHost](AppHost/apphost.cs)** models the local stack. It starts:

| Resource | Language | Purpose |
| --- | --- | --- |
| `postgres` / `scores` | PostgreSQL | Aspire-managed persistent database for submitted scores. |
| `score-api` | C# / ASP.NET Core | Persists submitted scores in PostgreSQL and exposes `/scores`, `/leaderboard`, and `/health`. |
| `matchmaking-api` | Go | Maintains a live queue, pairs distinct players through `/match`, and seeds labeled demo leaderboard scores when using the explicit demo opponent. |
| `leaderboard-api` | TypeScript / Node.js | Reads the persisted score API data and exposes `/leaderboard`. |
| `godot-snake-client` | C# / Godot | Runs the existing Godot game through the Godot .NET CLI, or builds the C# project when no CLI is configured. |

Run the full local stack:

```powershell
$env:GODOT_EXECUTABLE = "C:\path\to\Godot_v4.x-stable_mono_win64_console.exe"
dotnet run --file AppHost\apphost.cs
aspire ps --format Json
aspire describe --format Json
```

`GODOT_EXECUTABLE` is optional. When it points to the Godot .NET/Mono console executable, Aspire runs the Godot client through the Godot CLI; otherwise it falls back to building `Snakes.csproj`.
Submitted scores are stored in Aspire's persistent PostgreSQL volume.

To launch the playable Godot window with the leaderboard, matchmaking, and score-demo buttons connected to the Aspire services:

```powershell
$env:GODOT_HEADLESS = "false"
dotnet run --file AppHost\apphost.cs
```

For a deterministic presentation, click **Find Match**, then **Demo Opponent** to pair the queued player with the clearly labeled Aspire Demo Bot and seed three idempotent `Demo` leaderboard entries. A real second player can still join the same queue normally.

The custom Godot resource uses Fluent's **Games** icon in the dashboard. Use **Clear leaderboard** on `leaderboard-api` to remove all persisted player and demo scores; Aspire asks for confirmation and reports the number of removed rows.

### Traces

The Aspire dashboard receives OpenTelemetry traces from the Godot client, matchmaking API, score API/PostgreSQL, and leaderboard API. For a complete demo trace, click **Find Match**, **Demo Opponent**, then **Leaderboard**, and open **Traces** in the dashboard.

Useful endpoints once the dashboard starts:

```powershell
curl http://localhost:<score-api-port>/scores
curl -X POST http://localhost:<matchmaking-api-port>/match -H "Content-Type: application/json" -d '{\"player\":\"Player One\",\"mode\":\"classic\"}'
curl http://localhost:<leaderboard-api-port>/leaderboard
```

### Preview

![Snake C# screenshot](screenshot-1.jpg?raw=true "Godot C# screenshot")

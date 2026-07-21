# Snake C# + Aspire

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](./LICENSE)
[![Godot v4.0](https://img.shields.io/badge/Godot-v4.0-blue.svg)](https://github.com/ramaureirac/godot-tactical-rpg/tree/release/godot-v4.0)

Basic snake game sample made with C# for Godot 4.0+, now paired with a modern Aspire AppHost that models the backend stack a game usually grows around: scores, leaderboards, matchmaking, and the local orchestration needed to debug them together.

It has a main scene with three nodes:

![Scenes](scenes.jpg)

Each one of the nodes has a script in C# to implement the game logic.

### Requirements

- [.NET 10 SDK](https://get.dot.net) for the Aspire AppHost and services
- [.NET 6+ SDK](https://get.dot.net) for the existing Godot C# project
- [Godot Engine - .Net 4.0](https://godotengine.org)
- [Aspire CLI](https://aspire.dev)
- [Go](https://go.dev)
- [Node.js](https://nodejs.org)
- [Visual Studio Code](https://code.visualstudio.com/)
  - [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

Remember to modify **[.vscode/launch.json](.vscode/launch.json)** and change **{path_to_godot}** to your installation path to enable debugging on Visual Studio Code.

### Aspire demo

The root **[apphost.cs](apphost.cs)** file models the local stack. It starts:

| Resource | Language | Purpose |
| --- | --- | --- |
| `score-api` | C# / ASP.NET Core | Records scores and exposes `/scores`, `/leaderboard`, and `/health`. |
| `matchmaking-api` | Go | Returns a lightweight matchmaking ticket from `/match`. |
| `leaderboard-api` | TypeScript / Node.js | Reads score data and exposes a leaderboard-shaped API. |
| `godot-snake-client` | C# / Godot | Builds the existing Godot game project as the client resource. |

Run the full local stack:

```powershell
dotnet run --file apphost.cs
aspire ps --format Json
aspire describe --format Json
```

Useful endpoints once the dashboard starts:

```powershell
curl http://localhost:<score-api-port>/scores
curl http://localhost:<matchmaking-api-port>/match
curl http://localhost:<leaderboard-api-port>/leaderboard
```

### Preview

![Snake C# screenshot](screenshot-1.jpg?raw=true "Godot C# screenshot")

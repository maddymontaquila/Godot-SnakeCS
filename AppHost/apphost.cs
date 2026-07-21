#:sdk Aspire.AppHost.Sdk@13.5.0-preview.1.26371.3
#:package Aspire.Hosting.Go@13.5.0-preview.1.26371.3
#:package Aspire.Hosting.JavaScript@13.5.0-preview.1.26371.3
#:package Aspire.Hosting.PostgreSQL@13.5.0-preview.1.26371.4

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Go;

var builder = DistributedApplication.CreateBuilder(args);
var godotHeadless = !string.Equals(builder.Configuration["GODOT_HEADLESS"], "false", StringComparison.OrdinalIgnoreCase);
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var scoresDb = postgres.AddDatabase("scores");

#pragma warning disable ASPIRECSHARPAPPS001
var scoreApi = builder.AddCSharpApp("score-api", "../Services/ScoreService", options => options.ExcludeLaunchProfile = true)
    .WithHttpEndpoint(env: "PORT")
    .WithReference(scoresDb)
    .WaitFor(scoresDb)
    .WithExternalHttpEndpoints();
#pragma warning restore ASPIRECSHARPAPPS001

var matchmaking = builder.AddGoApp("matchmaking-api", "../Services/MatchmakingService")
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("SCORE_API_URL", scoreApi.GetEndpoint("http"))
    .WaitFor(scoreApi)
    .WithExternalHttpEndpoints();

var leaderboard = builder.AddNodeApp("leaderboard-api", "../Services/LeaderboardService", "src/server.ts")
    .WithNpm()
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("SCORE_API_URL", scoreApi.GetEndpoint("http"))
    .WaitFor(scoreApi)
    .WithHttpCommand(
        path: "/scores/clear",
        displayName: "Clear leaderboard",
        endpointSelector: () => scoreApi.GetEndpoint("http"),
        commandOptions: new HttpCommandOptions
        {
            Description = "Clears all player and demo scores from the Postgres leaderboard.",
            ConfirmationMessage = "Clear every persisted score from the leaderboard?",
            IconName = "Delete",
            Method = HttpMethod.Post,
            ResultMode = HttpCommandResultMode.Auto
        })
    .WithExternalHttpEndpoints();

builder.AddViteApp("slides", "../slides", "start")
    .WithUrlForEndpoint("http", url => url.DisplayText = "slides")
    .WithExternalHttpEndpoints();

builder.AddGodotApp(
        "godot-snake-client",
        "..",
        builder.Configuration["GODOT_EXECUTABLE"],
        headless: godotHeadless,
        quitAfterFrames: godotHeadless ? 120 : 0)
    .WithOtlpExporter()
    .WithEnvironment("SCORE_API_URL", scoreApi.GetEndpoint("http"))
    .WithEnvironment("MATCHMAKING_API_URL", matchmaking.GetEndpoint("http"))
    .WithEnvironment("LEADERBOARD_API_URL", leaderboard.GetEndpoint("http"))
    .WaitFor(scoreApi)
    .WaitFor(matchmaking)
    .WaitFor(leaderboard);

builder.Build().Run();

public sealed class GodotResource : ExecutableResource
{
    public GodotResource([ResourceName] string name, string command, string workingDirectory, bool usesGodotCli)
        : base(name, command, workingDirectory)
    {
        UsesGodotCli = usesGodotCli;
    }

    public bool UsesGodotCli { get; }
}

public static class GodotResourceBuilderExtensions
{
    public static IResourceBuilder<GodotResource> AddGodotApp(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string projectPath,
        string? godotExecutablePath = null,
        bool headless = true,
        int quitAfterFrames = 120,
        string fallbackProjectFile = "Snakes.csproj")
    {
        var godotExecutable = ResolveGodotExecutable(godotExecutablePath);
        var usesGodotCli = godotExecutable is not null;

        var resource = new GodotResource(
            name,
            usesGodotCli ? godotExecutable! : "dotnet",
            projectPath,
            usesGodotCli);

        var resourceBuilder = builder.AddResource(resource);
        resourceBuilder.WithIconName("Games");

        if (!usesGodotCli)
        {
            return resourceBuilder.WithArgs("build", fallbackProjectFile);
        }

        var args = new List<string>();
        if (headless)
        {
            args.Add("--headless");
        }

        args.AddRange(["--path", "."]);

        if (quitAfterFrames > 0)
        {
            args.AddRange(["--quit-after", quitAfterFrames.ToString()]);
        }

        return resourceBuilder.WithArgs(args.ToArray());
    }

    private static string? ResolveGodotExecutable(string? configuredPath)
    {
        if (IsUsableExecutable(configuredPath))
        {
            return configuredPath;
        }

        foreach (var command in new[] { "godot", "godot4", "godot_console", "godot.console" })
        {
            var resolved = ResolveCommandOnPath(command);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? ResolveCommandOnPath(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, command + extension);
                if (IsUsableExecutable(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool IsUsableExecutable(string? path)
        => !string.IsNullOrWhiteSpace(path) && File.Exists(path);
}

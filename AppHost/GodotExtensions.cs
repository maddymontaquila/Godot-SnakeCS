using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class GodotExtensions
{
    /// <summary>
    /// Adds a Godot project to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="projectPath">The path to the Godot project file (.csproj).</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="args">Optional arguments to pass to the Godot executable.</param>
    /// <returns>A reference to the Godot project resource.</returns>
    public static IResourceBuilder<GodotResource> AddGodot(
        this IDistributedApplicationBuilder builder, 
        string projectPath, 
        string? name = null,
        params string[] args)
    {
        name ??= Path.GetFileNameWithoutExtension(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? ".";
        var godotPath = GetGodotExecutablePath();
        
        // Create a plain resource for Godot with metadata
        var resource = new GodotResource(name);
        var project = builder.AddResource(resource)
            .WithAnnotation(new EnvironmentCallbackAnnotation(
                callback: envBuilder => 
                {
                    envBuilder["GODOT_PROJECT_PATH"] = projectPath;
                    envBuilder["GODOT_PROJECT_DIR"] = projectDirectory;
                }
            ));
        
        // Add a hook that runs before launch to build and run the project
        builder.Services.AddSingleton<IDistributedApplicationLifecycleHook>(sp => 
            new GodotBuildHook(name, projectPath, projectDirectory, args, 
                sp.GetService<ILogger<GodotBuildHook>>() ?? NullLogger<GodotBuildHook>.Instance));
        
        return project;
    }
    
    // Custom resource type to hold Godot-specific information
    public class GodotResource(string name) : Resource(name)
    {
        
    }
    
    /// <summary>
    /// Get the Godot executable path from the GODOT environment variable or fallback to default
    /// </summary>
    private static string GetGodotExecutablePath()
    {
        // First check for GODOT environment variable
        var godotPath = Environment.GetEnvironmentVariable("GODOT");
        if (!string.IsNullOrWhiteSpace(godotPath))
        {
            return godotPath;
        }
        
        // Fallback to platform-specific defaults
        if (OperatingSystem.IsMacOS())
        {
            return "godot";
        }
        else if (OperatingSystem.IsWindows())
        {
            return "godot.exe";
        }
        else if (OperatingSystem.IsLinux())
        {
            return "godot";
        }
        
        throw new PlatformNotSupportedException("Current platform is not supported for Godot execution.");
    }
    
    // Class to handle building and running Godot through lifecycle hooks
    private class GodotBuildHook : IDistributedApplicationLifecycleHook
    {
        private readonly string _name;
        private readonly string _projectPath;
        private readonly string _projectDirectory;
        private readonly string[] _args;
        private readonly ILogger<GodotBuildHook> _logger;
        
        public GodotBuildHook(string name, string projectPath, string projectDirectory, string[] args, ILogger<GodotBuildHook> logger)
        {
            _name = name;
            _projectPath = projectPath;
            _projectDirectory = projectDirectory;
            _args = args;
            _logger = logger;
        }
        
        public async Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Building Godot project: {ProjectPath}", _projectPath);
            
            // First, build the .NET project
            var buildProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build \"{_projectPath}\" --configuration Debug",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = _projectDirectory
                }
            };
            
            buildProcess.Start();
            await buildProcess.WaitForExitAsync(cancellationToken);
            
            if (buildProcess.ExitCode != 0)
            {
                _logger.LogError("Failed to build Godot project: {ProjectPath}", _projectPath);
                var error = await buildProcess.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogError("Build errors: {Error}", error);
                return;
            }
            
            _logger.LogInformation("Successfully built Godot project: {ProjectPath}", _projectPath);
            
            // Now run Godot using the path from GODOT env var or default
            var godotExecutable = GetGodotExecutablePath();
            
            // Prepare Godot arguments
            var godotArgs = new List<string>
            {
                "--path",
                _projectDirectory,
            };
            
            // Add any custom args
            godotArgs.AddRange(_args);
            
            _logger.LogInformation("Starting Godot project: {ProjectPath} in directory {Directory} using executable: {GodotPath}", 
                _projectPath, _projectDirectory, godotExecutable);
                
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = godotExecutable,
                    Arguments = String.Join(" ", godotArgs),
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = _projectDirectory,
                    
                    // Set environment variables
                    EnvironmentVariables = 
                    {
                        ["ASPNETCORE_ENVIRONMENT"] = "Development"
                    }
                }
            };
            
            process.Start();
            _logger.LogInformation("Godot project started: {ProjectPath}", _projectPath);
        }
        
        public Task AfterStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
    
    // Null logger implementation if logger service is not available
    private class NullLogger<T> : ILogger<T>
    {
        public static readonly ILogger<T> Instance = new NullLogger<T>();
        
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TddGuard.DotNet;

namespace TddGuard.DotNet.TestWrapper;

/// <summary>
/// Test wrapper that forwards arguments to dotnet test and handles compilation errors
/// </summary>
public class Program
{
    private static readonly ILogger<Program> _logger = LoggerFactory
        .Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
        .CreateLogger<Program>();

    private static string _storageDirectory = ".claude/tdd-guard/data";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            InitializeStorageDirectory();
            
            // Add the tdd-guard logger to the arguments if not already present
            var modifiedArgs = EnsureTddGuardLoggerPresent(args);
            
            // Run dotnet test with forwarded arguments
            var (exitCode, output, errorOutput) = await RunDotnetTest(modifiedArgs);
            
            // If compilation failed, log it as a test result
            if (exitCode != 0 && IsCompilationError(output + "\n" + errorOutput))
            {
                await LogCompilationError(output + "\n" + errorOutput);
            }
            
            return exitCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run dotnet test wrapper");
            return 1;
        }
    }

    private static void InitializeStorageDirectory()
    {
        var currentWorkDir = Directory.GetCurrentDirectory();
        _logger.LogInformation("Current work dir: {currentWorkDir}", currentWorkDir);
        
        // Check for TDD_GUARD_PROJECT_ROOT environment variable
        var projectRoot = Environment.GetEnvironmentVariable("TDD_GUARD_PROJECT_ROOT");
        _logger.LogInformation("projectRoot: {projectRoot}", projectRoot);
        
        if (!string.IsNullOrEmpty(projectRoot) && Path.IsPathRooted(projectRoot))
        {
            _storageDirectory = Path.Combine(projectRoot, ".claude", "tdd-guard", "data");
        }
        else if (!string.IsNullOrEmpty(currentWorkDir))
        {
            // Find project root by looking for .claude directory
            var foundProjectRoot = FindProjectRoot(currentWorkDir);
            _storageDirectory = Path.Combine(foundProjectRoot, ".claude", "tdd-guard", "data");
        }
        
        _logger.LogInformation("_storageDirectory: {_storageDirectory}", _storageDirectory);
        
        // Ensure storage directory exists
        Directory.CreateDirectory(_storageDirectory);
    }

    private static string FindProjectRoot(string startDirectory)
    {
        var currentDir = new DirectoryInfo(startDirectory);
        
        while (currentDir != null)
        {
            // Look for .claude directory
            if (Directory.Exists(Path.Combine(currentDir.FullName, ".claude")))
            {
                return currentDir.FullName;
            }
            
            currentDir = currentDir.Parent;
        }
        
        // If no .claude found, fall back to current directory
        return startDirectory;
    }

    private static string[] EnsureTddGuardLoggerPresent(string[] args)
    {
        // Check if --logger:tdd-guard is already present
        var hasLogger = args.Any(arg => arg.Contains("--logger") && arg.Contains("tdd-guard"));
        
        if (hasLogger)
        {
            return args;
        }
        
        // Add the logger
        var modifiedArgs = new List<string>(args);
        modifiedArgs.Add("--logger:tdd-guard");
        
        return modifiedArgs.ToArray();
    }

    private static async Task<(int exitCode, string output, string errorOutput)> RunDotnetTest(string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "test " + string.Join(" ", args.Select(EscapeArgument)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _logger.LogInformation("Running: dotnet {arguments}", startInfo.Arguments);

        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new StringBuilder();
        var errorOutputBuilder = new StringBuilder();
        
        // Capture output while also forwarding to console
        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null)
            {
                Console.WriteLine(e.Data);
                outputBuilder.AppendLine(e.Data);
            }
        };
        
        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null)
            {
                Console.Error.WriteLine(e.Data);
                errorOutputBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        await process.WaitForExitAsync();
        
        return (process.ExitCode, outputBuilder.ToString(), errorOutputBuilder.ToString());
    }

    private static string EscapeArgument(string arg)
    {
        // Escape arguments that contain spaces
        if (arg.Contains(' ') && !arg.StartsWith('"'))
        {
            return $"\"{arg}\"";
        }
        return arg;
    }

    private static bool IsCompilationError(string errorOutput)
    {
        if (string.IsNullOrEmpty(errorOutput))
            return false;

        // Look for compilation error indicators
        var compilationErrorPatterns = new[]
        {
            @"Build FAILED",
            @"error CS\d+:",
            @"error MSB\d+:",
            @"Compilation failed",
            @"\d+\s+Error\(s\)",
            @"could not execute because the specified command or file was not found"
        };

        return compilationErrorPatterns.Any(pattern => 
            Regex.IsMatch(errorOutput, pattern, RegexOptions.IgnoreCase));
    }

    private static async Task LogCompilationError(string errorOutput)
    {
        try
        {
            // Create a synthetic test result for the compilation error
            var compilationError = ExtractCompilationErrorMessage(errorOutput);
            
            var capturedTestRun = new CapturedTestRun
            {
                TestModules = new List<CapturedModule>
                {
                    new CapturedModule
                    {
                        ModuleId = "CompilationError",
                        Tests = new List<CapturedTest>
                        {
                            new CapturedTest
                            {
                                Name = "CompilationError",
                                FullName = "CompilationError.CompilationError",
                                State = "failed",
                                Errors = new List<CapturedError>
                                {
                                    new CapturedError
                                    {
                                        Message = compilationError,
                                    }
                                }
                            }
                        }
                    }
                },
                UnhandledErrors = new List<CapturedUnhandledError>(),
                Reason = "failed"
            };

            // Save as JSON (matching the test logger format)
            var outputPath = Path.Combine(_storageDirectory, "test.json");
            var json = JsonSerializer.Serialize(capturedTestRun, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await File.WriteAllTextAsync(outputPath, json);
            _logger.LogInformation("Compilation error logged to {outputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log compilation error");
        }
    }

    private static string ExtractCompilationErrorMessage(string errorOutput)
    {
        if (string.IsNullOrEmpty(errorOutput))
            return "Compilation failed";

        var lines = errorOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var relevantLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines and MSBuild header lines
            if (string.IsNullOrEmpty(trimmedLine) || 
                trimmedLine.StartsWith("Microsoft (R) Build Engine") ||
                trimmedLine.StartsWith("Copyright (C)") ||
                trimmedLine.StartsWith("Build started") ||
                trimmedLine.StartsWith("Project file(s)") ||
                trimmedLine.Contains("Time Elapsed"))
            {
                continue;
            }

            // Include error lines and build failure summaries
            if (trimmedLine.Contains("error CS") || 
                trimmedLine.Contains("error MSB") ||
                trimmedLine.Contains("Build FAILED") ||
                trimmedLine.Contains("Error(s)") ||
                trimmedLine.Contains("Warning(s)"))
            {
                relevantLines.Add(trimmedLine);
            }
        }

        if (relevantLines.Count > 0)
        {
            return string.Join("\n", relevantLines);
        }

        // Fallback: return first few non-empty lines
        var firstLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(5);
        return string.Join("\n", firstLines);
    }
}

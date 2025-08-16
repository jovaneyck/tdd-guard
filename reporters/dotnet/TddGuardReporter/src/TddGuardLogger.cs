using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TddGuard.DotNet;

/// <summary>
/// Test logger implementation for TDD Guard that captures Tests results
/// and saves them in the format expected by the TDD Guard validation system.
/// </summary>
[FriendlyName("tdd-guard")]
[ExtensionUri("logger://tdd-guard/dotnet-logger/v1")]
public class TddGuardLogger : ITestLogger
{
    private readonly List<CapturedTest> _testResults = new();
    private readonly List<CapturedUnhandledError> _unhandledErrors = new();
    private string _storageDirectory = ".claude/tdd-guard/data";

    private ILogger<TddGuardLogger> _logger = LoggerFactory
        .Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace))
        .CreateLogger<TddGuardLogger>();
    
    public void Initialize(TestLoggerEvents events, string testRunDirectory)
    {
        _logger.LogInformation("Initializing. {testRunDirectory}", testRunDirectory);
        // Initialize storage directory from environment or config
        InitializeStorageDirectory(testRunDirectory);
        
        // Register event handlers
        events.TestResult += OnTestResult;
        events.TestRunComplete += OnTestRunComplete;
        events.TestRunMessage += OnTestRunMessage;
    }

    private void InitializeStorageDirectory(string testRunDirectory)
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
            // Find project root by looking for .git directory or .sln files
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
        
        // If no .claude found, we're out of luck.
        throw new Exception("Could not find a .claude directory to log to.");
    }

    private void OnTestResult(object? sender, TestResultEventArgs e)
    {
        var testResult = e.Result;
        var capturedTest = new CapturedTest
        {
            Name = testResult.DisplayName ?? testResult.TestCase.DisplayName,
            FullName = testResult.TestCase.FullyQualifiedName,
            State = MapTestOutcome(testResult.Outcome)
        };

        // Capture error details if Tests failed
        if (testResult.Outcome == TestOutcome.Failed && testResult.ErrorMessage != null)
        {
            capturedTest.Errors =
            [
                new CapturedError
                {
                    Message = testResult.ErrorMessage,
                    Stack = testResult.ErrorStackTrace,
                    Name = "AssertionError"
                }
            ];
        }

        _testResults.Add(capturedTest);
    }

    private void OnTestRunMessage(object? sender, TestRunMessageEventArgs e)
    {
        // Capture unhandled errors during Tests run
        if (e.Level == TestMessageLevel.Error)
        {
            _unhandledErrors.Add(new CapturedUnhandledError
            {
                Message = e.Message,
                Name = "TestRunError"
            });
        }
    }

    private void OnTestRunComplete(object? sender, TestRunCompleteEventArgs e)
    {
        try
        {
            SaveTestResults(e);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the Tests run
            Console.WriteLine($"TDD Guard Logger: Failed to save Tests results - {ex.Message}");
        }
    }

    private void SaveTestResults(TestRunCompleteEventArgs e)
    {
        // Group tests by module/source file (similar to Jest/Vitest pattern)
        var moduleGroups = _testResults
            .GroupBy(test => GetModuleId(test.FullName))
            .Select(group => new CapturedModule
            {
                ModuleId = group.Key,
                Tests = group.ToList()
            })
            .ToList();

        var capturedTestRun = new CapturedTestRun
        {
            TestModules = moduleGroups,
            UnhandledErrors = _unhandledErrors.ToList(),
            Reason = DetermineTestRunReason(e)
        };

        // Ensure storage directory exists
        Directory.CreateDirectory(_storageDirectory);

        // Save as JSON (matching other reporters' format)
        var outputPath = Path.Combine(_storageDirectory, "test.json");
        var json = JsonSerializer.Serialize(capturedTestRun, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        File.WriteAllText(outputPath, json);
    }

    private static string GetModuleId(string fullyQualifiedName)
    {
        // Extract class/module name from fully qualified Tests name
        // Example: "MyProject.Tests.UserServiceTests.ShouldCreateUser" -> "MyProject.Tests.UserServiceTests"
        var parts = fullyQualifiedName.Split('.');
        if (parts.Length > 1)
        {
            return string.Join(".", parts.Take(parts.Length - 1));
        }
        return fullyQualifiedName;
    }

    private static string MapTestOutcome(TestOutcome outcome)
    {
        return outcome switch
        {
            TestOutcome.Passed => "passed",
            TestOutcome.Failed => "failed",
            TestOutcome.Skipped => "skipped",
            _ => "failed"
        };
    }

    private static string DetermineTestRunReason(TestRunCompleteEventArgs e)
    {
        if (e.IsCanceled || e.IsAborted)
        {
            return "interrupted";
        }

        if (e.TestRunStatistics != null && e.TestRunStatistics.Stats != null)
        {
            var failedCount = e.TestRunStatistics.Stats.TryGetValue(TestOutcome.Failed, out var failed) ? failed : 0;
            return failedCount == 0 ? "passed" : "failed";
        }

        return "failed";
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Text.Json;
using Xunit;

namespace TddGuard.DotNet.Tests;

public class TestableTestLoggerEvents : TestLoggerEvents
{
    public override event EventHandler<TestRunMessageEventArgs>? TestRunMessage;
    public override event EventHandler<TestRunStartEventArgs>? TestRunStart;
    public override event EventHandler<TestResultEventArgs>? TestResult;
    public override event EventHandler<TestRunCompleteEventArgs>? TestRunComplete;
    public override event EventHandler<DiscoveryStartEventArgs>? DiscoveryStart;
    public override event EventHandler<TestRunMessageEventArgs>? DiscoveryMessage;
    public override event EventHandler<DiscoveredTestsEventArgs>? DiscoveredTests;
    public override event EventHandler<DiscoveryCompleteEventArgs>? DiscoveryComplete;

    public void RaiseTestResult(TestResultEventArgs e)
    {
        this.TestResult?.Invoke(this,e);
    }

    public void RaiseTestRunComplete(TestRunCompleteEventArgs e)
    {
        this.TestRunComplete?.Invoke(this,e);
    }

    public void RaiseTestRunMessage(TestRunMessageEventArgs e)
    {
        this.TestRunMessage?.Invoke(this, e);
    }
}

public class TddGuardLoggerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly TddGuardLogger _logger;
    private readonly TestableTestLoggerEvents _events;

    public TddGuardLoggerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        
        _logger = new TddGuardLogger();
        _events = new TestableTestLoggerEvents();
        
        // Set project root to temp directory for testing
        Environment.SetEnvironmentVariable("TDD_GUARD_PROJECT_ROOT", _tempDirectory);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("TDD_GUARD_PROJECT_ROOT", null);
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Initialize_ShouldSetupEventHandlers()
    {
        // Act
        _logger.Initialize(_events, _tempDirectory);

        // Assert - No exception thrown indicates successful initialization
        Assert.True(true);
    }

    [Fact]
    public void OnTestResult_ShouldCapturePassedTest()
    {
        // Arrange
        _logger.Initialize(_events, _tempDirectory);
        var testCase = new TestCase("MyTest", new Uri("executor://xunit"), "TestAssembly.dll")
        {
            DisplayName = "Should Pass Test",
            FullyQualifiedName = "MyNamespace.MyClass.MyTest"
        };
        var testResult = new TestResult(testCase)
        {
            Outcome = TestOutcome.Passed,
            DisplayName = "Should Pass Test"
        };

        // Act
        _events.RaiseTestResult(new TestResultEventArgs(testResult));
        _events.RaiseTestRunComplete(new TestRunCompleteEventArgs(
            new TestRunStatistics(1, new Dictionary<TestOutcome, long> { { TestOutcome.Passed, 1 } }),
            false, false, null, null, null, TimeSpan.Zero));

        // Assert
        var outputPath = Path.Combine(_tempDirectory, ".claude", "tdd-guard", "data", "test.json");
        Assert.True(File.Exists(outputPath));

        var json = File.ReadAllText(outputPath);
        var result = JsonSerializer.Deserialize<CapturedTestRun>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(result);
        Assert.Single(result.TestModules);
        Assert.Single(result.TestModules[0].Tests);
        Assert.Equal("Should Pass Test", result.TestModules[0].Tests[0].Name);
        Assert.Equal("MyNamespace.MyClass.MyTest", result.TestModules[0].Tests[0].FullName);
        Assert.Equal("passed", result.TestModules[0].Tests[0].State);
        Assert.Equal("passed", result.Reason);
    }

    [Fact]
    public void OnTestResult_ShouldCaptureFailedTestWithError()
    {
        // Arrange
        _logger.Initialize(_events, _tempDirectory);
        var testCase = new TestCase("MyTest", new Uri("executor://xunit"), "TestAssembly.dll")
        {
            DisplayName = "Should Fail Test",
            FullyQualifiedName = "MyNamespace.MyClass.MyTest"
        };
        var testResult = new TestResult(testCase)
        {
            Outcome = TestOutcome.Failed,
            DisplayName = "Should Fail Test",
            ErrorMessage = "Expected: 5\nActual: 3",
            ErrorStackTrace = "at MyNamespace.MyClass.MyTest() in Tests.cs:line 10"
        };

        // Act
        _events.RaiseTestResult(new TestResultEventArgs(testResult));
        _events.RaiseTestRunComplete(new TestRunCompleteEventArgs(
            new TestRunStatistics(1, new Dictionary<TestOutcome, long> { { TestOutcome.Failed, 1 } }),
            false, false, null, null, null, TimeSpan.Zero));

        // Assert
        var outputPath = Path.Combine(_tempDirectory, ".claude", "tdd-guard", "data", "test.json");
        var json = File.ReadAllText(outputPath);
        var result = JsonSerializer.Deserialize<CapturedTestRun>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(result);
        var test = result.TestModules[0].Tests[0];
        Assert.Equal("failed", test.State);
        Assert.NotNull(test.Errors);
        Assert.Single(test.Errors);
        Assert.Equal("Expected: 5\nActual: 3", test.Errors[0].Message);
        Assert.Equal("at MyNamespace.MyClass.MyTest() in Tests.cs:line 10", test.Errors[0].Stack);
        Assert.Equal("failed", result.Reason);
    }

    [Fact]
    public void OnTestResult_ShouldGroupTestsByModule()
    {
        // Arrange
        _logger.Initialize(_events, _tempDirectory);
        
        var testCase1 = new TestCase("Test1", new Uri("executor://xunit"), "TestAssembly.dll")
        {
            FullyQualifiedName = "MyNamespace.ClassA.Test1"
        };
        var testCase2 = new TestCase("Test2", new Uri("executor://xunit"), "TestAssembly.dll")
        {
            FullyQualifiedName = "MyNamespace.ClassA.Test2"
        };
        var testCase3 = new TestCase("Test3", new Uri("executor://xunit"), "TestAssembly.dll")
        {
            FullyQualifiedName = "MyNamespace.ClassB.Test3"
        };

        // Act
        _events.RaiseTestResult(new TestResultEventArgs(new TestResult(testCase1) { Outcome = TestOutcome.Passed }));
        _events.RaiseTestResult(new TestResultEventArgs(new TestResult(testCase2) { Outcome = TestOutcome.Passed }));
        _events.RaiseTestResult(new TestResultEventArgs(new TestResult(testCase3) { Outcome = TestOutcome.Passed }));
        _events.RaiseTestRunComplete(new TestRunCompleteEventArgs(
            new TestRunStatistics(3, new Dictionary<TestOutcome, long> { { TestOutcome.Passed, 3 } }),
            false, false, null, null, null, TimeSpan.Zero));

        // Assert
        var outputPath = Path.Combine(_tempDirectory, ".claude", "tdd-guard", "data", "test.json");
        var json = File.ReadAllText(outputPath);
        var result = JsonSerializer.Deserialize<CapturedTestRun>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(result);
        Assert.Equal(2, result.TestModules.Count);
        
        var moduleA = result.TestModules.First(m => m.ModuleId == "MyNamespace.ClassA");
        var moduleB = result.TestModules.First(m => m.ModuleId == "MyNamespace.ClassB");
        
        Assert.Equal(2, moduleA.Tests.Count);
        Assert.Single((IEnumerable)moduleB.Tests);
    }

    [Fact]
    public void OnTestRunMessage_ShouldCaptureUnhandledErrors()
    {
        // Arrange
        _logger.Initialize(_events, _tempDirectory);

        // Act
        _events.RaiseTestRunMessage(new TestRunMessageEventArgs(TestMessageLevel.Error, "Unhandled exception occurred"));
        _events.RaiseTestRunComplete(new TestRunCompleteEventArgs(
            new TestRunStatistics(0, new Dictionary<TestOutcome, long>()),
            false, false, null, null, null, TimeSpan.Zero));

        // Assert
        var outputPath = Path.Combine(_tempDirectory, ".claude", "tdd-guard", "data", "test.json");
        var json = File.ReadAllText(outputPath);
        var result = JsonSerializer.Deserialize<CapturedTestRun>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(result);
        Assert.NotNull(result.UnhandledErrors);
        Assert.Single(result.UnhandledErrors);
        Assert.Equal("Unhandled exception occurred", result.UnhandledErrors[0].Message);
        Assert.Equal("TestRunError", result.UnhandledErrors[0].Name);
    }

    [Fact]
    public void OnTestRunComplete_ShouldHandleInterruptedRun()
    {
        // Arrange
        _logger.Initialize(_events, _tempDirectory);

        // Act
        _events.RaiseTestRunComplete(new TestRunCompleteEventArgs(
            new TestRunStatistics(0, new Dictionary<TestOutcome, long>()),
            true, // isCanceled
            false,
            null, null, null, TimeSpan.Zero));

        // Assert
        var outputPath = Path.Combine(_tempDirectory, ".claude", "tdd-guard", "data", "test.json");
        var json = File.ReadAllText(outputPath);
        var result = JsonSerializer.Deserialize<CapturedTestRun>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(result);
        Assert.Equal("interrupted", result.Reason);
    }

    [Theory]
    [InlineData(TestOutcome.Passed, "passed")]
    [InlineData(TestOutcome.Failed, "failed")]
    [InlineData(TestOutcome.Skipped, "skipped")]
    [InlineData(TestOutcome.NotFound, "failed")]
    public void OnTestResult_ShouldMapTestOutcomeCorrectly(TestOutcome outcome, string expectedState)
    {
        // Arrange
        _logger.Initialize(_events, _tempDirectory);
        var testCase = new TestCase("MyTest", new Uri("executor://xunit"), "TestAssembly.dll")
        {
            FullyQualifiedName = "MyNamespace.MyClass.MyTest"
        };
        var testResult = new TestResult(testCase)
        {
            Outcome = outcome
        };

        // Act
        _events.RaiseTestResult(new TestResultEventArgs(testResult));
        _events.RaiseTestRunComplete(new TestRunCompleteEventArgs(
            new TestRunStatistics(1, new Dictionary<TestOutcome, long> { { outcome, 1 } }),
            false, false, null, null, null, TimeSpan.Zero));

        // Assert
        var outputPath = Path.Combine(_tempDirectory, ".claude", "tdd-guard", "data", "test.json");
        var json = File.ReadAllText(outputPath);
        var result = JsonSerializer.Deserialize<CapturedTestRun>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.NotNull(result);
        Assert.Equal(expectedState, result.TestModules[0].Tests[0].State);
    }

    [Fact]
    public void FailingTest()
    {
        Assert.True(false);
    }
    
    [Fact(Skip = "skipped example")]
    public void SkippedTest()
    {
        Assert.True(false);
    }
}
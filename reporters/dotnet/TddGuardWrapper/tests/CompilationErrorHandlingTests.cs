using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using TddGuard.DotNet;
using Xunit;

namespace TddGuard.DotNet.TestWrapper.IntegrationTests;

public class CompilationErrorHandlingTests
{
    private readonly string _wrapperExecutablePath;
    private readonly string _brokenProjectPath;
    private readonly string _tempDataDirectory;

    public CompilationErrorHandlingTests()
    {
        // Get paths relative to test assembly location
        var testAssemblyPath = typeof(CompilationErrorHandlingTests).Assembly.Location;
        var testDirectory = Path.GetDirectoryName(testAssemblyPath)!;
        
        _wrapperExecutablePath = Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "..", "src", "bin", "Debug", "net9.0", "tdd-guard-dotnet-test.exe"));
        _brokenProjectPath = Path.GetFullPath(Path.Combine(testDirectory, "..", "..", "..", "fixtures", "BrokenProject"));
        
        // Create a unique temp directory for each test run
        _tempDataDirectory = Path.Combine(Path.GetTempPath(), "tdd-guard-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDataDirectory);
        Directory.CreateDirectory(Path.Combine(_tempDataDirectory, ".claude", "tdd-guard", "data"));
    }

    [Fact]
    public async Task WhenProjectHasCompilationErrors_ShouldCreateSyntheticTestResult()
    {
        // Arrange
        var claudeDataPath = Path.Combine(_tempDataDirectory, ".claude", "tdd-guard", "data", "test.json");
        
        // Act
        var (exitCode, output, errorOutput) = await RunWrapperAsync(_brokenProjectPath);
        
        // Assert
        exitCode.Should().NotBe(0, "compilation should fail");
        
        // Check that the test.json file was created
        File.Exists(claudeDataPath).Should().BeTrue("test.json should be created for compilation errors");
        
        // Read and parse the test results
        var jsonContent = await File.ReadAllTextAsync(claudeDataPath);
        var testResult = JsonSerializer.Deserialize<CapturedTestRun>(jsonContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        // Verify the structure matches expected format
        testResult.Should().NotBeNull();
        testResult!.TestModules.Should().HaveCount(1, "should have one module for compilation error");
        testResult.Reason.Should().Be("failed");
        
        var module = testResult.TestModules[0];
        module.ModuleId.Should().Be("CompilationError");
        module.Tests.Should().HaveCount(1, "should have one synthetic test for compilation error");
        
        var test = module.Tests[0];
        test.Name.Should().Be("CompilationError");
        test.FullName.Should().Be("CompilationError.CompilationError");
        test.State.Should().Be("failed");
        test.Errors.Should().NotBeNull().And.HaveCount(1);
        
        var error = test.Errors![0];
        error.Message.Should().NotBeNullOrEmpty("should contain compilation error details");
        
        // Verify error message contains expected compilation error patterns
        error.Message.Should().MatchRegex(@"(error CS\d+|Build FAILED|Error\(s\))", 
            "error message should contain compilation error indicators");
        
        jsonContent.Should().NotContain("stack", "tdd-guard does not like explicit nulls in JSON.");
    }
    
    [Fact]
    public async Task WhenProjectHasCompilationErrors_ShouldIncludeLoggerInArguments()
    {
        // Act
        var (exitCode, output, errorOutput) = await RunWrapperAsync(_brokenProjectPath);
        
        // Assert
        exitCode.Should().NotBe(0);
        
        // Check that the wrapper logged that it added the logger
        output.Should().Contain("--logger:tdd-guard", "wrapper should add the TDD Guard logger");
    }
    
    [Fact]
    public async Task WhenRunWithExistingLoggerArgument_ShouldNotDuplicateLogger()
    {
        // Act
        var (exitCode, output, errorOutput) = await RunWrapperAsync(_brokenProjectPath, "--logger:tdd-guard");
        
        // Assert
        exitCode.Should().NotBe(0);
        
        // Verify logger is not duplicated in the command
        var loggerCount = CountOccurrences(output, "--logger:tdd-guard");
        loggerCount.Should().Be(1, "should not duplicate the logger argument");
    }

    private async Task<(int exitCode, string output, string errorOutput)> RunWrapperAsync(string projectPath, params string[] additionalArgs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _wrapperExecutablePath,
            Arguments = $"\"{projectPath}\" {string.Join(" ", additionalArgs)}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = projectPath
        };
        
        // Set environment variable to control where test results are saved
        startInfo.Environment["TDD_GUARD_PROJECT_ROOT"] = _tempDataDirectory;

        using var process = new Process { StartInfo = startInfo };
        
        process.Start();
        
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        
        var output = await outputTask;
        var errorOutput = await errorTask;
        
        return (process.ExitCode, output, errorOutput);
    }
    
    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
    
    public void Dispose()
    {
        // Clean up temp directory
        if (Directory.Exists(_tempDataDirectory))
        {
            try
            {
                Directory.Delete(_tempDataDirectory, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}

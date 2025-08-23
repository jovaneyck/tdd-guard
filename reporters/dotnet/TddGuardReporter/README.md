# TDD Guard .NET Reporter

A .NET test logger for TDD Guard that captures test results and enforces Test-Driven Development principles.

## Features

- **Framework Agnostic**: Works with XUnit, MSTest, NUnit, and other .NET testing frameworks
- **Automatic Integration**: No code changes required - just add the logger to your test runs
- **TDD Validation**: Captures detailed test results for TDD Guard's AI-powered validation
- **Error Details**: Captures assertion failures, stack traces

## Installation

Install via NuGet:

```bash
dotnet add package tdd-guard-dotnet
```

## Usage

### With XUnit

```bash
# Run tests with TDD Guard logger
dotnet test --logger:tdd-guard

# With specific project root (recommended)
TDD_GUARD_PROJECT_ROOT=/path/to/project dotnet test --logger:tdd-guard
```

### With MSTest

```bash
dotnet test --logger:tdd-guard
```

### With NUnit

```bash
dotnet test --logger:tdd-guard
```

## Configuration

The logger supports configuration via environment variables:

- `TDD_GUARD_PROJECT_ROOT`: Absolute path to your project root (recommended)

If not set, test results are saved to `.claude/tdd-guard/data/test.json` relative to the test run directory.

## Example XUnit Test

```csharp
using Xunit;

namespace MyProject.Tests
{
    public class CalculatorTests
    {
        [Fact]
        public void Add_ShouldReturnSum_WhenGivenTwoNumbers()
        {
            // Arrange
            var calculator = new Calculator();
            
            // Act
            var result = calculator.Add(2, 3);
            
            // Assert
            Assert.Equal(5, result);
        }
        
        [Theory]
        [InlineData(1, 1, 2)]
        [InlineData(2, 3, 5)]
        [InlineData(-1, 1, 0)]
        public void Add_ShouldReturnExpectedSum(int a, int b, int expected)
        {
            // Arrange
            var calculator = new Calculator();
            
            // Act
            var result = calculator.Add(a, b);
            
            // Assert
            Assert.Equal(expected, result);
        }
    }
}
```

## Integration with TDD Guard

When using with TDD Guard (Claude Code hooks), the logger automatically:

1. Captures all test results (passed, failed, skipped)
2. Records assertion failures with detailed error messages
3. Groups tests by class/module for organized reporting
4. Saves results in JSON format for TDD Guard validation

## Troubleshooting

### Logger Not Found

If you get "Logger with FriendlyName 'tdd-guard' could not be found":

1. Ensure the package is installed: `dotnet list package | grep tdd-guard-dotnet`
2. Try restoring packages: `dotnet restore`
3. Check that you're running from the correct directory

### Results Not Saved

If test results aren't being saved:

1. Set the `TDD_GUARD_PROJECT_ROOT` environment variable
2. Check file permissions for the output directory
3. Run with verbose logging: `dotnet test --logger:tdd-guard --verbosity:normal`

## Output Format

The logger saves test results in the same JSON format as other TDD Guard reporters:

```json
{
  "testModules": [
    {
      "moduleId": "MyProject.Tests.CalculatorTests",
      "tests": [
        {
          "name": "Add_ShouldReturnSum_WhenGivenTwoNumbers",
          "fullName": "MyProject.Tests.CalculatorTests.Add_ShouldReturnSum_WhenGivenTwoNumbers",
          "state": "passed"
        }
      ]
    }
  ],
  "reason": "passed"
}
```
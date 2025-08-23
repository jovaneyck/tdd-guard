# TDD Guard .NET Test Wrapper

A .NET test wrapper that forwards all arguments to `dotnet test` while ensuring the TDD Guard logger is included and reporting compilation errors.

## Features

- **Argument Forwarding**: Passes all command-line arguments to `dotnet test`
- **Automatic Logger**: Automatically adds `--logger:tdd-guard` if not already present
- **Compilation Error Handling**: Captures and logs compilation errors in TDD Guard format when build fails
- **Output Forwarding**: Shows all `dotnet test` output in real-time

## Installation

```bash
dotnet pack
dotnet tool install --global --add-source ./src/bin/Debug/ TddGuard.Dotnet.TestWrapper
```

## Usage

Use exactly like `dotnet test`, but with the wrapper:

```bash
# Instead of: dotnet test
tdd-guard-dotnet-test

# Instead of: dotnet test MyProject.Tests
tdd-guard-dotnet-test MyProject.Tests

# Instead of: dotnet test --configuration Release --verbosity normal
tdd-guard-dotnet-test --configuration Release --verbosity normal
```

The wrapper will:
1. Add `--logger:tdd-guard` to capture test results
2. Forward all your arguments to `dotnet test`  
3. Show all output in real-time
4. If compilation fails, create a synthetic test result with the compilation error

## How it Works

- When `dotnet test` succeeds or fails with test failures: The existing TDD Guard logger captures results
- When `dotnet test` fails due to compilation errors: The wrapper creates a synthetic test result with compilation error details
- Results are saved to `.claude/tdd-guard/data/test.json` in the same format as the regular logger

## Environment Variables

- `TDD_GUARD_PROJECT_ROOT`: Override the project root directory (must be absolute path)

## Output Format

When compilation fails, creates a test result like:

```json
{
  "testModules": [
    {
      "moduleId": "CompilationError", 
      "tests": [
        {
          "name": "CompilationError",
          "fullName": "CompilationError.CompilationError",
          "state": "failed",
          "errors": [
            {
              "message": "error CS1002: ; expected...",
              "name": "CompilationError"
            }
          ]
        }
      ]
    }
  ],
  "reason": "failed"
}
```

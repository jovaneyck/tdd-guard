using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TddGuard.DotNet;

/// <summary>
/// Represents an error captured during Tests execution
/// </summary>
public class CapturedError
{
    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("actual")]
    public string? Actual { get; set; }

    [JsonPropertyName("expected")]
    public string? Expected { get; set; }

    [JsonPropertyName("showDiff")]
    public bool? ShowDiff { get; set; }

    [JsonPropertyName("operator")]
    public string? Operator { get; set; }

    [JsonPropertyName("diff")]
    public string? Diff { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ok")]
    public bool? Ok { get; set; }

    [JsonPropertyName("stack")]
    public string? Stack { get; set; }
}

/// <summary>
/// Represents a captured Tests result
/// </summary>
public class CapturedTest
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("fullName")]
    public required string FullName { get; set; }

    [JsonPropertyName("state")]
    public required string State { get; set; }

    [JsonPropertyName("errors")]
    public List<CapturedError>? Errors { get; set; }
}

/// <summary>
/// Represents a Tests module (typically a Tests class)
/// </summary>
public class CapturedModule
{
    [JsonPropertyName("moduleId")]
    public required string ModuleId { get; set; }

    [JsonPropertyName("tests")]
    public required List<CapturedTest> Tests { get; set; }
}

/// <summary>
/// Represents an unhandled error during Tests execution
/// </summary>
public class CapturedUnhandledError
{
    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("stack")]
    public string? Stack { get; set; }
}

/// <summary>
/// Represents the complete Tests run results
/// </summary>
public class CapturedTestRun
{
    [JsonPropertyName("testModules")]
    public required List<CapturedModule> TestModules { get; set; }

    [JsonPropertyName("unhandledErrors")]
    public List<CapturedUnhandledError>? UnhandledErrors { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
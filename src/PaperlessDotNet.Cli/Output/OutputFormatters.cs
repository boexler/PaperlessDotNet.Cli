using System.Text.Encodings.Web;
using System.Text.Json;

namespace PaperlessDotNet.Cli.Output;

/// <summary>
/// Output format for CLI results.
/// </summary>
public enum OutputFormat
{
    Table,
    Json
}

/// <summary>
/// Utilities for formatting CLI output.
/// </summary>
public static class OutputFormatters
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Serializes an object to JSON string.
    /// </summary>
    public static string ToJson(object? value)
    {
        if (value is null)
            return "null";

        return JsonSerializer.Serialize(value, JsonOptions);
    }
}

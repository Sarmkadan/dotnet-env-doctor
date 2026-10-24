using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetEnvDoctor;

/// <summary>
/// Serialises validation, diff and secret-detection results as JSON for CI consumption.
/// Output is shaped by private DTOs rather than the domain records, so the wire format stays stable
/// when the domain model changes. Every document starts with <c>"schemaVersion"</c>.
/// </summary>
public static class JsonReportWriter
{
    /// <summary>
    /// Version of the JSON document shape. Incremented only on breaking changes to the output.
    /// </summary>
    public const string SchemaVersion = "1";

    private static readonly JsonSerializerOptions IndentedOptions = CreateOptions(indented: true);
    private static readonly JsonSerializerOptions CompactOptions = CreateOptions(indented: false);

    /// <summary>
    /// Serialises a validation result to JSON.
    /// </summary>
    /// <param name="result">Validation result to serialise.</param>
    /// <param name="indented">Whether to format the output with indentation. Default: <c>true</c>.</param>
    /// <returns>A JSON document of the form <c>{ "schemaVersion", "isValid", "findings": [...] }</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c>.</exception>
    public static string Write(ValidationResult result, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Serialize(ToDto(result), indented);
    }

    /// <summary>
    /// Serialises a diff result to JSON.
    /// </summary>
    /// <param name="result">Diff result to serialise. Values are already redacted by the differ when a schema was supplied.</param>
    /// <param name="indented">Whether to format the output with indentation. Default: <c>true</c>.</param>
    /// <returns>A JSON document of the form <c>{ "schemaVersion", "left", "right", "isIdentical", "entries": [...] }</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c>.</exception>
    public static string Write(DiffResult result, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Serialize(ToDto(result), indented);
    }

    /// <summary>
    /// Serialises secret-detection issues to JSON.
    /// </summary>
    /// <param name="issues">Issues to serialise. Values are never included in the output.</param>
    /// <param name="indented">Whether to format the output with indentation. Default: <c>true</c>.</param>
    /// <returns>A JSON document of the form <c>{ "schemaVersion", "count", "issues": [...] }</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="issues"/> is <c>null</c>.</exception>
    public static string Write(IReadOnlyList<SecretIssue> issues, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return Serialize(ToDto(issues), indented);
    }

    /// <summary>
    /// Writes a validation result as indented JSON to <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">Destination writer. Flushed after writing.</param>
    /// <param name="result">Validation result to serialise.</param>
    /// <param name="ct">Token used to cancel the write.</param>
    /// <returns>A task that completes when the document has been written and flushed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="result"/> is <c>null</c>.</exception>
    public static Task WriteAsync(TextWriter writer, ValidationResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(result);
        return WriteDocumentAsync(writer, Write(result), ct);
    }

    /// <summary>
    /// Writes a diff result as indented JSON to <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">Destination writer. Flushed after writing.</param>
    /// <param name="result">Diff result to serialise.</param>
    /// <param name="ct">Token used to cancel the write.</param>
    /// <returns>A task that completes when the document has been written and flushed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="result"/> is <c>null</c>.</exception>
    public static Task WriteAsync(TextWriter writer, DiffResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(result);
        return WriteDocumentAsync(writer, Write(result), ct);
    }

    /// <summary>
    /// Writes secret-detection issues as indented JSON to <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">Destination writer. Flushed after writing.</param>
    /// <param name="issues">Issues to serialise.</param>
    /// <param name="ct">Token used to cancel the write.</param>
    /// <returns>A task that completes when the document has been written and flushed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="issues"/> is <c>null</c>.</exception>
    public static Task WriteAsync(TextWriter writer, IReadOnlyList<SecretIssue> issues, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(issues);
        return WriteDocumentAsync(writer, Write(issues), ct);
    }

    private static async Task WriteDocumentAsync(TextWriter writer, string json, CancellationToken ct)
    {
        await writer.WriteAsync(json.AsMemory(), ct).ConfigureAwait(false);
        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    private static string Serialize<T>(T dto, bool indented)
    {
        return JsonSerializer.Serialize(dto, indented ? IndentedOptions : CompactOptions);
    }

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static ValidationReportDto ToDto(ValidationResult result)
    {
        var findings = result.Findings
            .Select(f => new FindingDto(f.Key, f.Severity, f.Code, f.Message, f.Source))
            .ToList();

        return new ValidationReportDto(SchemaVersion, result.IsValid, findings);
    }

    private static DiffReportDto ToDto(DiffResult result)
    {
        var entries = result.Entries
            .Select(e => new DiffEntryDto(e.Key, e.Kind, e.LeftValue, e.RightValue))
            .ToList();

        return new DiffReportDto(SchemaVersion, result.LeftName, result.RightName, result.IsIdentical, entries);
    }

    private static SecretReportDto ToDto(IReadOnlyList<SecretIssue> issues)
    {
        var items = issues
            .Select(i => new SecretIssueDto(i.Key, i.Kind, i.Source, i.Message))
            .ToList();

        return new SecretReportDto(SchemaVersion, items.Count, items);
    }

    private sealed record ValidationReportDto(
        string SchemaVersion,
        bool IsValid,
        IReadOnlyList<FindingDto> Findings);

    private sealed record FindingDto(
        string Key,
        Severity Severity,
        string Code,
        string Message,
        string? Source);

    private sealed record DiffReportDto(
        string SchemaVersion,
        string Left,
        string Right,
        bool IsIdentical,
        IReadOnlyList<DiffEntryDto> Entries);

    private sealed record DiffEntryDto(
        string Key,
        DiffKind Kind,
        string? LeftValue,
        string? RightValue);

    private sealed record SecretReportDto(
        string SchemaVersion,
        int Count,
        IReadOnlyList<SecretIssueDto> Issues);

    private sealed record SecretIssueDto(
        string Key,
        SecretIssueKind Kind,
        string? Source,
        string Message);
}

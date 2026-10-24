using System.Text.Json;

namespace DotnetEnvDoctor.Tests;

public sealed class JsonReportWriterTests
{
    [Fact]
    public void Write_ValidationResult_EmitsSchemaVersionAndCamelCaseFields()
    {
        var result = new ValidationResult(new[]
        {
            new Finding("Api:Key", Severity.Error, FindingCodes.MissingRequired, "Required key is missing or empty", "appsettings.json"),
        });

        using var doc = JsonDocument.Parse(JsonReportWriter.Write(result, indented: false));
        var root = doc.RootElement;

        Assert.Equal(JsonReportWriter.SchemaVersion, root.GetProperty("schemaVersion").GetString());
        Assert.False(root.GetProperty("isValid").GetBoolean());

        var finding = Assert.Single(root.GetProperty("findings").EnumerateArray());
        Assert.Equal("Api:Key", finding.GetProperty("key").GetString());
        Assert.Equal("Error", finding.GetProperty("severity").GetString());
        Assert.Equal(FindingCodes.MissingRequired, finding.GetProperty("code").GetString());
        Assert.Equal("Required key is missing or empty", finding.GetProperty("message").GetString());
        Assert.Equal("appsettings.json", finding.GetProperty("source").GetString());
    }

    [Fact]
    public void Write_ValidationResult_SchemaVersionIsFirstProperty()
    {
        var json = JsonReportWriter.Write(new ValidationResult(Array.Empty<Finding>()), indented: false);

        Assert.StartsWith("{\"schemaVersion\":\"1\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_EmptyValidationResult_IsValidTrueAndEmptyFindings()
    {
        using var doc = JsonDocument.Parse(JsonReportWriter.Write(new ValidationResult(Array.Empty<Finding>())));
        var root = doc.RootElement;

        Assert.True(root.GetProperty("isValid").GetBoolean());
        Assert.Empty(root.GetProperty("findings").EnumerateArray());
    }

    [Fact]
    public void Write_DiffResult_EmitsEntriesWithKindAsString()
    {
        var result = new DiffResult(
            "Staging",
            "Production",
            new[]
            {
                new DiffEntry("Logging:Level", DiffKind.Changed, "Debug", "Warning"),
                new DiffEntry("Feature:X", DiffKind.OnlyInLeft, "on", null),
            });

        using var doc = JsonDocument.Parse(JsonReportWriter.Write(result, indented: false));
        var root = doc.RootElement;

        Assert.Equal("Staging", root.GetProperty("left").GetString());
        Assert.Equal("Production", root.GetProperty("right").GetString());
        Assert.False(root.GetProperty("isIdentical").GetBoolean());

        var entries = root.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("Changed", entries[0].GetProperty("kind").GetString());
        Assert.Equal("Debug", entries[0].GetProperty("leftValue").GetString());
        Assert.Equal("Warning", entries[0].GetProperty("rightValue").GetString());
        Assert.Equal("OnlyInLeft", entries[1].GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, entries[1].GetProperty("rightValue").ValueKind);
    }

    [Fact]
    public void Write_IdenticalDiff_IsIdenticalTrue()
    {
        var json = JsonReportWriter.Write(new DiffResult("a", "b", Array.Empty<DiffEntry>()));

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("isIdentical").GetBoolean());
    }

    [Fact]
    public void Write_SecretIssues_EmitsCountAndIssues()
    {
        IReadOnlyList<SecretIssue> issues = new[]
        {
            new SecretIssue("Db:Password", SecretIssueKind.PlaceholderValue, "secrets.json", "Secret holds a placeholder value"),
        };

        using var doc = JsonDocument.Parse(JsonReportWriter.Write(issues, indented: false));
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("count").GetInt32());
        var issue = Assert.Single(root.GetProperty("issues").EnumerateArray());
        Assert.Equal("Db:Password", issue.GetProperty("key").GetString());
        Assert.Equal("PlaceholderValue", issue.GetProperty("kind").GetString());
        Assert.Equal("secrets.json", issue.GetProperty("source").GetString());
        Assert.Equal("Secret holds a placeholder value", issue.GetProperty("message").GetString());
    }

    [Fact]
    public void Write_IndentedFlagTrue_ProducesMultipleLines()
    {
        var json = JsonReportWriter.Write(new ValidationResult(Array.Empty<Finding>()));

        Assert.Contains('\n', json);
    }

    [Fact]
    public void Write_IndentedFlagFalse_ProducesSingleLine()
    {
        var json = JsonReportWriter.Write(new ValidationResult(Array.Empty<Finding>()), indented: false);

        Assert.DoesNotContain('\n', json);
    }

    [Fact]
    public void Write_NullValidationResult_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => JsonReportWriter.Write((ValidationResult)null!));
    }

    [Fact]
    public void Write_NullDiffResult_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => JsonReportWriter.Write((DiffResult)null!));
    }

    [Fact]
    public void Write_NullSecretIssues_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => JsonReportWriter.Write((IReadOnlyList<SecretIssue>)null!));
    }

    [Fact]
    public async Task WriteAsync_ValidationResult_WritesDocumentToWriter()
    {
        var result = new ValidationResult(Array.Empty<Finding>());
        using var writer = new StringWriter();

        await JsonReportWriter.WriteAsync(writer, result);

        Assert.Equal(JsonReportWriter.Write(result), writer.ToString());
    }

    [Fact]
    public async Task WriteAsync_DiffResult_WritesDocumentToWriter()
    {
        var result = new DiffResult("a", "b", Array.Empty<DiffEntry>());
        using var writer = new StringWriter();

        await JsonReportWriter.WriteAsync(writer, result);

        Assert.Equal(JsonReportWriter.Write(result), writer.ToString());
    }

    [Fact]
    public async Task WriteAsync_SecretIssues_WritesDocumentToWriter()
    {
        IReadOnlyList<SecretIssue> issues = Array.Empty<SecretIssue>();
        using var writer = new StringWriter();

        await JsonReportWriter.WriteAsync(writer, issues);

        Assert.Equal(JsonReportWriter.Write(issues), writer.ToString());
    }

    [Fact]
    public async Task WriteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var writer = new StringWriter();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JsonReportWriter.WriteAsync(writer, new ValidationResult(Array.Empty<Finding>()), cts.Token));
    }

    [Fact]
    public async Task WriteAsync_NullWriter_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            JsonReportWriter.WriteAsync(null!, new ValidationResult(Array.Empty<Finding>())));
    }
}

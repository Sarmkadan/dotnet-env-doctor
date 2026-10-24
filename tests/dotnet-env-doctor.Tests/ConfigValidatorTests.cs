namespace DotnetEnvDoctor.Tests;

public sealed class ConfigValidatorTests
{
    private static ConfigSchema Schema(params ConfigKeySpec[] keys) => new(keys);

    private static ConfigSnapshot Snapshot(params (string Key, string? Value)[] values)
    {
        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            dictionary[key] = value;
        }

        return new ConfigSnapshot("appsettings.json", dictionary);
    }

    [Fact]
    public void Validate_MissingRequiredKey_ReturnsErrorED001()
    {
        var schema = Schema(new ConfigKeySpec("ConnectionStrings:Default"));

        var result = new ConfigValidator().Validate(schema, Snapshot());

        var finding = Assert.Single(result.Findings);
        Assert.Equal("ConnectionStrings:Default", finding.Key);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal(FindingCodes.MissingRequired, finding.Code);
        Assert.Equal("appsettings.json", finding.Source);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyRequiredValue_ReturnsErrorED001(string? value)
    {
        var schema = Schema(new ConfigKeySpec("Api:Key"));

        var result = new ConfigValidator().Validate(schema, Snapshot(("Api:Key", value)));

        Assert.Equal(FindingCodes.MissingRequired, Assert.Single(result.Findings).Code);
    }

    [Fact]
    public void Validate_MissingOptionalKey_ReturnsNoFinding()
    {
        var schema = Schema(new ConfigKeySpec("Feature:Flag", ConfigValueKind.Boolean, Required: false));

        var result = new ConfigValidator().Validate(schema, Snapshot());

        Assert.Empty(result.Findings);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_KindMismatch_ReturnsErrorED002WithoutValue()
    {
        var schema = Schema(new ConfigKeySpec("Smtp:Port", ConfigValueKind.Integer));

        var result = new ConfigValidator().Validate(schema, Snapshot(("Smtp:Port", "hunter2-not-a-number")));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(FindingCodes.KindMismatch, finding.Code);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal("Value does not match kind Integer: expected an integer (invariant culture)", finding.Message);
        Assert.DoesNotContain("hunter2", finding.Message);
    }

    [Fact]
    public void Validate_OptionalKeyWithInvalidValue_ReturnsErrorED002()
    {
        var schema = Schema(new ConfigKeySpec("Smtp:Port", ConfigValueKind.Integer, Required: false));

        var result = new ConfigValidator().Validate(schema, Snapshot(("Smtp:Port", "abc")));

        Assert.Equal(FindingCodes.KindMismatch, Assert.Single(result.Findings).Code);
    }

    [Fact]
    public void Validate_KeyLookup_IsCaseInsensitive()
    {
        var schema = Schema(new ConfigKeySpec("ConnectionStrings:Default"));

        var result = new ConfigValidator().Validate(schema, Snapshot(("connectionstrings:default", "Server=x")));

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Validate_UndeclaredKey_ReturnsWarningED004()
    {
        var schema = Schema();

        var result = new ConfigValidator().Validate(schema, Snapshot(("Legacy:Setting", "1")));

        var finding = Assert.Single(result.Findings);
        Assert.Equal("Legacy:Setting", finding.Key);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(FindingCodes.UndeclaredKey, finding.Code);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_UndeclaredKeysDisabled_ReturnsNoFinding()
    {
        var schema = Schema();
        var options = new ValidationOptions { ReportUndeclaredKeys = false };

        var result = new ConfigValidator().Validate(schema, Snapshot(("Legacy:Setting", "1")), options);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Validate_TreatWarningsAsErrors_PromotesWarningsToErrors()
    {
        var schema = Schema();
        var options = new ValidationOptions { TreatWarningsAsErrors = true };

        var result = new ConfigValidator().Validate(schema, Snapshot(("Legacy:Setting", "1")), options);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal(FindingCodes.UndeclaredKey, finding.Code);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_Findings_OrderedBySeverityThenKey()
    {
        var schema = Schema(new ConfigKeySpec("B"), new ConfigKeySpec("a"));

        var result = new ConfigValidator().Validate(schema, Snapshot(("z", "1")));

        Assert.Collection(
            result.Findings,
            f => Assert.Equal(("a", Severity.Error), (f.Key, f.Severity)),
            f => Assert.Equal(("B", Severity.Error), (f.Key, f.Severity)),
            f => Assert.Equal(("z", Severity.Warning), (f.Key, f.Severity)));
    }

    [Fact]
    public void Validate_ValidSnapshot_IsValidWithNoFindings()
    {
        var schema = Schema(
            new ConfigKeySpec("ConnectionStrings:Default", ConfigValueKind.ConnectionString),
            new ConfigKeySpec("Retries:Count", ConfigValueKind.Integer),
            new ConfigKeySpec("Site:Url", ConfigValueKind.Uri));

        var result = new ConfigValidator().Validate(
            schema,
            Snapshot(
                ("ConnectionStrings:Default", "Server=db"),
                ("Retries:Count", "3"),
                ("Site:Url", "https://example.com")));

        Assert.Empty(result.Findings);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PatternMismatch_ReturnsErrorED003WithoutValue()
    {
        var schema = Schema(new ConfigKeySpec("Region:Code", Pattern: "^[a-z]{2}$"));

        var result = new ConfigValidator().Validate(schema, Snapshot(("Region:Code", "SECRET-XYZ")));

        var finding = Assert.Single(result.Findings);
        Assert.Equal(FindingCodes.PatternMismatch, finding.Code);
        Assert.DoesNotContain("SECRET-XYZ", finding.Message);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_KindMismatchWithPattern_ReturnsOnlyED002()
    {
        var schema = Schema(new ConfigKeySpec("Smtp:Port", ConfigValueKind.Integer, Pattern: "^[0-9]+$"));

        var result = new ConfigValidator().Validate(schema, Snapshot(("Smtp:Port", "abc")));

        Assert.Equal(FindingCodes.KindMismatch, Assert.Single(result.Findings).Code);
    }

    [Fact]
    public void Validate_InvalidPattern_ReturnsErrorED005()
    {
        var schema = Schema(new ConfigKeySpec("Api:Key", Pattern: "[unclosed"));

        var result = new ConfigValidator().Validate(schema, Snapshot(("Api:Key", "value")));

        Assert.Equal(FindingCodes.InvalidPattern, Assert.Single(result.Findings).Code);
        Assert.False(result.IsValid);
    }
}

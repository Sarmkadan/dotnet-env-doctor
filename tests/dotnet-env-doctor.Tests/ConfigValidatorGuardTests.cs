namespace DotnetEnvDoctor.Tests;

public sealed class ConfigValidatorGuardTests
{
    private static ConfigSnapshot EmptySnapshot() =>
        new("appsettings.json", new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

    private static IConfigSchema SchemaWithNullKeys() => new NullKeysSchema();

    [Fact]
    public void Validate_NullSchema_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ConfigValidator().Validate(null!, EmptySnapshot()));

        Assert.Equal("schema", ex.ParamName);
    }

    [Fact]
    public void Validate_NullSnapshot_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ConfigValidator().Validate(new ConfigSchema([]), null!));

        Assert.Equal("snapshot", ex.ParamName);
    }

    [Fact]
    public void Validate_NullSchemaKeys_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConfigValidator().Validate(SchemaWithNullKeys(), EmptySnapshot()));

        Assert.Equal("schema", ex.ParamName);
        Assert.Contains("Keys", ex.Message);
    }

    [Fact]
    public void Constructor_NullFindings_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ValidationResult(null!));

        Assert.Equal("Findings", ex.ParamName);
    }

    [Fact]
    public void Deconstruct_ValidResult_ReturnsFindings()
    {
        var findings = Array.Empty<Finding>();
        var result = new ValidationResult(findings);

        result.Deconstruct(out var actual);

        Assert.Same(findings, actual);
    }

    private sealed class NullKeysSchema : IConfigSchema
    {
        public IReadOnlyList<ConfigKeySpec> Keys => null!;

        public bool TryGetSpec(string key, out ConfigKeySpec? spec)
        {
            spec = null;
            return false;
        }
    }
}

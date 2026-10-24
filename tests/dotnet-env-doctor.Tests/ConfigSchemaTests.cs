namespace DotnetEnvDoctor.Tests;

public sealed class ConfigSchemaTests
{
    private static readonly ConfigKeySpec Connection = new("ConnectionStrings:Default", ConfigValueKind.ConnectionString);
    private static readonly ConfigKeySpec Level = new("Logging:Level");
    private static readonly ConfigKeySpec Password = new("Db:Password", ConfigValueKind.Secret);

    private static ConfigSchema SampleSchema() => new(new[] { Password, Connection, Level });

    [Fact]
    public void Keys_PreservesDeclarationOrder()
    {
        var schema = SampleSchema();

        Assert.Equal(
            new[] { "Db:Password", "ConnectionStrings:Default", "Logging:Level" },
            schema.Keys.Select(k => k.Key));
    }

    [Fact]
    public void TryGetSpec_ExactKey_ReturnsSpec()
    {
        var schema = SampleSchema();

        Assert.True(schema.TryGetSpec("ConnectionStrings:Default", out var spec));
        Assert.Same(Connection, spec);
    }

    [Theory]
    [InlineData("connectionstrings:default")]
    [InlineData("CONNECTIONSTRINGS:DEFAULT")]
    [InlineData("ConnectionStrings:default")]
    public void TryGetSpec_DifferentCase_ReturnsSpec(string key)
    {
        var schema = SampleSchema();

        Assert.True(schema.TryGetSpec(key, out var spec));
        Assert.Same(Connection, spec);
    }

    [Fact]
    public void TryGetSpec_UnknownKey_ReturnsFalseAndNull()
    {
        var schema = SampleSchema();

        Assert.False(schema.TryGetSpec("Unknown:Key", out var spec));
        Assert.Null(spec);
    }

    [Fact]
    public void TryGetSpec_NullKey_ThrowsArgumentNullException()
    {
        var schema = SampleSchema();

        Assert.Throws<ArgumentNullException>(() => schema.TryGetSpec(null!, out _));
    }

    [Fact]
    public void Ctor_DuplicateKeysDifferentCase_ThrowsArgumentException()
    {
        var duplicate = new ConfigKeySpec("connectionstrings:default", ConfigValueKind.ConnectionString);

        var ex = Assert.Throws<ArgumentException>(() => new ConfigSchema(new[] { Connection, duplicate }));

        Assert.Contains("connectionstrings:default", ex.Message);
    }

    [Fact]
    public void Ctor_NullKeys_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigSchema(null!));
    }

    [Fact]
    public void Empty_HasNoKeys()
    {
        Assert.Empty(ConfigSchema.Empty.Keys);
    }
}

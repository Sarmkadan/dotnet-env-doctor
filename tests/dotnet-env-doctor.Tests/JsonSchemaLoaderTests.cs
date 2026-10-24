namespace DotnetEnvDoctor.Tests;

public sealed class JsonSchemaLoaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "envdoctor-schema-" + Guid.NewGuid().ToString("N"));

    public JsonSchemaLoaderTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_FullEntry_ParsesAllFields()
    {
        var path = WriteSchema("""
            { "keys": [ { "key": "ConnectionStrings:Default", "kind": "connectionstring", "required": false,
                          "pattern": "^Server=", "description": "Main DB" } ] }
            """);

        var schema = await new JsonSchemaLoader().LoadAsync(path);

        var spec = Assert.Single(schema.Keys);
        Assert.Equal("ConnectionStrings:Default", spec.Key);
        Assert.Equal(ConfigValueKind.ConnectionString, spec.Kind);
        Assert.False(spec.Required);
        Assert.Equal("^Server=", spec.Pattern);
        Assert.Equal("Main DB", spec.Description);
    }

    [Fact]
    public async Task LoadAsync_MinimalEntry_AppliesDefaults()
    {
        var path = WriteSchema("""{ "keys": [ { "key": "Logging:Level" } ] }""");

        var schema = await new JsonSchemaLoader().LoadAsync(path);

        var spec = Assert.Single(schema.Keys);
        Assert.Equal(ConfigValueKind.String, spec.Kind);
        Assert.True(spec.Required);
        Assert.Null(spec.Pattern);
        Assert.Null(spec.Description);
    }

    [Fact]
    public async Task LoadAsync_KeysLookupIsCaseInsensitive()
    {
        var path = WriteSchema("""{ "keys": [ { "key": "Logging:Level" } ] }""");

        var schema = await new JsonSchemaLoader().LoadAsync(path);

        Assert.True(schema.TryGetSpec("logging:level", out var spec));
        Assert.Equal("Logging:Level", spec!.Key);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_dir, "missing.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => new JsonSchemaLoader().LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_NullPath_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => new JsonSchemaLoader().LoadAsync(null!));
    }

    [Fact]
    public async Task LoadAsync_MalformedJson_ThrowsFormatExceptionWithJsonInner()
    {
        var path = WriteSchema("{ \"keys\": [");

        var ex = await Assert.ThrowsAsync<FormatException>(() => new JsonSchemaLoader().LoadAsync(path));

        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex.InnerException);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "keys": {} }""")]
    [InlineData("[]")]
    public async Task LoadAsync_KeysMissingOrNotArray_ThrowsFormatException(string json)
    {
        var path = WriteSchema(json);

        await Assert.ThrowsAsync<FormatException>(() => new JsonSchemaLoader().LoadAsync(path));
    }

    [Theory]
    [InlineData("""{ "keys": [ { "kind": "String" } ] }""")]
    [InlineData("""{ "keys": [ { "key": "" } ] }""")]
    [InlineData("""{ "keys": [ "Logging:Level" ] }""")]
    public async Task LoadAsync_EntryWithoutValidKey_ThrowsFormatException(string json)
    {
        var path = WriteSchema(json);

        await Assert.ThrowsAsync<FormatException>(() => new JsonSchemaLoader().LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_UnknownKind_ThrowsFormatException()
    {
        var path = WriteSchema("""{ "keys": [ { "key": "Port", "kind": "Number" } ] }""");

        await Assert.ThrowsAsync<FormatException>(() => new JsonSchemaLoader().LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_NonBooleanRequired_ThrowsFormatException()
    {
        var path = WriteSchema("""{ "keys": [ { "key": "Port", "required": "yes" } ] }""");

        await Assert.ThrowsAsync<FormatException>(() => new JsonSchemaLoader().LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_InvalidPattern_ThrowsFormatException()
    {
        var path = WriteSchema("""{ "keys": [ { "key": "Port", "pattern": "([" } ] }""");

        await Assert.ThrowsAsync<FormatException>(() => new JsonSchemaLoader().LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_DuplicateKeys_ThrowsFormatException()
    {
        var path = WriteSchema("""{ "keys": [ { "key": "Port" }, { "key": "port" } ] }""");

        await Assert.ThrowsAsync<FormatException>(() => new JsonSchemaLoader().LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var path = WriteSchema("""{ "keys": [] }""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new JsonSchemaLoader().LoadAsync(path, cts.Token));
    }

    [Fact]
    public void Parse_ValidJson_ReturnsSchema()
    {
        var schema = JsonSchemaLoader.Parse("""{ "keys": [ { "key": "Port", "kind": "integer" } ] }""");

        var spec = Assert.Single(schema.Keys);
        Assert.Equal(ConfigValueKind.Integer, spec.Kind);
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsFormatException()
    {
        var ex = Assert.Throws<FormatException>(() => JsonSchemaLoader.Parse("not json"));

        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex.InnerException);
    }

    [Fact]
    public void Parse_NullJson_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => JsonSchemaLoader.Parse(null!));
    }

    private string WriteSchema(string json)
    {
        var path = Path.Combine(_dir, "schema-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }
}

namespace DotnetEnvDoctor.Tests;

public sealed class JsonFileConfigSourceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "envdoctor-json-" + Guid.NewGuid().ToString("N"));

    public JsonFileConfigSourceTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task ReadAsync_NestedObjects_FlattensWithColon()
    {
        var path = WriteJson("""{"ConnectionStrings":{"Default":"x"}}""");

        var snapshot = await new JsonFileConfigSource(path).ReadAsync();

        var entry = Assert.Single(snapshot.Values);
        Assert.Equal("ConnectionStrings:Default", entry.Key);
        Assert.Equal("x", entry.Value);
    }

    [Fact]
    public async Task ReadAsync_Arrays_UseIndexKeys()
    {
        var path = WriteJson("""{"Hosts":["a","b"]}""");

        var snapshot = await new JsonFileConfigSource(path).ReadAsync();

        Assert.Equal(2, snapshot.Values.Count);
        Assert.Equal("a", snapshot.Values["Hosts:0"]);
        Assert.Equal("b", snapshot.Values["Hosts:1"]);
    }

    [Fact]
    public async Task ReadAsync_NumberAndBool_StoredAsRawText()
    {
        var path = WriteJson("""{"Port":42,"Enabled":true}""");

        var snapshot = await new JsonFileConfigSource(path).ReadAsync();

        Assert.Equal("42", snapshot.Values["Port"]);
        Assert.Equal("true", snapshot.Values["Enabled"]);
    }

    [Fact]
    public async Task ReadAsync_NullValue_StoredAsNull()
    {
        var path = WriteJson("""{"Optional":null}""");

        var snapshot = await new JsonFileConfigSource(path).ReadAsync();

        Assert.True(snapshot.Values.ContainsKey("Optional"));
        Assert.Null(snapshot.Values["Optional"]);
    }

    [Fact]
    public async Task ReadAsync_EmptyObject_ProducesNoKeys()
    {
        var path = WriteJson("{}");

        var snapshot = await new JsonFileConfigSource(path).ReadAsync();

        Assert.Empty(snapshot.Values);
    }

    [Fact]
    public async Task ReadAsync_Keys_AreCaseInsensitive()
    {
        var path = WriteJson("""{"ConnectionStrings":{"Default":"x"}}""");

        var snapshot = await new JsonFileConfigSource(path).ReadAsync();

        Assert.Equal("x", snapshot.Values["connectionstrings:default"]);
    }

    [Fact]
    public async Task ReadAsync_WithCommentsAndTrailingComma_Parses()
    {
        var path = WriteJson(
            """
            {
              // logging section
              "Logging": {
                "Level": "Information", /* inline */
              },
            }
            """);

        var snapshot = await new JsonFileConfigSource(path).ReadAsync();

        Assert.Equal("Information", snapshot.Values["Logging:Level"]);
    }

    [Fact]
    public async Task ReadAsync_RootIsArray_ThrowsFormatException()
    {
        var path = WriteJson("""["a","b"]""");

        await Assert.ThrowsAsync<FormatException>(() => new JsonFileConfigSource(path).ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_InvalidJson_ThrowsFormatException()
    {
        var path = WriteJson("""{"Key": """);

        await Assert.ThrowsAsync<FormatException>(() => new JsonFileConfigSource(path).ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_dir, "missing.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => new JsonFileConfigSource(path).ReadAsync());
    }

    [Fact]
    public void Name_ReturnsFileNameOnly()
    {
        var source = new JsonFileConfigSource(Path.Combine(_dir, "nested", "appsettings.Production.json"));

        Assert.Equal("appsettings.Production.json", source.Name);
    }

    [Fact]
    public void Ctor_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonFileConfigSource(null!));
    }

    private string WriteJson(string content)
    {
        var path = Path.Combine(_dir, "appsettings.json");
        File.WriteAllText(path, content);
        return path;
    }
}

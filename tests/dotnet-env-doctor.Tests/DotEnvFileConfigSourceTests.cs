namespace DotnetEnvDoctor.Tests;

public sealed class DotEnvFileConfigSourceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "envdoctor-dotenv-" + Guid.NewGuid().ToString("N"));

    public DotEnvFileConfigSourceTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData("KEY=value", "KEY", "value")]
    [InlineData("export KEY=value", "KEY", "value")]
    [InlineData("  KEY =  value  ", "KEY", "value")]
    [InlineData("KEY=a=b", "KEY", "a=b")]
    [InlineData("KEY=", "KEY", "")]
    [InlineData("A__B__C=1", "A:B:C", "1")]
    [InlineData("KEY=value # comment", "KEY", "value")]
    [InlineData("KEY=value#notcomment", "KEY", "value#notcomment")]
    [InlineData("KEY=#notcomment", "KEY", "#notcomment")]
    [InlineData("KEY= # only comment", "KEY", "")]
    public void TryParseLine_UnquotedAssignment_ParsesKeyAndValue(string line, string expectedKey, string expectedValue)
    {
        var parsed = DotEnvFileConfigSource.TryParseLine(line, out var key, out var value);

        Assert.True(parsed);
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("KEY='single # kept'", "single # kept")]
    [InlineData("KEY=\"double # kept\"", "double # kept")]
    [InlineData("KEY=\"  padded  \"", "  padded  ")]
    [InlineData("KEY='a\\nb'", "a\\nb")]
    [InlineData("KEY=\"a\\nb\"", "a\nb")]
    [InlineData("KEY=\"say \\\"hi\\\"\"", "say \"hi\"")]
    [InlineData("KEY=\"C:\\\\temp\"", "C:\\temp")]
    [InlineData("KEY=\"quoted\" # trailing comment", "quoted")]
    public void TryParseLine_QuotedValue_UnwrapsAndUnescapes(string line, string expectedValue)
    {
        var parsed = DotEnvFileConfigSource.TryParseLine(line, out _, out var value);

        Assert.True(parsed);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# KEY=value")]
    [InlineData("NO_EQUALS_SIGN")]
    [InlineData("=value")]
    [InlineData("KEY=\"unterminated")]
    [InlineData("KEY=\"quoted\" junk")]
    public void TryParseLine_IgnoredOrInvalidLine_ReturnsFalse(string line)
    {
        var parsed = DotEnvFileConfigSource.TryParseLine(line, out _, out _);

        Assert.False(parsed);
    }

    [Fact]
    public async Task ReadAsync_MixedFile_ReturnsFlattenedValues()
    {
        var path = WriteEnv("""
            # database settings
            ConnectionStrings__Default=Server=localhost;Database=app
            export Logging__LogLevel__Default="Information"

            Greeting='hello # not a comment'
            """);

        var snapshot = await new DotEnvFileConfigSource(path).ReadAsync();

        Assert.Equal(3, snapshot.Values.Count);
        Assert.Equal("Server=localhost;Database=app", snapshot.Values["ConnectionStrings:Default"]);
        Assert.Equal("Information", snapshot.Values["Logging:LogLevel:Default"]);
        Assert.Equal("hello # not a comment", snapshot.Values["Greeting"]);
    }

    [Fact]
    public async Task ReadAsync_DuplicateKeys_LaterValueWins()
    {
        var path = WriteEnv("KEY=first\nkey=second\n");

        var snapshot = await new DotEnvFileConfigSource(path).ReadAsync();

        var entry = Assert.Single(snapshot.Values);
        Assert.Equal("second", entry.Value);
    }

    [Fact]
    public async Task ReadAsync_LineWithoutEquals_ThrowsFormatExceptionWithLineNumber()
    {
        var path = WriteEnv("A=1\n\n# comment\nBROKEN\n");

        var ex = await Assert.ThrowsAsync<FormatException>(() => new DotEnvFileConfigSource(path).ReadAsync());

        Assert.Contains("line 4", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(_dir, "missing.env");

        await Assert.ThrowsAsync<FileNotFoundException>(() => new DotEnvFileConfigSource(path).ReadAsync());
    }

    [Fact]
    public void Name_ReturnsFileName()
    {
        var source = new DotEnvFileConfigSource(Path.Combine(_dir, "prod.env"));

        Assert.Equal("prod.env", source.Name);
    }

    [Fact]
    public void Constructor_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DotEnvFileConfigSource(null!));
    }

    private string WriteEnv(string content)
    {
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.env");
        File.WriteAllText(path, content);
        return path;
    }
}

namespace DotnetEnvDoctor.Tests;

public sealed class SecretDetectorGuardTests
{
    private static ConfigSnapshot EmptySnapshot(string name = "appsettings.json") =>
        new(name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void Scan_NullSchema_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SecretDetector().Scan(null!, new[] { EmptySnapshot() }));

        Assert.Equal("schema", ex.ParamName);
    }

    [Fact]
    public void Scan_NullSnapshots_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new SecretDetector().Scan(ConfigSchema.Empty, null!));

        Assert.Equal("snapshots", ex.ParamName);
    }

    [Fact]
    public void Scan_NullElement_ThrowsArgumentExceptionWithIndexInMessage()
    {
        var snapshots = new List<ConfigSnapshot> { EmptySnapshot("a.json"), null!, EmptySnapshot("c.json") };

        var ex = Assert.Throws<ArgumentException>(() =>
            new SecretDetector().Scan(ConfigSchema.Empty, snapshots));

        Assert.Equal("snapshots", ex.ParamName);
        Assert.Contains("[1]", ex.Message);
        Assert.Contains("snapshots[1] is null", ex.Message);
    }

    [Fact]
    public void Scan_NullFirstElement_ThrowsArgumentExceptionWithZeroIndex()
    {
        var snapshots = new List<ConfigSnapshot> { null! };

        var ex = Assert.Throws<ArgumentException>(() =>
            new SecretDetector().Scan(ConfigSchema.Empty, snapshots));

        Assert.Contains("snapshots[0] is null", ex.Message);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new SecretDetector(null!));

        Assert.Equal("options", ex.ParamName);
    }

    [Fact]
    public void SecretIssueConstructor_NullKey_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new SecretIssue(null!, SecretIssueKind.MissingSecret, null, "Not set"));

        Assert.Equal("Key", ex.ParamName);
    }

    [Fact]
    public void SecretIssueConstructor_WhitespaceKey_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new SecretIssue("   ", SecretIssueKind.MissingSecret, null, "Not set"));

        Assert.Equal("Key", ex.ParamName);
    }

    [Fact]
    public void SecretIssueConstructor_NullMessage_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new SecretIssue("Db:Password", SecretIssueKind.MissingSecret, null, null!));

        Assert.Equal("Message", ex.ParamName);
    }

    [Fact]
    public void SecretIssueConstructor_WhitespaceMessage_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new SecretIssue("Db:Password", SecretIssueKind.MissingSecret, null, "\t"));

        Assert.Equal("Message", ex.ParamName);
    }

    [Fact]
    public void SecretIssueConstructor_ValidArguments_SetsProperties()
    {
        var issue = new SecretIssue("Db:Password", SecretIssueKind.MissingSecret, "secrets.json", "Not set");

        Assert.Equal("Db:Password", issue.Key);
        Assert.Equal(SecretIssueKind.MissingSecret, issue.Kind);
        Assert.Equal("secrets.json", issue.Source);
        Assert.Equal("Not set", issue.Message);
    }
}

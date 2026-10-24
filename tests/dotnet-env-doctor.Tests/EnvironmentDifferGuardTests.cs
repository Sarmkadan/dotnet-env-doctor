namespace DotnetEnvDoctor.Tests;

public sealed class EnvironmentDifferGuardTests
{
    private static ConfigSnapshot EmptySnapshot(string name = "appsettings.json") =>
        new(name, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void Diff_NullLeft_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new EnvironmentDiffer().Diff(null!, EmptySnapshot()));

        Assert.Equal("left", ex.ParamName);
    }

    [Fact]
    public void Diff_NullRight_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new EnvironmentDiffer().Diff(EmptySnapshot(), null!));

        Assert.Equal("right", ex.ParamName);
    }

    [Fact]
    public void Diff_LeftWithNullValues_ThrowsArgumentException()
    {
        var left = EmptySnapshot() with { Values = null! };

        var ex = Assert.Throws<ArgumentException>(() =>
            new EnvironmentDiffer().Diff(left, EmptySnapshot()));

        Assert.Equal("left", ex.ParamName);
    }

    [Fact]
    public void Diff_RightWithNullValues_ThrowsArgumentException()
    {
        var right = EmptySnapshot() with { Values = null! };

        var ex = Assert.Throws<ArgumentException>(() =>
            new EnvironmentDiffer().Diff(EmptySnapshot(), right));

        Assert.Equal("right", ex.ParamName);
    }

    [Fact]
    public void Diff_NullSchema_ReturnsDifferences()
    {
        var left = new ConfigSnapshot("left.json", new Dictionary<string, string?> { ["Db:Host"] = "a" });
        var right = new ConfigSnapshot("right.json", new Dictionary<string, string?> { ["Db:Host"] = "b" });

        var result = new EnvironmentDiffer().Diff(left, right, schema: null);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(DiffKind.Changed, entry.Kind);
    }

    [Fact]
    public void ConfigSnapshotConstructor_NullName_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConfigSnapshot(null!, new Dictionary<string, string?>()));

        Assert.Equal("Name", ex.ParamName);
    }

    [Fact]
    public void ConfigSnapshotConstructor_EmptyName_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConfigSnapshot(string.Empty, new Dictionary<string, string?>()));

        Assert.Equal("Name", ex.ParamName);
    }

    [Fact]
    public void ConfigSnapshotConstructor_WhitespaceName_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new ConfigSnapshot("   ", new Dictionary<string, string?>()));

        Assert.Equal("Name", ex.ParamName);
    }

    [Fact]
    public void ConfigSnapshotConstructor_NullValues_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ConfigSnapshot("appsettings.json", null!));

        Assert.Equal("Values", ex.ParamName);
    }

    [Fact]
    public void DiffResultConstructor_NullEntries_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new DiffResult("left", "right", null!));

        Assert.Equal("Entries", ex.ParamName);
    }
}

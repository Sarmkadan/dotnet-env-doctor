namespace DotnetEnvDoctor.Tests;

public sealed class SnapshotMergerTests
{
    private static ConfigSnapshot Snap(string name, params (string key, string? value)[] pairs)
    {
        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
        {
            dictionary[key] = value;
        }

        return new ConfigSnapshot(name, dictionary);
    }

    [Fact]
    public void Merge_LaterSnapshotOverridesEarlier_ValueFromLast()
    {
        var snapshots = new[]
        {
            Snap("appsettings.json", ("Logging:LogLevel:Default", "Information"), ("ConnectionStrings:Default", "Server=dev")),
            Snap("appsettings.Production.json", ("Logging:LogLevel:Default", "Warning")),
            Snap("environment", ("Logging:LogLevel:Default", "Error")),
        };

        var merged = SnapshotMerger.Merge(snapshots);

        Assert.Equal("Error", merged.Snapshot.Values["Logging:LogLevel:Default"]);
        Assert.Equal("Server=dev", merged.Snapshot.Values["ConnectionStrings:Default"]);
    }

    [Fact]
    public void Merge_KeysDifferOnlyByCase_TreatedAsSameKey()
    {
        var snapshots = new[]
        {
            Snap("appsettings.json", ("Logging:LogLevel:Default", "Information")),
            Snap("environment", ("logging:loglevel:default", "Warning")),
        };

        var merged = SnapshotMerger.Merge(snapshots);

        var entry = Assert.Single(merged.Snapshot.Values);
        Assert.Equal("Warning", entry.Value);
        Assert.Equal("Warning", merged.Snapshot.Values["LOGGING:LOGLEVEL:DEFAULT"]);

        var origin = Assert.Single(merged.Origins);
        Assert.Equal("environment", origin.Value);
        Assert.Equal("environment", merged.Origins["LOGGING:LOGLEVEL:DEFAULT"]);
    }

    [Fact]
    public void Merge_NullInLaterSnapshot_OverridesEarlier()
    {
        var snapshots = new[]
        {
            Snap("appsettings.json", ("Db:Password", "from-file")),
            Snap("environment", ("Db:Password", null)),
        };

        var merged = SnapshotMerger.Merge(snapshots);

        Assert.True(merged.Snapshot.Values.ContainsKey("Db:Password"));
        Assert.Null(merged.Snapshot.Values["Db:Password"]);
        Assert.Equal("environment", merged.Origins["Db:Password"]);
    }

    [Fact]
    public void Merge_Origins_PointToWinningSource()
    {
        var snapshots = new[]
        {
            Snap("first", ("OnlyFirst", "1"), ("OverriddenTwice", "a"), ("OverriddenOnce", "b")),
            Snap("second", ("OverriddenTwice", "c"), ("OverriddenOnce", "d")),
            Snap("third", ("OverriddenTwice", "e")),
        };

        var merged = SnapshotMerger.Merge(snapshots);

        Assert.Equal(3, merged.Origins.Count);
        Assert.Equal("first", merged.Origins["OnlyFirst"]);
        Assert.Equal("second", merged.Origins["OverriddenOnce"]);
        Assert.Equal("third", merged.Origins["OverriddenTwice"]);
    }

    [Fact]
    public void Merge_DisjointKeys_UnionPreserved()
    {
        var snapshots = new[]
        {
            Snap("appsettings.json", ("App:Name", "Demo")),
            Snap("environment", ("Db:Host", "localhost")),
        };

        var merged = SnapshotMerger.Merge(snapshots);

        Assert.Equal(2, merged.Snapshot.Values.Count);
        Assert.Equal("Demo", merged.Snapshot.Values["App:Name"]);
        Assert.Equal("localhost", merged.Snapshot.Values["Db:Host"]);
        Assert.Equal("appsettings.json", merged.Origins["App:Name"]);
        Assert.Equal("environment", merged.Origins["Db:Host"]);
    }

    [Fact]
    public void Merge_EmptyList_ReturnsEmptySnapshotNamedEmpty()
    {
        var merged = SnapshotMerger.Merge(Array.Empty<ConfigSnapshot>());

        Assert.Equal("empty", merged.Snapshot.Name);
        Assert.Empty(merged.Snapshot.Values);
        Assert.Empty(merged.Origins);
    }

    [Fact]
    public void Merge_SingleSnapshot_ReturnsEquivalentValues()
    {
        var source = Snap("appsettings.json", ("App:Name", "Demo"), ("App:Secret", null));

        var merged = SnapshotMerger.Merge(new[] { source });

        Assert.Equal(source.Values.Count, merged.Snapshot.Values.Count);
        foreach (var (key, value) in source.Values)
        {
            Assert.True(merged.Snapshot.Values.ContainsKey(key));
            Assert.Equal(value, merged.Snapshot.Values[key]);
            Assert.Equal("appsettings.json", merged.Origins[key]);
        }
    }

    [Fact]
    public void Merge_Name_JoinsSourceNames()
    {
        var snapshots = new[]
        {
            Snap("appsettings.json", ("A", "1")),
            Snap("appsettings.Production.json", ("B", "2")),
            Snap("environment", ("C", "3")),
        };

        var merged = SnapshotMerger.Merge(snapshots);

        Assert.Contains("appsettings.json", merged.Snapshot.Name);
        Assert.Contains("appsettings.Production.json", merged.Snapshot.Name);
        Assert.Contains("environment", merged.Snapshot.Name);
    }

    [Fact]
    public void Merge_NullList_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SnapshotMerger.Merge((IReadOnlyList<ConfigSnapshot>)null!));
    }

    [Fact]
    public void Merge_NullElement_ThrowsWithIndexInMessage()
    {
        IReadOnlyList<ConfigSnapshot> snapshots = new[]
        {
            Snap("appsettings.json", ("A", "1")),
            null!,
            Snap("environment", ("B", "2")),
        };

        var exception = Assert.Throws<ArgumentNullException>(() => SnapshotMerger.Merge(snapshots));

        Assert.Contains("index 1", exception.Message);
    }
}

namespace DotnetEnvDoctor.Tests;

public sealed class EnvironmentDifferTests
{
    private const string StagingName = "appsettings.Staging.json";
    private const string ProductionName = "appsettings.Production.json";

    private static readonly IConfigSchema Schema = new ConfigSchema(
    [
        new ConfigKeySpec("ConnectionStrings:Default", ConfigValueKind.ConnectionString),
        new ConfigKeySpec("Api:Key", ConfigValueKind.Secret),
        new ConfigKeySpec("Api:Endpoint", ConfigValueKind.Uri),
    ]);

    private static ConfigSnapshot Snapshot(string name, params (string Key, string? Value)[] values)
    {
        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            dictionary[key] = value;
        }

        return new ConfigSnapshot(name, dictionary);
    }

    private static ConfigSnapshot Staging(params (string Key, string? Value)[] values) =>
        Snapshot(StagingName, values);

    private static ConfigSnapshot Production(params (string Key, string? Value)[] values) =>
        Snapshot(ProductionName, values);

    private static void AssertNoRawValue(DiffResult result, params string[] rawValues)
    {
        foreach (var entry in result.Entries)
        {
            var text = $"{entry.Key}|{entry.LeftValue}|{entry.RightValue}";
            foreach (var raw in rawValues)
            {
                Assert.DoesNotContain(raw, text);
            }
        }
    }

    [Fact]
    public void Diff_IdenticalSnapshots_IsIdenticalTrueAndNoEntries()
    {
        var staging = Staging(("Logging:Level", "Information"), ("Api:Endpoint", "https://api.example.test"));
        var production = Production(("Logging:Level", "Information"), ("Api:Endpoint", "https://api.example.test"));

        var result = new EnvironmentDiffer().Diff(staging, production, Schema);

        Assert.True(result.IsIdentical);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Diff_KeyOnlyInLeft_OnlyInLeftWithNullRight()
    {
        var staging = Staging(("Feature:NewUi", "true"));
        var production = Production();

        var result = new EnvironmentDiffer().Diff(staging, production);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Feature:NewUi", entry.Key);
        Assert.Equal(DiffKind.OnlyInLeft, entry.Kind);
        Assert.Equal("true", entry.LeftValue);
        Assert.Null(entry.RightValue);
    }

    [Fact]
    public void Diff_KeyOnlyInRight_OnlyInRightWithNullLeft()
    {
        var staging = Staging();
        var production = Production(("Feature:NewUi", "false"));

        var result = new EnvironmentDiffer().Diff(staging, production);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Feature:NewUi", entry.Key);
        Assert.Equal(DiffKind.OnlyInRight, entry.Kind);
        Assert.Null(entry.LeftValue);
        Assert.Equal("false", entry.RightValue);
    }

    [Fact]
    public void Diff_DifferentValues_Changed()
    {
        var staging = Staging(("Logging:Level", "Debug"));
        var production = Production(("Logging:Level", "Warning"));

        var result = new EnvironmentDiffer().Diff(staging, production);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Logging:Level", entry.Key);
        Assert.Equal(DiffKind.Changed, entry.Kind);
        Assert.Equal("Debug", entry.LeftValue);
        Assert.Equal("Warning", entry.RightValue);
    }

    [Fact]
    public void Diff_SameValueDifferentKeyCase_NoEntry()
    {
        var staging = Staging(("Api:Endpoint", "https://api.example.test"));
        var production = Production(("api:ENDPOINT", "https://api.example.test"));

        var result = new EnvironmentDiffer().Diff(staging, production);

        Assert.True(result.IsIdentical);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Diff_ValuesDifferOnlyByCase_ReportedAsChanged()
    {
        var staging = Staging(("Feature:Flag", "true"));
        var production = Production(("Feature:Flag", "True"));

        var result = new EnvironmentDiffer().Diff(staging, production);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(DiffKind.Changed, entry.Kind);
        Assert.Equal("true", entry.LeftValue);
        Assert.Equal("True", entry.RightValue);
    }

    [Fact]
    public void Diff_Entries_OrderedByKeyIgnoringCase()
    {
        // Ordinal order would be Alpha, Zeta, beta; case-insensitive order is Alpha, beta, Zeta.
        var staging = Staging(("Zeta:Mode", "a"), ("Alpha:Mode", "a"));
        var production = Production(("beta:Mode", "a"));

        var result = new EnvironmentDiffer().Diff(staging, production);

        Assert.Equal(
            ["Alpha:Mode", "beta:Mode", "Zeta:Mode"],
            result.Entries.Select(entry => entry.Key).ToArray());
    }

    [Fact]
    public void Diff_SecretKeyChanged_BothValuesRedacted()
    {
        const string stagingSecret = "staging-api-key-111";
        const string productionSecret = "production-api-key-222";
        var staging = Staging(("Api:Key", stagingSecret));
        var production = Production(("Api:Key", productionSecret));

        var result = new EnvironmentDiffer().Diff(staging, production, Schema);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(DiffKind.Changed, entry.Kind);
        Assert.Equal("***", entry.LeftValue);
        Assert.Equal("***", entry.RightValue);
        AssertNoRawValue(result, stagingSecret, productionSecret);
    }

    [Fact]
    public void Diff_SecretKeyOnlyInRight_LeftNullRightRedacted()
    {
        const string productionSecret = "production-api-key-222";
        var staging = Staging();
        var production = Production(("Api:Key", productionSecret));

        var result = new EnvironmentDiffer().Diff(staging, production, Schema);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(DiffKind.OnlyInRight, entry.Kind);
        Assert.Null(entry.LeftValue);
        Assert.Equal("***", entry.RightValue);
        AssertNoRawValue(result, productionSecret);
    }

    [Fact]
    public void Diff_ConnectionStringChanged_BothValuesRedacted()
    {
        const string stagingConnection = "Server=staging-db;Password=staging-pw-333";
        const string productionConnection = "Server=prod-db;Password=production-pw-444";
        var staging = Staging(("ConnectionStrings:Default", stagingConnection));
        var production = Production(("ConnectionStrings:Default", productionConnection));

        var result = new EnvironmentDiffer().Diff(staging, production, Schema);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("***", entry.LeftValue);
        Assert.Equal("***", entry.RightValue);
        AssertNoRawValue(result, stagingConnection, productionConnection);
    }

    [Fact]
    public void Diff_NonSecretKeyWithSchema_ValuesNotRedacted()
    {
        var staging = Staging(("Api:Endpoint", "https://staging.example.test"));
        var production = Production(("Api:Endpoint", "https://api.example.test"));

        var result = new EnvironmentDiffer().Diff(staging, production, Schema);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("https://staging.example.test", entry.LeftValue);
        Assert.Equal("https://api.example.test", entry.RightValue);
    }

    [Fact]
    public void Diff_NoSchema_NothingRedacted()
    {
        var staging = Staging(("Api:Key", "staging-api-key-111"));
        var production = Production(("Api:Key", "production-api-key-222"));

        var result = new EnvironmentDiffer().Diff(staging, production, schema: null);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("staging-api-key-111", entry.LeftValue);
        Assert.Equal("production-api-key-222", entry.RightValue);
    }

    [Fact]
    public void Diff_Names_CopiedFromSnapshots()
    {
        var staging = Staging(("Logging:Level", "Debug"));
        var production = Production(("Logging:Level", "Warning"));

        var result = new EnvironmentDiffer().Diff(staging, production);

        Assert.Equal(StagingName, result.LeftName);
        Assert.Equal(ProductionName, result.RightName);
    }
}

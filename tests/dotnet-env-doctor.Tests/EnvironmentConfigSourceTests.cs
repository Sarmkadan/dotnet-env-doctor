using System.Collections;

namespace DotnetEnvDoctor.Tests;

public sealed class EnvironmentConfigSourceTests
{
    [Fact]
    public async Task ReadAsync_DoubleUnderscore_BecomesColon()
    {
        var source = Source(null, ("ConnectionStrings__Default", "Server=db"));

        var snapshot = await source.ReadAsync();

        Assert.Equal("Server=db", snapshot.Values["ConnectionStrings:Default"]);
    }

    [Fact]
    public async Task ReadAsync_MultipleDoubleUnderscores_AllReplaced()
    {
        var source = Source(null, ("A__B__C", "value"));

        var snapshot = await source.ReadAsync();

        Assert.Equal("value", snapshot.Values["A:B:C"]);
        Assert.DoesNotContain("A__B__C", snapshot.Values.Keys);
    }

    [Fact]
    public async Task ReadAsync_WithPrefix_OnlyMatchingVariablesIncluded()
    {
        var source = Source("MYAPP_", ("MYAPP_Foo", "1"), ("OTHER_Bar", "2"));

        var snapshot = await source.ReadAsync();

        Assert.Equal("1", snapshot.Values["Foo"]);
        Assert.DoesNotContain("Bar", snapshot.Values.Keys);
        Assert.DoesNotContain("OTHER_Bar", snapshot.Values.Keys);
        Assert.Single(snapshot.Values);
    }

    [Fact]
    public async Task ReadAsync_PrefixMatchIsCaseInsensitive()
    {
        var source = Source("MYAPP_", ("myapp_Foo", "1"));

        var snapshot = await source.ReadAsync();

        Assert.Equal("1", snapshot.Values["Foo"]);
    }

    [Fact]
    public async Task ReadAsync_WithPrefix_PrefixStripped()
    {
        var source = Source("MYAPP_", ("MYAPP_Logging__LogLevel__Default", "Information"));

        var snapshot = await source.ReadAsync();

        Assert.Equal("Information", snapshot.Values["Logging:LogLevel:Default"]);
        Assert.DoesNotContain("MYAPP_Logging:LogLevel:Default", snapshot.Values.Keys);
    }

    [Fact]
    public async Task ReadAsync_PrefixOnlyVariable_Skipped()
    {
        var source = Source("MYAPP_", ("MYAPP_", "orphan"));

        var snapshot = await source.ReadAsync();

        Assert.Empty(snapshot.Values);
    }

    [Fact]
    public async Task ReadAsync_NoPrefix_IncludesAll()
    {
        var source = Source(null, ("MYAPP_Foo", "1"), ("OTHER_Bar", "2"));

        var snapshot = await source.ReadAsync();

        Assert.Equal("1", snapshot.Values["MYAPP_Foo"]);
        Assert.Equal("2", snapshot.Values["OTHER_Bar"]);
        Assert.Equal(2, snapshot.Values.Count);
    }

    [Fact]
    public async Task ReadAsync_Values_AreCaseInsensitiveLookup()
    {
        var source = Source(null, ("ConnectionStrings__Default", "Server=db"));

        var snapshot = await source.ReadAsync();

        Assert.Equal("Server=db", snapshot.Values["connectionstrings:default"]);
        Assert.Equal("Server=db", snapshot.Values["CONNECTIONSTRINGS:DEFAULT"]);
    }

    [Fact]
    public void Name_WithoutPrefix_IsEnvironment()
    {
        var source = Source(null);

        Assert.Equal("environment", source.Name);
    }

    [Fact]
    public void Name_WithPrefix_IncludesPrefix()
    {
        var source = Source("MYAPP_");

        Assert.Equal("environment (MYAPP_*)", source.Name);
    }

    [Fact]
    public async Task ReadAsync_RealEnvironment_ContainsPath()
    {
        var snapshot = await new EnvironmentConfigSource().ReadAsync();

        // Only assert when the platform exposes PATH at all; the lookup itself is case-insensitive.
        if (Environment.GetEnvironmentVariable("PATH") is not null)
        {
            Assert.Contains(snapshot.Values.Keys, key => string.Equals(key, "PATH", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static EnvironmentConfigSource Source(string? prefix, params (string Name, string? Value)[] variables)
    {
        var table = new Hashtable();
        foreach (var (name, value) in variables)
        {
            table[name] = value;
        }

        return new EnvironmentConfigSource(prefix, () => table);
    }
}

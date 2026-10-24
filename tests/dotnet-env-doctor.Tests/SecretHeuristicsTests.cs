namespace DotnetEnvDoctor.Tests;

public sealed class SecretHeuristicsTests
{
    private static readonly SecretDetectorOptions Options = new();

    [Fact]
    public void ShannonEntropy_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, SecretHeuristics.ShannonEntropy(string.Empty));
    }

    [Fact]
    public void ShannonEntropy_EightDistinctCharacters_ReturnsThree()
    {
        Assert.Equal(3.0, SecretHeuristics.ShannonEntropy("abcdefgh"), precision: 10);
    }

    [Fact]
    public void ShannonEntropy_RepeatedCharacter_ReturnsZero()
    {
        Assert.Equal(0, SecretHeuristics.ShannonEntropy("aaaaaaaa"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CHANGEME")]
    [InlineData("changeme")]
    [InlineData("  TODO  ")]
    [InlineData("<your-token>")]
    [InlineData("${DB_PASSWORD}")]
    [InlineData("%DB_PASSWORD%")]
    public void IsPlaceholder_PlaceholderOrEmptyForm_ReturnsTrue(string? value)
    {
        Assert.True(SecretHeuristics.IsPlaceholder(value, Options.PlaceholderValues));
    }

    [Theory]
    [InlineData("s3cr3t-Value-42")]
    [InlineData("<unterminated")]
    [InlineData("$DB_PASSWORD")]
    public void IsPlaceholder_RealValue_ReturnsFalse(string value)
    {
        Assert.False(SecretHeuristics.IsPlaceholder(value, Options.PlaceholderValues));
    }

    [Fact]
    public void IsPlaceholder_NullPlaceholders_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SecretHeuristics.IsPlaceholder("x", null!));
    }

    [Fact]
    public void HasKnownTokenPrefix_MatchingPrefix_ReturnsTrue()
    {
        Assert.True(SecretHeuristics.HasKnownTokenPrefix("ghp_abc123", Options.KnownTokenPrefixes));
    }

    [Fact]
    public void HasKnownTokenPrefix_DifferentCase_ReturnsFalse()
    {
        Assert.False(SecretHeuristics.HasKnownTokenPrefix("akia0000", Options.KnownTokenPrefixes));
    }

    [Fact]
    public void HasKnownTokenPrefix_EmptyPrefixList_ReturnsFalse()
    {
        Assert.False(SecretHeuristics.HasKnownTokenPrefix("sk-abc", Array.Empty<string>()));
    }

    [Fact]
    public void LooksLikeSecret_NullValue_ReturnsFalse()
    {
        Assert.False(SecretHeuristics.LooksLikeSecret(null, Options));
    }

    [Fact]
    public void LooksLikeSecret_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SecretHeuristics.LooksLikeSecret("sk-abc", null!));
    }

    [Fact]
    public void LooksLikeSecret_ValueWithWhitespace_ReturnsFalse()
    {
        Assert.False(SecretHeuristics.LooksLikeSecret("ghp_abc def ghi jkl mno", Options));
    }

    [Fact]
    public void LooksLikeSecret_ShorterThanMinimum_ReturnsFalse()
    {
        Assert.False(SecretHeuristics.LooksLikeSecret("sk-short", Options));
    }

    [Fact]
    public void LooksLikeSecret_KnownTokenPrefix_ReturnsTrue()
    {
        Assert.True(SecretHeuristics.LooksLikeSecret("ghp_0123456789abcdef", Options));
    }

    [Fact]
    public void LooksLikeSecret_HighEntropyValue_ReturnsTrue()
    {
        Assert.True(SecretHeuristics.LooksLikeSecret("Qz7$pLm2Vx9RtBn4Wc8Y", Options));
    }

    [Theory]
    [InlineData("https://example.com/api")]
    [InlineData("Information")]
    [InlineData("true")]
    [InlineData("False")]
    [InlineData("12345678901234567890")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void LooksLikeSecret_CommonNonSecret_ReturnsFalse(string value)
    {
        Assert.False(SecretHeuristics.LooksLikeSecret(value, Options));
    }

    [Theory]
    [InlineData("a-", "a-", true)]
    [InlineData("A-", "a-", false)]
    public void HasKnownTokenPrefix_PrefixCase_IsOrdinal(string value, string prefix, bool expected)
    {
        Assert.Equal(expected, SecretHeuristics.HasKnownTokenPrefix(value, new[] { prefix }));
    }

    [Theory]
    [InlineData("appsettings.json", "appsettings.json", true)]
    [InlineData("appsettings.Production.json", "appsettings.*.json", true)]
    [InlineData("APPSETTINGS.JSON", "appsettings.json", true)]
    [InlineData("appsettings.json.bak", "appsettings.json", false)]
    [InlineData("config1.env.example", "config?.env.example", true)]
    [InlineData("config10.env.example", "config?.env.example", false)]
    [InlineData("a.b", "a.b", true)]
    [InlineData("axb", "a.b", false)]
    public void MatchesGlob_Pattern_MatchesWholeNameCaseInsensitively(string name, string pattern, bool expected)
    {
        Assert.Equal(expected, SecretHeuristics.MatchesGlob(name, pattern));
    }

    [Fact]
    public void MatchesGlob_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SecretHeuristics.MatchesGlob(null!, "*"));
    }
}

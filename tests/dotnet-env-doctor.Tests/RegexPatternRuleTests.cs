namespace DotnetEnvDoctor.Tests;

public sealed class RegexPatternRuleTests
{
    [Fact]
    public void Evaluate_NoPattern_ReturnsNull()
    {
        var spec = new ConfigKeySpec("Api:Key");

        Assert.Null(new RegexPatternRule().Evaluate(spec, "anything", "appsettings.json"));
    }

    [Fact]
    public void Evaluate_NullValue_ReturnsNull()
    {
        var spec = new ConfigKeySpec("Api:Key", Pattern: "^[a-z]+$");

        Assert.Null(new RegexPatternRule().Evaluate(spec, null, "appsettings.json"));
    }

    [Fact]
    public void Evaluate_MatchingValue_ReturnsNull()
    {
        var spec = new ConfigKeySpec("Region:Code", Pattern: "^[a-z]{2}-[a-z]+$");

        Assert.Null(new RegexPatternRule().Evaluate(spec, "eu-west", "appsettings.json"));
    }

    [Fact]
    public void Evaluate_MismatchedValue_ReturnsErrorED003WithoutValue()
    {
        var spec = new ConfigKeySpec("Region:Code", Pattern: "^[a-z]{2}-[a-z]+$");

        var finding = new RegexPatternRule().Evaluate(spec, "SECRET-VALUE-123", "appsettings.json");

        Assert.NotNull(finding);
        Assert.Equal("Region:Code", finding.Key);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal(FindingCodes.PatternMismatch, finding.Code);
        Assert.Equal("Value does not match pattern '^[a-z]{2}-[a-z]+$'", finding.Message);
        Assert.DoesNotContain("SECRET-VALUE-123", finding.Message);
        Assert.Equal("appsettings.json", finding.Source);
    }

    [Fact]
    public void Evaluate_CalledTwiceWithSamePattern_ReusesCompiledRegex()
    {
        var rule = new RegexPatternRule();
        var spec = new ConfigKeySpec("Region:Code", Pattern: "^[a-z]+$");

        Assert.Null(rule.Evaluate(spec, "abc", null));
        Assert.NotNull(rule.Evaluate(spec, "ABC", null));
        Assert.Null(rule.Evaluate(spec, "xyz", null));
    }

    [Fact]
    public void Evaluate_InvalidPattern_ReturnsErrorED005WithoutThrowing()
    {
        var spec = new ConfigKeySpec("Api:Key", Pattern: "[unclosed");

        var finding = new RegexPatternRule().Evaluate(spec, "value", "appsettings.json");

        Assert.NotNull(finding);
        Assert.Equal(FindingCodes.InvalidPattern, finding.Code);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal("Pattern is invalid or timed out", finding.Message);
        Assert.Equal("Api:Key", finding.Key);
    }

    [Fact]
    public void Evaluate_CatastrophicPatternExceedsTimeout_ReturnsErrorED005WithoutThrowing()
    {
        var rule = new RegexPatternRule(TimeSpan.FromMilliseconds(50));
        var spec = new ConfigKeySpec("Api:Key", Pattern: "^(a+)+$");

        var finding = rule.Evaluate(spec, new string('a', 60) + "!", null);

        Assert.NotNull(finding);
        Assert.Equal(FindingCodes.InvalidPattern, finding.Code);
        Assert.Equal("Pattern is invalid or timed out", finding.Message);
    }

    [Fact]
    public void Constructor_ZeroTimeout_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegexPatternRule(TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_NegativeTimeout_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RegexPatternRule(TimeSpan.FromMilliseconds(-2)));
    }
}

namespace DotnetEnvDoctor.Tests;

public sealed class ValueKindCheckerTests
{
    [Theory]
    [InlineData(ConfigValueKind.String, "")]
    [InlineData(ConfigValueKind.Secret, "anything at all")]
    [InlineData(ConfigValueKind.ConnectionString, "Server=db;Password=x")]
    public void IsValid_OpaqueKind_AcceptsAnyString(ConfigValueKind kind, string value)
    {
        bool valid = ValueKindChecker.IsValid(kind, value, out string? reason);

        Assert.True(valid);
        Assert.Null(reason);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("-7")]
    [InlineData("  15  ")]
    [InlineData("9223372036854775807")]
    public void IsValid_Integer_AcceptsIntegers(string value)
    {
        bool valid = ValueKindChecker.IsValid(ConfigValueKind.Integer, value, out string? reason);

        Assert.True(valid);
        Assert.Null(reason);
    }

    [Theory]
    [InlineData("4.2")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1,000")]
    public void IsValid_Integer_RejectsNonIntegers(string value)
    {
        bool valid = ValueKindChecker.IsValid(ConfigValueKind.Integer, value, out string? reason);

        Assert.False(valid);
        Assert.Equal("expected an integer (invariant culture)", reason);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("False")]
    [InlineData("TRUE")]
    public void IsValid_Boolean_AcceptsTrueAndFalseCaseInsensitively(string value)
    {
        bool valid = ValueKindChecker.IsValid(ConfigValueKind.Boolean, value, out string? reason);

        Assert.True(valid);
        Assert.Null(reason);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    public void IsValid_Boolean_RejectsNonBooleanLiterals(string value)
    {
        bool valid = ValueKindChecker.IsValid(ConfigValueKind.Boolean, value, out string? reason);

        Assert.False(valid);
        Assert.Equal("expected a boolean (true or false)", reason);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost:8080/api")]
    public void IsValid_Uri_AcceptsAbsoluteUriWithSchemeAndHost(string value)
    {
        bool valid = ValueKindChecker.IsValid(ConfigValueKind.Uri, value, out string? reason);

        Assert.True(valid);
        Assert.Null(reason);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("C:\\path")]
    [InlineData("")]
    public void IsValid_Uri_RejectsRelativeOrHostless(string value)
    {
        bool valid = ValueKindChecker.IsValid(ConfigValueKind.Uri, value, out string? reason);

        Assert.False(valid);
        Assert.Equal("expected an absolute URI with a scheme and host", reason);
    }

    [Fact]
    public void IsValid_InvalidValue_ReasonDoesNotEchoValue()
    {
        const string secret = "s3cr3t-not-a-number";

        ValueKindChecker.IsValid(ConfigValueKind.Integer, secret, out string? reason);

        Assert.NotNull(reason);
        Assert.DoesNotContain(secret, reason);
    }

    [Fact]
    public void IsValid_NullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ValueKindChecker.IsValid(ConfigValueKind.String, null!, out _));
    }

    [Fact]
    public void IsValid_UnknownKind_ThrowsArgumentOutOfRangeException()
    {
        const ConfigValueKind unknown = (ConfigValueKind)999;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ValueKindChecker.IsValid(unknown, "value", out _));
    }
}

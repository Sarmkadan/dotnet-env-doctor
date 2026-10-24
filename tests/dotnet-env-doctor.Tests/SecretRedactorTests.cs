namespace DotnetEnvDoctor.Tests;

public sealed class SecretRedactorTests
{
    private static readonly ConfigSchema Schema = new(new[]
    {
        new ConfigKeySpec("Db:Password", ConfigValueKind.Secret),
        new ConfigKeySpec("ConnectionStrings:Default", ConfigValueKind.ConnectionString),
        new ConfigKeySpec("Logging:Level", ConfigValueKind.String),
    });

    [Theory]
    [InlineData(ConfigValueKind.Secret, true)]
    [InlineData(ConfigValueKind.ConnectionString, true)]
    [InlineData(ConfigValueKind.String, false)]
    [InlineData(ConfigValueKind.Integer, false)]
    [InlineData(ConfigValueKind.Boolean, false)]
    [InlineData(ConfigValueKind.Uri, false)]
    public void IsSecretKind_Kind_MatchesRedactionPolicy(ConfigValueKind kind, bool expected)
    {
        Assert.Equal(expected, SecretRedactor.IsSecretKind(kind));
    }

    [Fact]
    public void IsSecretKey_NullSchema_ReturnsFalse()
    {
        Assert.False(SecretRedactor.IsSecretKey(null, "Db:Password"));
    }

    [Fact]
    public void IsSecretKey_UndeclaredKey_ReturnsFalse()
    {
        Assert.False(SecretRedactor.IsSecretKey(Schema, "Unknown:Key"));
    }

    [Fact]
    public void IsSecretKey_SecretKeyDifferentCase_ReturnsTrue()
    {
        Assert.True(SecretRedactor.IsSecretKey(Schema, "db:password"));
    }

    [Fact]
    public void IsSecretKey_NullKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SecretRedactor.IsSecretKey(Schema, null!));
    }

    [Theory]
    [InlineData("hunter2")]
    [InlineData(" ")]
    public void Redact_NonEmptyValue_ReturnsMask(string value)
    {
        Assert.Equal(SecretRedactor.Mask, SecretRedactor.Redact(value));
    }

    [Fact]
    public void Redact_NullValue_ReturnsNull()
    {
        Assert.Null(SecretRedactor.Redact(null));
    }

    [Fact]
    public void Redact_EmptyValue_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SecretRedactor.Redact(string.Empty));
    }

    [Fact]
    public void RedactIfSecret_SecretKey_MasksValue()
    {
        Assert.Equal(SecretRedactor.Mask, SecretRedactor.RedactIfSecret(Schema, "Db:Password", "hunter2"));
    }

    [Fact]
    public void RedactIfSecret_NonSecretKey_ReturnsValueUnchanged()
    {
        Assert.Equal("Debug", SecretRedactor.RedactIfSecret(Schema, "Logging:Level", "Debug"));
    }

    [Fact]
    public void RedactIfSecret_NullSchema_ReturnsValueUnchanged()
    {
        Assert.Equal("hunter2", SecretRedactor.RedactIfSecret(null, "Db:Password", "hunter2"));
    }

    [Fact]
    public void RedactIfSecret_SecretKeyNullValue_ReturnsNull()
    {
        Assert.Null(SecretRedactor.RedactIfSecret(Schema, "Db:Password", null));
    }

    [Fact]
    public void RedactSnapshot_MasksOnlySecretValues()
    {
        var snapshot = new ConfigSnapshot("prod.json", new Dictionary<string, string?>
        {
            ["Db:Password"] = "hunter2",
            ["ConnectionStrings:Default"] = "Server=db;Password=x",
            ["Logging:Level"] = "Warning",
            ["Empty:Secret"] = null,
        });

        var redacted = SecretRedactor.RedactSnapshot(Schema, snapshot);

        Assert.Equal("prod.json", redacted.Name);
        Assert.Equal(SecretRedactor.Mask, redacted.Values["Db:Password"]);
        Assert.Equal(SecretRedactor.Mask, redacted.Values["ConnectionStrings:Default"]);
        Assert.Equal("Warning", redacted.Values["Logging:Level"]);
        Assert.Null(redacted.Values["Empty:Secret"]);
    }

    [Fact]
    public void RedactSnapshot_DoesNotModifyOriginal()
    {
        var values = new Dictionary<string, string?> { ["Db:Password"] = "hunter2" };
        var snapshot = new ConfigSnapshot("prod.json", values);

        SecretRedactor.RedactSnapshot(Schema, snapshot);

        Assert.Equal("hunter2", snapshot.Values["Db:Password"]);
    }

    [Fact]
    public void RedactSnapshot_ResultLookupIsCaseInsensitive()
    {
        var snapshot = new ConfigSnapshot("prod.json", new Dictionary<string, string?> { ["Db:Password"] = "hunter2" });

        var redacted = SecretRedactor.RedactSnapshot(Schema, snapshot);

        Assert.Equal(SecretRedactor.Mask, redacted.Values["db:password"]);
    }

    [Fact]
    public void RedactSnapshot_NullSchema_Throws()
    {
        var snapshot = new ConfigSnapshot("prod.json", new Dictionary<string, string?>());

        Assert.Throws<ArgumentNullException>(() => SecretRedactor.RedactSnapshot(null!, snapshot));
    }

    [Fact]
    public void RedactSnapshot_NullSnapshot_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SecretRedactor.RedactSnapshot(Schema, null!));
    }
}

namespace DotnetEnvDoctor.Tests;

public sealed class SecretDetectorTests
{
    private const string AppSettings = "appsettings.json";
    private const string Production = "appsettings.Production.json";
    private const string EnvironmentSource = "environment";

    // Fake fixtures. They are only used to prove that values never leak into issue messages.
    private const string RealApiKey = "Qz7pLm2Vx9RtBn4Wc8Yd";
    private const string RealSmtpPassword = "Mx4vKe9Qa2Lp7Zr";
    private const string RealConnection = "Server=db.internal;Database=app;User Id=svc;Password=Pq8rTz2Wm5Lk";
    private const string HighEntropyLogLevel = "q8Zt3LxV7mNcR1pWbK9sYd2HfGjE4uAo";
    private const string GhToken = "ghp_aB3dE5fG7hJ9kL1mN2pQ4rS6tU8vW0";

    private static readonly IConfigSchema Schema = new ConfigSchema(new[]
    {
        new ConfigKeySpec("ConnectionStrings:Default", ConfigValueKind.ConnectionString),
        new ConfigKeySpec("Api:Key", ConfigValueKind.Secret),
        new ConfigKeySpec("Smtp:Password", ConfigValueKind.Secret),
        new ConfigKeySpec("Logging:LogLevel:Default", ConfigValueKind.String),
    });

    [Fact]
    public void Scan_SecretAbsentEverywhere_MissingSecret()
    {
        var issues = Scan(
            Snapshot(AppSettings, ("Logging:LogLevel:Default", "Information")),
            Snapshot(Production),
            Snapshot(EnvironmentSource, ("ConnectionStrings:Default", RealConnection), ("Smtp:Password", RealSmtpPassword)));

        var issue = Assert.Single(issues, i => i.Key == "Api:Key");
        Assert.Equal(SecretIssueKind.MissingSecret, issue.Kind);
        Assert.Null(issue.Source);
    }

    [Fact]
    public void Scan_SecretEmptyInAllSources_MissingSecret()
    {
        var issues = Scan(
            Snapshot(AppSettings, ("Api:Key", string.Empty), ("Logging:LogLevel:Default", "Information")),
            Snapshot(Production, ("Api:Key", "   ")),
            Snapshot(EnvironmentSource, ("Api:Key", string.Empty), ("ConnectionStrings:Default", RealConnection), ("Smtp:Password", RealSmtpPassword)));

        var issue = Assert.Single(issues, i => i.Key == "Api:Key");
        Assert.Equal(SecretIssueKind.MissingSecret, issue.Kind);
    }

    [Fact]
    public void Scan_SecretOnlyInEnvironment_NoIssue()
    {
        var issues = Scan(Healthy());

        Assert.Empty(issues);
    }

    [Fact]
    public void Scan_SecretIsCHANGEME_PlaceholderValue()
    {
        var issues = Scan(
            Snapshot(AppSettings, ("Api:Key", "CHANGEME"), ("Logging:LogLevel:Default", "Information")),
            Snapshot(Production),
            Snapshot(EnvironmentSource, ("ConnectionStrings:Default", RealConnection), ("Smtp:Password", RealSmtpPassword)));

        var issue = Assert.Single(issues, i => i.Key == "Api:Key");
        Assert.Equal(SecretIssueKind.PlaceholderValue, issue.Kind);
        Assert.Equal(AppSettings, issue.Source);
    }

    [Fact]
    public void Scan_SecretIsAngleBracketTemplate_PlaceholderValue()
    {
        var issues = Scan(
            Snapshot(AppSettings, ("Api:Key", "<your-api-key>"), ("Logging:LogLevel:Default", "Information")),
            Snapshot(Production),
            Snapshot(EnvironmentSource, ("ConnectionStrings:Default", RealConnection), ("Smtp:Password", RealSmtpPassword)));

        var issue = Assert.Single(issues, i => i.Key == "Api:Key");
        Assert.Equal(SecretIssueKind.PlaceholderValue, issue.Kind);
        Assert.Equal(AppSettings, issue.Source);
    }

    [Fact]
    public void Scan_PlaceholderInJsonOverriddenByEnvironment_NoPlaceholderIssue()
    {
        // The placeholder in appsettings.json is skipped by the committed-file check, and the
        // environment value wins the merge, so nothing should be reported.
        var issues = Scan(
            Snapshot(AppSettings, ("Api:Key", "CHANGEME"), ("Logging:LogLevel:Default", "Information")),
            Snapshot(Production),
            Snapshot(EnvironmentSource, ("ConnectionStrings:Default", RealConnection), ("Api:Key", RealApiKey), ("Smtp:Password", RealSmtpPassword)));

        Assert.DoesNotContain(issues, i => i.Kind == SecretIssueKind.PlaceholderValue);
        Assert.Empty(issues);
    }

    [Fact]
    public void Scan_RealSecretInAppsettingsJson_SecretInCommittedFile()
    {
        var issues = Scan(
            Snapshot(AppSettings, ("Api:Key", RealApiKey), ("Logging:LogLevel:Default", "Information")),
            Snapshot(Production),
            Snapshot(EnvironmentSource, ("ConnectionStrings:Default", RealConnection), ("Smtp:Password", RealSmtpPassword)));

        var issue = Assert.Single(issues, i => i.Key == "Api:Key");
        Assert.Equal(SecretIssueKind.SecretInCommittedFile, issue.Kind);
        Assert.Equal(AppSettings, issue.Source);
    }

    [Fact]
    public void Scan_RealSecretInEnvironment_NotReportedAsCommitted()
    {
        var issues = Scan(Healthy());

        Assert.DoesNotContain(issues, i => i.Kind == SecretIssueKind.SecretInCommittedFile);
    }

    [Fact]
    public void Scan_HighEntropyValueOnStringKey_UndeclaredSecretLike()
    {
        var issues = Scan(Healthy(environmentLogLevel: HighEntropyLogLevel));

        var issue = Assert.Single(issues, i => i.Key == "Logging:LogLevel:Default");
        Assert.Equal(SecretIssueKind.UndeclaredSecretLike, issue.Kind);
        Assert.Equal(EnvironmentSource, issue.Source);
    }

    [Fact]
    public void Scan_UrlOnStringKey_NotSecretLike()
    {
        var issues = Scan(Healthy(environmentLogLevel: "https://logs.example.com/api/v1/ingest"));

        Assert.Empty(issues);
    }

    [Fact]
    public void Scan_KnownPrefixTokenOnStringKey_UndeclaredSecretLike()
    {
        // Low entropy on purpose: only the ghp_ prefix should trigger the finding.
        var issues = Scan(Healthy(environmentLogLevel: "ghp_abababababababababababababab"));

        var issue = Assert.Single(issues, i => i.Key == "Logging:LogLevel:Default");
        Assert.Equal(SecretIssueKind.UndeclaredSecretLike, issue.Kind);
        Assert.Equal(EnvironmentSource, issue.Source);
    }

    [Fact]
    public void Scan_DetectUndeclaredDisabled_NoUndeclaredIssues()
    {
        var snapshots = Healthy(environmentLogLevel: HighEntropyLogLevel);

        // Control: the same input is flagged with default options.
        var defaults = new SecretDetector().Scan(Schema, snapshots);
        Assert.Contains(defaults, i => i.Kind == SecretIssueKind.UndeclaredSecretLike);

        var options = new SecretDetectorOptions { DetectUndeclaredSecretLike = false };
        var issues = new SecretDetector(options).Scan(Schema, snapshots);

        Assert.Empty(issues);
    }

    [Fact]
    public void Scan_Messages_NeverContainSecretValue()
    {
        var issues = Scan(
            Snapshot(AppSettings,
                ("Api:Key", RealApiKey),
                ("ConnectionStrings:Default", RealConnection),
                ("Logging:LogLevel:Default", "Information")),
            Snapshot(Production, ("Smtp:Password", "CHANGEME")),
            Snapshot(EnvironmentSource,
                ("Logging:LogLevel:Default", HighEntropyLogLevel),
                ("Github:Token", GhToken)));

        // Make sure the scan actually produced each kind, so the assertions below are not vacuous.
        Assert.Contains(issues, i => i.Kind == SecretIssueKind.SecretInCommittedFile);
        Assert.Contains(issues, i => i.Kind == SecretIssueKind.PlaceholderValue);
        Assert.Contains(issues, i => i.Kind == SecretIssueKind.UndeclaredSecretLike);

        string[] fixtures = [RealApiKey, RealConnection, HighEntropyLogLevel, GhToken, "CHANGEME"];
        Assert.All(issues, issue =>
        {
            foreach (var fixture in fixtures)
            {
                Assert.DoesNotContain(fixture, issue.Message);
            }
        });
    }

    [Fact]
    public void Scan_Results_OrderedByKindThenKey()
    {
        var issues = Scan(
            Snapshot(AppSettings,
                ("Api:Key", RealApiKey),
                ("ConnectionStrings:Default", RealConnection),
                ("Logging:LogLevel:Default", "Information")),
            Snapshot(Production),
            Snapshot(EnvironmentSource,
                ("Logging:LogLevel:Default", HighEntropyLogLevel),
                ("Github:Token", GhToken)));

        // Kind order follows the enum (MissingSecret, SecretInCommittedFile, UndeclaredSecretLike, PlaceholderValue),
        // and keys within a kind are sorted case-insensitively.
        var expected = new[]
        {
            (SecretIssueKind.MissingSecret, "Smtp:Password"),
            (SecretIssueKind.SecretInCommittedFile, "Api:Key"),
            (SecretIssueKind.SecretInCommittedFile, "ConnectionStrings:Default"),
            (SecretIssueKind.UndeclaredSecretLike, "Github:Token"),
            (SecretIssueKind.UndeclaredSecretLike, "Logging:LogLevel:Default"),
        };

        Assert.Equal(expected, issues.Select(i => (i.Kind, i.Key)));
    }

    [Fact]
    public void Scan_NullSnapshots_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new SecretDetector().Scan(Schema, null!));

        Assert.Equal("snapshots", ex.ParamName);
    }

    private static IReadOnlyList<SecretIssue> Scan(params ConfigSnapshot[] snapshots) =>
        new SecretDetector().Scan(Schema, snapshots);

    private static ConfigSnapshot Snapshot(string name, params (string Key, string? Value)[] values)
    {
        var dictionary = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            dictionary[key] = value;
        }

        return new ConfigSnapshot(name, dictionary);
    }

    /// <summary>
    /// A configuration where every declared secret is supplied by the environment and nothing is flagged.
    /// </summary>
    private static ConfigSnapshot[] Healthy(string? environmentLogLevel = null)
    {
        var environment = new List<(string Key, string? Value)>
        {
            ("ConnectionStrings:Default", RealConnection),
            ("Api:Key", RealApiKey),
            ("Smtp:Password", RealSmtpPassword),
        };

        if (environmentLogLevel is not null)
        {
            environment.Add(("Logging:LogLevel:Default", environmentLogLevel));
        }

        return
        [
            Snapshot(AppSettings, ("Logging:LogLevel:Default", "Information")),
            Snapshot(Production),
            Snapshot(EnvironmentSource, environment.ToArray()),
        ];
    }
}

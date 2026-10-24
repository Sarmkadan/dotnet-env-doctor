namespace DotnetEnvDoctor.Tests;

public sealed class EnvDoctorRunnerTests : IDisposable
{
    private const string Schema = """
        { "keys": [
            { "key": "ConnectionStrings:Default", "kind": "String", "required": true },
            { "key": "Api:Key", "kind": "Secret", "required": true }
        ] }
        """;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "envdoctor-runner-" + Guid.NewGuid().ToString("N"));

    public EnvDoctorRunnerTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Constructor_NullDependency_Throws()
    {
        var loader = new JsonSchemaLoader();
        var validator = new ConfigValidator();
        var differ = new EnvironmentDiffer();
        var detector = new SecretDetector();

        Assert.Throws<ArgumentNullException>(() => new EnvDoctorRunner(null!, validator, differ, detector));
        Assert.Throws<ArgumentNullException>(() => new EnvDoctorRunner(loader, null!, differ, detector));
        Assert.Throws<ArgumentNullException>(() => new EnvDoctorRunner(loader, validator, null!, detector));
        Assert.Throws<ArgumentNullException>(() => new EnvDoctorRunner(loader, validator, differ, null!));
    }

    [Fact]
    public async Task ValidateAsync_LaterSourceOverridesEarlier_IsValid()
    {
        var schemaPath = WriteFile("schema.json", Schema);
        var baseSource = new JsonFileConfigSource(WriteFile("appsettings.json", """{ "ConnectionStrings": { "Default": "" }, "Api": { "Key": "x" } }"""));
        var overrideSource = new JsonFileConfigSource(WriteFile("appsettings.Production.json", """{ "ConnectionStrings": { "Default": "Server=db" } }"""));

        var result = await new EnvDoctorRunner().ValidateAsync(schemaPath, [baseSource, overrideSource]);

        Assert.True(result.IsValid);
        Assert.Equal(0, EnvDoctorRunner.ToExitCode(result));
    }

    [Fact]
    public async Task ValidateAsync_MissingRequiredKey_ReturnsInvalidWithExitCodeOne()
    {
        var schemaPath = WriteFile("schema.json", Schema);
        var source = new JsonFileConfigSource(WriteFile("appsettings.json", """{ "ConnectionStrings": { "Default": "Server=db" } }"""));

        var result = await new EnvDoctorRunner().ValidateAsync(schemaPath, [source]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Findings, f => f.Key == "Api:Key" && f.Code == FindingCodes.MissingRequired);
        Assert.Equal(1, EnvDoctorRunner.ToExitCode(result));
    }

    [Fact]
    public async Task DiffAsync_ChangedValue_ReportsChangedEntry()
    {
        var left = new JsonFileConfigSource(WriteFile("staging.json", """{ "Logging": { "Level": "Debug" } }"""));
        var right = new JsonFileConfigSource(WriteFile("production.json", """{ "Logging": { "Level": "Warning" } }"""));

        var result = await new EnvDoctorRunner().DiffAsync(left, right);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Logging:Level", entry.Key);
        Assert.Equal(DiffKind.Changed, entry.Kind);
        Assert.False(result.IsIdentical);
    }

    [Fact]
    public async Task DiffAsync_SecretKeyWithSchema_RedactsValues()
    {
        var schemaPath = WriteFile("schema.json", Schema);
        var left = new JsonFileConfigSource(WriteFile("left.json", """{ "Api": { "Key": "left-secret" } }"""));
        var right = new JsonFileConfigSource(WriteFile("right.json", """{ "Api": { "Key": "right-secret" } }"""));

        var result = await new EnvDoctorRunner().DiffAsync(left, right, schemaPath);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(EnvironmentDiffer.RedactedValue, entry.LeftValue);
        Assert.Equal(EnvironmentDiffer.RedactedValue, entry.RightValue);
    }

    [Fact]
    public async Task ScanSecretsAsync_SecretNotSetInAnySource_ReportsMissingSecret()
    {
        var schemaPath = WriteFile("schema.json", Schema);
        var source = new JsonFileConfigSource(WriteFile("appsettings.json", """{ "ConnectionStrings": { "Default": "Server=db" } }"""));

        var issues = await new EnvDoctorRunner().ScanSecretsAsync(schemaPath, [source]);

        var issue = Assert.Single(issues);
        Assert.Equal("Api:Key", issue.Key);
        Assert.Equal(SecretIssueKind.MissingSecret, issue.Kind);
        Assert.Equal(1, EnvDoctorRunner.ToExitCode(issues));
    }

    [Fact]
    public async Task ValidateAsync_CancellationRequested_Throws()
    {
        var schemaPath = WriteFile("schema.json", Schema);
        var source = new JsonFileConfigSource(WriteFile("appsettings.json", "{}"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new EnvDoctorRunner().ValidateAsync(schemaPath, [source], null, cts.Token));
    }

    [Fact]
    public void ToExitCode_ValidationResultWithoutErrors_ReturnsZero()
    {
        var result = new ValidationResult([new Finding("Logging:Level", Severity.Warning, FindingCodes.UndeclaredKey, "Undeclared")]);

        Assert.Equal(0, EnvDoctorRunner.ToExitCode(result));
    }

    [Fact]
    public void ToExitCode_ValidationResultWithError_ReturnsOne()
    {
        var result = new ValidationResult([new Finding("Api:Key", Severity.Error, FindingCodes.MissingRequired, "Missing")]);

        Assert.Equal(1, EnvDoctorRunner.ToExitCode(result));
    }

    [Fact]
    public void ToExitCode_NoSecretIssues_ReturnsZero()
    {
        Assert.Equal(0, EnvDoctorRunner.ToExitCode(Array.Empty<SecretIssue>()));
    }

    [Fact]
    public void ToExitCode_OnlyUndeclaredSecretLike_ReturnsTwo()
    {
        var issues = new[]
        {
            new SecretIssue("Token", SecretIssueKind.UndeclaredSecretLike, "appsettings.json", "Looks like a credential"),
        };

        Assert.Equal(2, EnvDoctorRunner.ToExitCode(issues));
    }

    [Fact]
    public void ToExitCode_MixedIssues_ReturnsOne()
    {
        var issues = new[]
        {
            new SecretIssue("Token", SecretIssueKind.UndeclaredSecretLike, "appsettings.json", "Looks like a credential"),
            new SecretIssue("Api:Key", SecretIssueKind.MissingSecret, null, "Not set"),
        };

        Assert.Equal(1, EnvDoctorRunner.ToExitCode(issues));
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }
}

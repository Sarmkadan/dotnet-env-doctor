namespace DotnetEnvDoctor;

/// <summary>
/// Options controlling behavior of <see cref="SecretDetector"/>.
/// </summary>
public sealed class SecretDetectorOptions
{
    /// <summary>
    /// Gets or sets well-known placeholder values that should be flagged
    /// (e.g. <c>CHANGEME</c>, <c>your-api-key</c>). Case-insensitive matching.
    /// </summary>
    public IReadOnlyList<string> PlaceholderValues { get; set; } = new[]
    {
        "CHANGEME",
        "changeme",
        "CHANGE_ME",
        "change_me",
        "TODO",
        "todo",
        "your-api-key",
        "your-secret",
        "your-password",
        "replace-me",
        "INSERT_HERE",
        "xxx",
        "XXX",
        "placeholder",
        "PLACEHOLDER",
        "secret",
        "password",
        "example",
        "test",
    };

    /// <summary>
    /// Gets or sets glob patterns for snapshot names that correspond to files
    /// committed to source control (e.g. <c>appsettings.json</c>, <c>appsettings.*.json</c>).
    /// A secret value found in a matching snapshot triggers a <see cref="SecretIssueKind.SecretInCommittedFile"/> issue.
    /// </summary>
    public IReadOnlyList<string> CommittedFilePatterns { get; set; } = new[]
    {
        "appsettings.json",
        "appsettings.*.json",
    };

    /// <summary>
    /// Gets or sets a value indicating whether the detector should flag values
    /// that look like credentials even when the key is not declared as a secret in the schema.
    /// Default: <c>true</c>.
    /// </summary>
    public bool DetectUndeclaredSecretLike { get; set; } = true;
}

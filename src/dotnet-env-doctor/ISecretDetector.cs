namespace DotnetEnvDoctor;

/// <summary>
/// Why a value was flagged by the secret detector.
/// </summary>
public enum SecretIssueKind
{
    /// <summary>A key declared as <see cref="ConfigValueKind.Secret"/> or
    /// <see cref="ConfigValueKind.ConnectionString"/> has no value in any source.</summary>
    MissingSecret,

    /// <summary>A secret value is present in a file that is likely committed to source control.</summary>
    SecretInCommittedFile,

    /// <summary>A value looks like a credential (high entropy, known token prefix) but the key is not declared as a secret.</summary>
    UndeclaredSecretLike,

    /// <summary>A secret still holds a well-known placeholder such as <c>CHANGEME</c> or <c>your-api-key</c>.</summary>
    PlaceholderValue,
}

/// <summary>
/// A secret-related issue.
/// </summary>
/// <param name="Key">Configuration key.</param>
/// <param name="Kind">Kind of issue.</param>
/// <param name="Source">Source in which the issue was observed, if any.</param>
/// <param name="Message">Human-readable explanation. Never contains the secret value itself.</param>
public sealed record SecretIssue(
    string Key,
    SecretIssueKind Kind,
    string? Source,
    string Message);

/// <summary>
/// Detects missing, leaked and placeholder secrets across configuration sources.
/// </summary>
public interface ISecretDetector
{
    /// <summary>
    /// Scans the given snapshots for secret-related issues.
    /// </summary>
    /// <param name="schema">Schema declaring which keys are secrets.</param>
    /// <param name="snapshots">All snapshots, in override order (first = lowest priority).</param>
    /// <returns>Issues found. Values are never included in the output.</returns>
    IReadOnlyList<SecretIssue> Scan(IConfigSchema schema, IReadOnlyList<ConfigSnapshot> snapshots);
}

/// <summary>
/// Default heuristic secret detector.
/// </summary>
public sealed class SecretDetector : ISecretDetector
{
    /// <inheritdoc />
    public IReadOnlyList<SecretIssue> Scan(IConfigSchema schema, IReadOnlyList<ConfigSnapshot> snapshots)
    {
        throw new NotImplementedException();
    }
}

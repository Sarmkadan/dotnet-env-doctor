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
    private readonly SecretDetectorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretDetector"/> class with default options.
    /// </summary>
    public SecretDetector()
        : this(new SecretDetectorOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretDetector"/> class.
    /// </summary>
    /// <param name="options">Options controlling detection behavior.</param>
    public SecretDetector(SecretDetectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
    }

    /// <inheritdoc />
    public IReadOnlyList<SecretIssue> Scan(IConfigSchema schema, IReadOnlyList<ConfigSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(snapshots);

        // (1) Merge snapshots in override order: first = lowest priority, later wins.
        var effective = new Dictionary<string, (string? Value, string SourceName)>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots)
        {
            foreach (var kvp in snapshot.Values)
            {
                effective[kvp.Key] = (kvp.Value, snapshot.Name);
            }
        }

        var issues = new List<SecretIssue>();
        var seen = new HashSet<(string Key, SecretIssueKind Kind, string? Source)>();

        // Build a set of secret-kind keys for quick lookup.
        var secretKindKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in schema.Keys)
        {
            if (spec.Kind is ConfigValueKind.Secret or ConfigValueKind.ConnectionString)
            {
                secretKindKeys.Add(spec.Key);
            }
        }

        // (2) For every ConfigKeySpec with Kind Secret or ConnectionString:
        foreach (var spec in schema.Keys)
        {
            if (spec.Kind is not (ConfigValueKind.Secret or ConfigValueKind.ConnectionString))
            {
                continue;
            }

            if (!effective.TryGetValue(spec.Key, out var entry) || string.IsNullOrWhiteSpace(entry.Value))
            {
                // Missing secret.
                AddIssue(issues, seen, spec.Key, SecretIssueKind.MissingSecret, null, "Secret is not set in any source");
            }
            else if (SecretHeuristics.IsPlaceholder(entry.Value, _options.PlaceholderValues))
            {
                // Placeholder value.
                AddIssue(issues, seen, spec.Key, SecretIssueKind.PlaceholderValue, entry.SourceName,
                    $"Secret holds a placeholder value in source '{entry.SourceName}'");
            }
        }

        // (3) For every snapshot whose Name matches any CommittedFilePatterns glob:
        foreach (var snapshot in snapshots)
        {
            if (!SecretHeuristics.MatchesAnyPattern(snapshot.Name, _options.CommittedFilePatterns))
            {
                continue;
            }

            foreach (var kvp in snapshot.Values)
            {
                if (!secretKindKeys.Contains(kvp.Key))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(kvp.Value))
                {
                    continue;
                }

                if (SecretHeuristics.IsPlaceholder(kvp.Value, _options.PlaceholderValues))
                {
                    continue;
                }

                // Non-empty, non-placeholder secret in a committed file.
                AddIssue(issues, seen, kvp.Key, SecretIssueKind.SecretInCommittedFile, snapshot.Name,
                    $"Secret has a real value in committed file '{snapshot.Name}'");
            }
        }

        // (4) Undeclared secret-like values.
        if (_options.DetectUndeclaredSecretLike)
        {
            foreach (var kvp in effective)
            {
                if (secretKindKeys.Contains(kvp.Key))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(kvp.Value.Value))
                {
                    continue;
                }

                if (SecretHeuristics.LooksLikeSecret(kvp.Value.Value, _options))
                {
                    AddIssue(issues, seen, kvp.Key, SecretIssueKind.UndeclaredSecretLike, kvp.Value.SourceName,
                        "Value looks like a credential but the key is not declared as a secret");
                }
            }
        }

        // Order by Kind then Key (OrdinalIgnoreCase).
        issues.Sort((a, b) =>
        {
            int kindCmp = a.Kind.CompareTo(b.Kind);
            return kindCmp != 0
                ? kindCmp
                : string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
        });

        return issues;
    }

    private static void AddIssue(
        List<SecretIssue> issues,
        HashSet<(string Key, SecretIssueKind Kind, string? Source)> seen,
        string key,
        SecretIssueKind kind,
        string? source,
        string message)
    {
        if (seen.Add((key, kind, source)))
        {
            issues.Add(new SecretIssue(key, kind, source, message));
        }
    }
}

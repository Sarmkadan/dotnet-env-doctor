namespace DotnetEnvDoctor;

/// <summary>
/// Severity of a validation finding.
/// </summary>
public enum Severity
{
    /// <summary>Informational, does not affect the exit code.</summary>
    Info,

    /// <summary>Suspicious but not fatal.</summary>
    Warning,

    /// <summary>Configuration is invalid; the process should fail.</summary>
    Error,
}

/// <summary>
/// A single validation finding.
/// </summary>
/// <param name="Key">The configuration key the finding refers to.</param>
/// <param name="Severity">Severity of the finding.</param>
/// <param name="Code">Stable machine-readable code, e.g. <c>ED001</c> (missing required key).</param>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="Source">Name of the source the value was read from, if any.</param>
public sealed record Finding(
    string Key,
    Severity Severity,
    string Code,
    string Message,
    string? Source = null);

/// <summary>
/// Result of validating a snapshot against a schema.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> record.
    /// </summary>
    /// <param name="Findings">All findings, ordered by severity descending then by key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="Findings"/> is <c>null</c>.</exception>
    public ValidationResult(IReadOnlyList<Finding> Findings)
    {
        ArgumentNullException.ThrowIfNull(Findings);
        this.Findings = Findings;
    }

    /// <summary>
    /// Gets all findings, ordered by severity descending then by key.
    /// </summary>
    public IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>
    /// Deconstructs the result into its findings, preserving the positional record shape.
    /// </summary>
    /// <param name="Findings">All findings.</param>
    public void Deconstruct(out IReadOnlyList<Finding> Findings) => Findings = this.Findings;

    /// <summary>
    /// Gets a value indicating whether the result contains no <see cref="Severity.Error"/> findings.
    /// </summary>
    public bool IsValid => Findings.All(f => f.Severity != Severity.Error);
}

/// <summary>
/// Validates a merged configuration snapshot against a schema.
/// </summary>
public interface IConfigValidator
{
    /// <summary>
    /// Validates the given snapshot.
    /// </summary>
    /// <param name="schema">Schema declaring expected keys.</param>
    /// <param name="snapshot">Merged configuration values (later sources override earlier ones).</param>
    /// <param name="options">Validation options.</param>
    /// <returns>Findings for missing, mistyped, malformed and undeclared keys.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> or <paramref name="snapshot"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><c>schema.Keys</c> or <c>snapshot.Values</c> is <c>null</c>.</exception>
    ValidationResult Validate(IConfigSchema schema, ConfigSnapshot snapshot, ValidationOptions? options = null);
}

/// <summary>
/// Options controlling validation behavior.
/// </summary>
public sealed class ValidationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether keys present in the snapshot but absent from the schema
    /// should be reported as <see cref="Severity.Warning"/>. Default: <c>true</c>.
    /// </summary>
    public bool ReportUndeclaredKeys { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Severity.Warning"/> findings should be
    /// promoted to <see cref="Severity.Error"/>. Default: <c>false</c>.
    /// </summary>
    public bool TreatWarningsAsErrors { get; set; }
}

/// <summary>
/// Default schema-driven validator.
/// </summary>
public sealed class ConfigValidator : IConfigValidator
{
    private readonly RegexPatternRule _patternRule = new();

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> or <paramref name="snapshot"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><c>schema.Keys</c> or <c>snapshot.Values</c> is <c>null</c>.</exception>
    public ValidationResult Validate(IConfigSchema schema, ConfigSnapshot snapshot, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (schema.Keys is null)
        {
            throw new ArgumentException("Schema.Keys must not be null.", nameof(schema));
        }

        if (snapshot.Values is null)
        {
            throw new ArgumentException("Snapshot.Values must not be null.", nameof(snapshot));
        }

        options ??= new ValidationOptions();

        // Case-insensitive view of the snapshot, matching Microsoft.Extensions.Configuration key semantics.
        // On a case-only collision the first key encountered wins.
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in snapshot.Values)
        {
            values.TryAdd(kvp.Key, kvp.Value);
        }

        var findings = new List<Finding>();

        foreach (var spec in schema.Keys)
        {
            values.TryGetValue(spec.Key, out string? value);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (spec.Required)
                {
                    findings.Add(new Finding(
                        spec.Key,
                        Severity.Error,
                        FindingCodes.MissingRequired,
                        "Required key is missing or empty",
                        snapshot.Name));
                }

                continue;
            }

            // Never include the value itself in the message: it may be a secret.
            if (!ValueKindChecker.IsValid(spec.Kind, value, out string? reason))
            {
                findings.Add(new Finding(
                    spec.Key,
                    Severity.Error,
                    FindingCodes.KindMismatch,
                    $"Value does not match kind {spec.Kind}: {reason}",
                    snapshot.Name));
            }
            else if (_patternRule.Evaluate(spec, value, snapshot.Name) is { } patternFinding)
            {
                findings.Add(patternFinding);
            }
        }

        if (options.ReportUndeclaredKeys)
        {
            foreach (var key in values.Keys)
            {
                if (!schema.TryGetSpec(key, out _))
                {
                    findings.Add(new Finding(
                        key,
                        Severity.Warning,
                        FindingCodes.UndeclaredKey,
                        "Key is not declared in the schema",
                        snapshot.Name));
                }
            }
        }

        IEnumerable<Finding> result = findings;
        if (options.TreatWarningsAsErrors)
        {
            result = result.Select(f => f.Severity == Severity.Warning ? f with { Severity = Severity.Error } : f);
        }

        var ordered = result
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationResult(ordered);
    }
}

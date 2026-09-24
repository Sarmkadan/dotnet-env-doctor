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
/// <param name="Findings">All findings, ordered by severity descending then by key.</param>
public sealed record ValidationResult(IReadOnlyList<Finding> Findings)
{
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
    /// <inheritdoc />
    public ValidationResult Validate(IConfigSchema schema, ConfigSnapshot snapshot, ValidationOptions? options = null)
    {
        throw new NotImplementedException();
    }
}

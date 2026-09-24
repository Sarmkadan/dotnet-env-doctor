namespace DotnetEnvDoctor;

/// <summary>
/// Kind of value a configuration key is expected to hold.
/// </summary>
public enum ConfigValueKind
{
    /// <summary>Any string value.</summary>
    String,

    /// <summary>Integer value (parsed with invariant culture).</summary>
    Integer,

    /// <summary>Boolean value (<c>true</c>/<c>false</c>).</summary>
    Boolean,

    /// <summary>Absolute URI.</summary>
    Uri,

    /// <summary>Connection string (opaque, treated as a secret).</summary>
    ConnectionString,

    /// <summary>Secret value: must never be committed, only injected via environment or a vault.</summary>
    Secret,
}

/// <summary>
/// Declares expectations for a single configuration key.
/// </summary>
/// <param name="Key">Colon-separated configuration path, e.g. <c>ConnectionStrings:Default</c>.</param>
/// <param name="Kind">Expected value kind.</param>
/// <param name="Required">Whether the key must be present with a non-empty value.</param>
/// <param name="Pattern">Optional regular expression the value must match.</param>
/// <param name="Description">Human-readable purpose of the key.</param>
public sealed record ConfigKeySpec(
    string Key,
    ConfigValueKind Kind = ConfigValueKind.String,
    bool Required = true,
    string? Pattern = null,
    string? Description = null);

/// <summary>
/// A schema describing the configuration keys an application expects.
/// </summary>
public interface IConfigSchema
{
    /// <summary>
    /// Gets all key specifications declared by the schema.
    /// </summary>
    IReadOnlyList<ConfigKeySpec> Keys { get; }

    /// <summary>
    /// Attempts to find the specification for a configuration key.
    /// </summary>
    /// <param name="key">Colon-separated configuration path (case-insensitive).</param>
    /// <param name="spec">The matching specification when found.</param>
    /// <returns><c>true</c> when the key is declared in the schema.</returns>
    bool TryGetSpec(string key, out ConfigKeySpec? spec);
}

/// <summary>
/// Loads an <see cref="IConfigSchema"/> from a schema file (JSON or YAML).
/// </summary>
public interface IConfigSchemaLoader
{
    /// <summary>
    /// Loads a schema from disk.
    /// </summary>
    /// <param name="path">Path to <c>envdoctor.schema.json</c> or a compatible file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed schema.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="FormatException">The file is not a valid schema.</exception>
    Task<IConfigSchema> LoadAsync(string path, CancellationToken cancellationToken = default);
}

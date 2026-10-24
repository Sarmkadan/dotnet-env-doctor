namespace DotnetEnvDoctor;

/// <summary>
/// Central place for deciding which configuration values are secrets and masking them.
/// Every component that writes values to output (diffs, findings, logs) should go through this class.
/// </summary>
public static class SecretRedactor
{
    /// <summary>
    /// Placeholder written in place of secret values.
    /// </summary>
    public const string Mask = "***";

    /// <summary>
    /// Returns <c>true</c> when values of the given kind must be redacted.
    /// </summary>
    /// <param name="kind">Declared value kind.</param>
    /// <returns><c>true</c> for <see cref="ConfigValueKind.Secret"/> and <see cref="ConfigValueKind.ConnectionString"/>.</returns>
    public static bool IsSecretKind(ConfigValueKind kind) =>
        kind is ConfigValueKind.Secret or ConfigValueKind.ConnectionString;

    /// <summary>
    /// Returns <c>true</c> when the schema declares <paramref name="key"/> as a secret kind.
    /// Undeclared keys and a <c>null</c> schema are never treated as secrets.
    /// </summary>
    /// <param name="schema">Schema to consult, or <c>null</c>.</param>
    /// <param name="key">Configuration key (case-insensitive).</param>
    /// <returns><c>true</c> when the key is declared as a secret kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public static bool IsSecretKey(IConfigSchema? schema, string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (schema is null || !schema.TryGetSpec(key, out var spec) || spec is null)
        {
            return false;
        }

        return IsSecretKind(spec.Kind);
    }

    /// <summary>
    /// Masks a value unconditionally. <c>null</c> and empty values are returned unchanged
    /// because they reveal nothing; any other value becomes <see cref="Mask"/>.
    /// </summary>
    /// <param name="value">Value to redact.</param>
    /// <returns>The redacted value.</returns>
    public static string? Redact(string? value) =>
        string.IsNullOrEmpty(value) ? value : Mask;

    /// <summary>
    /// Masks <paramref name="value"/> when <paramref name="key"/> is a secret according to <paramref name="schema"/>;
    /// otherwise returns it unchanged.
    /// </summary>
    /// <param name="schema">Schema to consult, or <c>null</c> (nothing is redacted).</param>
    /// <param name="key">Configuration key (case-insensitive).</param>
    /// <param name="value">Value to possibly redact.</param>
    /// <returns>The value, masked when the key is secret.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public static string? RedactIfSecret(IConfigSchema? schema, string key, string? value) =>
        IsSecretKey(schema, key) ? Redact(value) : value;

    /// <summary>
    /// Creates a copy of <paramref name="snapshot"/> in which every value whose key is a secret
    /// according to <paramref name="schema"/> is masked. The original snapshot is not modified.
    /// </summary>
    /// <param name="schema">Schema describing the secret keys.</param>
    /// <param name="snapshot">Snapshot to redact.</param>
    /// <returns>A new snapshot with the same name and case-insensitive key lookup.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> or <paramref name="snapshot"/> is <c>null</c>.</exception>
    public static ConfigSnapshot RedactSnapshot(IConfigSchema schema, ConfigSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(snapshot);

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in snapshot.Values)
        {
            values[pair.Key] = RedactIfSecret(schema, pair.Key, pair.Value);
        }

        return new ConfigSnapshot(snapshot.Name, values);
    }
}

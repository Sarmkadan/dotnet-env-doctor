namespace DotnetEnvDoctor;

/// <summary>
/// In-memory <see cref="IConfigSchema"/> built from a fixed set of <see cref="ConfigKeySpec"/> entries.
/// Key lookups are case-insensitive, matching <c>Microsoft.Extensions.Configuration</c>.
/// </summary>
public sealed class ConfigSchema : IConfigSchema
{
    private readonly ConfigKeySpec[] _keys;
    private readonly Dictionary<string, ConfigKeySpec> _specsByKey;

    /// <summary>
    /// Initializes a new schema from the given key specifications.
    /// </summary>
    /// <param name="keys">Key specifications, in declaration order. Each key must be unique (case-insensitive).</param>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> or one of its elements is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Two specifications share the same key, ignoring case.</exception>
    public ConfigSchema(IEnumerable<ConfigKeySpec> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        _keys = keys.ToArray();
        _specsByKey = new Dictionary<string, ConfigKeySpec>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in _keys)
        {
            ArgumentNullException.ThrowIfNull(spec);

            if (!_specsByKey.TryAdd(spec.Key, spec))
            {
                throw new ArgumentException(
                    $"Duplicate configuration key '{spec.Key}' (keys are case-insensitive).",
                    nameof(keys));
            }
        }
    }

    /// <summary>
    /// Gets a schema that declares no keys.
    /// </summary>
    public static ConfigSchema Empty { get; } = new(Array.Empty<ConfigKeySpec>());

    /// <inheritdoc />
    public IReadOnlyList<ConfigKeySpec> Keys => _keys;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool TryGetSpec(string key, out ConfigKeySpec? spec)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_specsByKey.TryGetValue(key, out var found))
        {
            spec = found;
            return true;
        }

        spec = null;
        return false;
    }
}

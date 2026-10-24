using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace DotnetEnvDoctor;

/// <summary>
/// A flattened snapshot of configuration values from one source
/// (an <c>appsettings*.json</c> file, the process environment, a <c>.env</c> file, ...).
/// </summary>
public sealed record ConfigSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigSnapshot"/> record.
    /// </summary>
    /// <param name="Name">Display name of the source, e.g. <c>appsettings.Production.json</c>.</param>
    /// <param name="Values">Flattened key/value pairs. Keys use the colon-separated configuration path form.</param>
    /// <exception cref="ArgumentException"><paramref name="Name"/> is <c>null</c>, empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="Values"/> is <c>null</c>.</exception>
    public ConfigSnapshot(string Name, IReadOnlyDictionary<string, string?> Values)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Snapshot name must not be null, empty or white-space.", nameof(Name));
        }

        ArgumentNullException.ThrowIfNull(Values);

        this.Name = Name;
        this.Values = Values;
    }

    /// <summary>
    /// Gets the display name of the source, e.g. <c>appsettings.Production.json</c>.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the flattened key/value pairs. Keys use the colon-separated configuration path form.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Values { get; init; }

    /// <summary>
    /// Deconstructs the snapshot into its name and values, preserving the positional record shape.
    /// </summary>
    /// <param name="Name">Display name of the source.</param>
    /// <param name="Values">Flattened key/value pairs.</param>
    public void Deconstruct(out string Name, out IReadOnlyDictionary<string, string?> Values)
    {
        Name = this.Name;
        Values = this.Values;
    }
}

/// <summary>
/// Reads configuration from a concrete location into a flat <see cref="ConfigSnapshot"/>.
/// </summary>
public interface IConfigSource
{
    /// <summary>
    /// Gets the display name of this source.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Reads all configuration values from the source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The flattened configuration snapshot.</returns>
    Task<ConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads an <c>appsettings.json</c>-style file, flattening nested sections into colon-separated keys.
/// </summary>
public sealed class JsonFileConfigSource : IConfigSource
{
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileConfigSource"/> class.
    /// </summary>
    /// <param name="path">Path to the JSON file.</param>
    public JsonFileConfigSource(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <inheritdoc />
    public string Name => Path.GetFileName(_path);

    /// <inheritdoc />
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="FormatException">The file is not valid JSON or its root value is not an object.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async Task<ConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            throw new FileNotFoundException($"Configuration file not found: '{_path}'.", _path);
        }

        var options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        JsonDocument document;
        await using (var stream = File.OpenRead(_path))
        {
            try
            {
                document = await JsonDocument.ParseAsync(stream, options, cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new FormatException($"Configuration file '{_path}' is not valid JSON: {ex.Message}", ex);
            }
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"Configuration file '{_path}' is invalid: the root value must be a JSON object.");
            }

            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            Flatten(root, string.Empty, values);
            return new ConfigSnapshot(Name, values);
        }
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string?> target)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Flatten(property.Value, prefix.Length == 0 ? property.Name : $"{prefix}:{property.Name}", target);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = prefix.Length == 0 ? index.ToString(CultureInfo.InvariantCulture) : $"{prefix}:{index.ToString(CultureInfo.InvariantCulture)}";
                    Flatten(item, key, target);
                    index++;
                }

                break;

            case JsonValueKind.Null:
                target[prefix] = null;
                break;

            case JsonValueKind.String:
                target[prefix] = element.GetString();
                break;

            default:
                // Numbers and booleans keep their literal JSON form (e.g. "42", "true").
                target[prefix] = element.GetRawText();
                break;
        }
    }
}

/// <summary>
/// Reads process environment variables, translating <c>__</c> into the <c>:</c> section separator
/// (the same convention used by <c>Microsoft.Extensions.Configuration.EnvironmentVariables</c>).
/// </summary>
public sealed class EnvironmentConfigSource : IConfigSource
{
    private readonly string? _prefix;
    private readonly Func<IDictionary> _variablesProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentConfigSource"/> class.
    /// </summary>
    /// <param name="prefix">Optional prefix to filter and strip from variable names (e.g. <c>MYAPP_</c>).</param>
    public EnvironmentConfigSource(string? prefix = null)
        : this(prefix, Environment.GetEnvironmentVariables)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentConfigSource"/> class with a custom variable provider.
    /// </summary>
    /// <param name="prefix">Optional prefix to filter and strip from variable names.</param>
    /// <param name="variablesProvider">Supplies the environment variables to read (used by tests).</param>
    internal EnvironmentConfigSource(string? prefix, Func<IDictionary> variablesProvider)
    {
        _prefix = prefix;
        _variablesProvider = variablesProvider ?? throw new ArgumentNullException(nameof(variablesProvider));
    }

    /// <inheritdoc />
    public string Name => _prefix is null ? "environment" : $"environment ({_prefix}*)";

    /// <inheritdoc />
    public Task<ConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in _variablesProvider())
        {
            if (entry.Key is not string name)
            {
                continue;
            }

            if (_prefix is not null)
            {
                if (!name.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                name = name[_prefix.Length..];
            }

            // Same mapping as Microsoft.Extensions.Configuration.EnvironmentVariables: "A__B" -> "A:B".
            name = name.Replace("__", ":", StringComparison.Ordinal);
            if (name.Length == 0)
            {
                continue;
            }

            // On a case-insensitive collision the first variable encountered wins.
            values.TryAdd(name, entry.Value?.ToString());
        }

        return Task.FromResult(new ConfigSnapshot(Name, values));
    }
}

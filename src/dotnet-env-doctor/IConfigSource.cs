namespace DotnetEnvDoctor;

/// <summary>
/// A flattened snapshot of configuration values from one source
/// (an <c>appsettings*.json</c> file, the process environment, a <c>.env</c> file, ...).
/// </summary>
/// <param name="Name">Display name of the source, e.g. <c>appsettings.Production.json</c>.</param>
/// <param name="Values">Flattened key/value pairs. Keys use the colon-separated configuration path form.</param>
public sealed record ConfigSnapshot(
    string Name,
    IReadOnlyDictionary<string, string?> Values);

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
    public Task<ConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Reads process environment variables, translating <c>__</c> into the <c>:</c> section separator
/// (the same convention used by <c>Microsoft.Extensions.Configuration.EnvironmentVariables</c>).
/// </summary>
public sealed class EnvironmentConfigSource : IConfigSource
{
    private readonly string? _prefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentConfigSource"/> class.
    /// </summary>
    /// <param name="prefix">Optional prefix to filter and strip from variable names (e.g. <c>MYAPP_</c>).</param>
    public EnvironmentConfigSource(string? prefix = null)
    {
        _prefix = prefix;
    }

    /// <inheritdoc />
    public string Name => _prefix is null ? "environment" : $"environment ({_prefix}*)";

    /// <inheritdoc />
    public Task<ConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;

namespace DotnetEnvDoctor;

/// <summary>
/// Loads an <see cref="IConfigSchema"/> from a JSON schema file, such as <c>envdoctor.schema.json</c>.
/// </summary>
/// <remarks>
/// Expected format:
/// <code>
/// { "keys": [ { "key": "ConnectionStrings:Default", "kind": "ConnectionString", "required": true, "pattern": "^Server=", "description": "..." } ] }
/// </code>
/// <c>kind</c> is a case-insensitive <see cref="ConfigValueKind"/> name and defaults to <c>String</c>.
/// <c>required</c> defaults to <c>true</c>. <c>pattern</c> and <c>description</c> are optional.
/// Unknown properties are ignored.
/// </remarks>
public sealed class JsonSchemaLoader : IConfigSchemaLoader
{
    /// <summary>
    /// Parses a schema from a JSON string.
    /// </summary>
    /// <param name="json">The schema document.</param>
    /// <returns>The parsed schema.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">The document is not a valid schema.</exception>
    public static IConfigSchema Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Schema is not valid JSON: {ex.Message}", ex);
        }

        using (document)
        {
            return Build(document.RootElement, "schema");
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <c>null</c>.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="FormatException">The file is not a valid schema.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async Task<IConfigSchema> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Schema file not found: '{path}'.", path);
        }

        JsonDocument document;
        await using (var stream = File.OpenRead(path))
        {
            try
            {
                document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new FormatException($"Schema file '{path}' is not valid JSON: {ex.Message}", ex);
            }
        }

        using (document)
        {
            return Build(document.RootElement, path);
        }
    }

    private static ConfigSchema Build(JsonElement root, string source)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(source, "the root value must be a JSON object");
        }

        if (!root.TryGetProperty("keys", out var keys) || keys.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(source, "'keys' must be an array");
        }

        var specs = new List<ConfigKeySpec>(keys.GetArrayLength());
        var index = 0;
        foreach (var entry in keys.EnumerateArray())
        {
            specs.Add(ParseKey(entry, $"{source}: keys[{index}]"));
            index++;
        }

        try
        {
            return new ConfigSchema(specs);
        }
        catch (ArgumentException ex)
        {
            throw new FormatException($"{source}: {ex.Message}", ex);
        }
    }

    private static ConfigKeySpec ParseKey(JsonElement entry, string location)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw Invalid(location, "must be a JSON object");
        }

        var key = ReadOptionalString(entry, "key", location);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw Invalid(location, "'key' is required and must be a non-empty string");
        }

        var kind = ConfigValueKind.String;
        var kindName = ReadOptionalString(entry, "kind", location);
        if (kindName is not null)
        {
            kind = ParseKind(kindName, location);
        }

        var required = true;
        if (entry.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind != JsonValueKind.Null)
        {
            if (requiredElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw Invalid(location, "'required' must be a boolean");
            }

            required = requiredElement.GetBoolean();
        }

        var pattern = ReadOptionalString(entry, "pattern", location);
        if (pattern is not null)
        {
            try
            {
                _ = new Regex(pattern);
            }
            catch (ArgumentException ex)
            {
                throw new FormatException($"{location}: 'pattern' is not a valid regular expression: {ex.Message}", ex);
            }
        }

        var description = ReadOptionalString(entry, "description", location);

        return new ConfigKeySpec(key, kind, required, pattern, description);
    }

    private static ConfigValueKind ParseKind(string kindName, string location)
    {
        foreach (var candidate in Enum.GetValues<ConfigValueKind>())
        {
            if (string.Equals(candidate.ToString(), kindName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        var known = string.Join(", ", Enum.GetNames<ConfigValueKind>());
        throw Invalid(location, $"unknown 'kind' '{kindName}' (expected one of: {known})");
    }

    /// <summary>
    /// Returns the string value of a property, or <c>null</c> when it is absent or JSON <c>null</c>.
    /// </summary>
    private static string? ReadOptionalString(JsonElement obj, string name, string location)
    {
        if (!obj.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid(location, $"'{name}' must be a string");
        }

        return element.GetString();
    }

    private static FormatException Invalid(string location, string message) =>
        new($"{location}: {message}.");
}

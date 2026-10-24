using System.Text;

namespace DotnetEnvDoctor;

/// <summary>
/// Reads a <c>.env</c> file (<c>KEY=VALUE</c> per line) into a flat <see cref="ConfigSnapshot"/>.
/// </summary>
/// <remarks>
/// Supported syntax: blank lines and lines starting with <c>#</c> are ignored; an optional leading
/// <c>export </c> is stripped; values may be wrapped in single or double quotes (double-quoted values
/// support the <c>\n</c>, <c>\"</c> and <c>\\</c> escapes); an unquoted value may end with a
/// <c> # comment</c>; <c>__</c> in keys maps to the <c>:</c> section separator. Later assignments
/// of the same key override earlier ones.
/// </remarks>
public sealed class DotEnvFileConfigSource : IConfigSource
{
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="DotEnvFileConfigSource"/> class.
    /// </summary>
    /// <param name="path">Path to the <c>.env</c> file.</param>
    public DotEnvFileConfigSource(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <inheritdoc />
    public string Name => Path.GetFileName(_path);

    /// <inheritdoc />
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="FormatException">A non-blank, non-comment line is not a valid <c>KEY=VALUE</c> assignment.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async Task<ConfigSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            throw new FileNotFoundException($"Configuration file not found: '{_path}'.", _path);
        }

        var lines = await File.ReadAllLinesAsync(_path, cancellationToken);

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            if (!TryParseLine(lines[i], out var key, out var value))
            {
                throw new FormatException($"Invalid line {i + 1} in '{_path}': expected KEY=VALUE.");
            }

            values[key] = value;
        }

        return new ConfigSnapshot(Name, values);
    }

    /// <summary>
    /// Parses a single <c>.env</c> line.
    /// </summary>
    /// <param name="line">The raw line, without its line terminator.</param>
    /// <param name="key">The parsed key with <c>__</c> replaced by <c>:</c>; empty when parsing fails.</param>
    /// <param name="value">The parsed value; <c>null</c> never occurs for a successful parse, it is empty for <c>KEY=</c>.</param>
    /// <returns>
    /// <c>true</c> when the line is a <c>KEY=VALUE</c> assignment; <c>false</c> when it is blank, a comment,
    /// has no <c>=</c>, has an empty key, or has an unterminated or badly quoted value.
    /// </returns>
    internal static bool TryParseLine(string line, out string key, out string? value)
    {
        key = string.Empty;
        value = null;

        var text = line.Trim();
        if (text.Length == 0 || text[0] == '#')
        {
            return false;
        }

        if (text.StartsWith("export ", StringComparison.Ordinal))
        {
            text = text["export ".Length..].TrimStart();
        }

        var separator = text.IndexOf('=');
        if (separator <= 0)
        {
            return false;
        }

        var name = text[..separator].Trim();
        if (name.Length == 0)
        {
            return false;
        }

        key = name.Replace("__", ":", StringComparison.Ordinal);

        var afterEquals = separator + 1;
        var valueStart = text[afterEquals..].TrimStart();
        if (valueStart.Length > 0 && (valueStart[0] == '"' || valueStart[0] == '\''))
        {
            return TryParseQuotedValue(valueStart, out value);
        }

        value = StripTrailingComment(text, afterEquals);
        return true;
    }

    /// <summary>
    /// Parses a value that starts with a quote character, dropping anything after the closing quote
    /// unless it is a <c>#</c> comment.
    /// </summary>
    private static bool TryParseQuotedValue(string raw, out string? value)
    {
        value = null;

        var quote = raw[0];
        var builder = new StringBuilder();
        var closing = -1;

        for (var i = 1; i < raw.Length; i++)
        {
            var c = raw[i];

            if (quote == '"' && c == '\\' && i + 1 < raw.Length && raw[i + 1] is 'n' or '"' or '\\')
            {
                var escaped = raw[++i];
                builder.Append(escaped == 'n' ? '\n' : escaped);
                continue;
            }

            if (c == quote)
            {
                closing = i;
                break;
            }

            builder.Append(c);
        }

        if (closing < 0)
        {
            return false;
        }

        var rest = raw[(closing + 1)..].TrimStart();
        if (rest.Length > 0 && rest[0] != '#')
        {
            return false;
        }

        value = builder.ToString();
        return true;
    }

    /// <summary>
    /// Returns the unquoted value that starts at <paramref name="start"/>, cutting off a trailing
    /// <c> # comment</c> (a <c>#</c> preceded by whitespace) and surrounding whitespace.
    /// </summary>
    private static string StripTrailingComment(string text, int start)
    {
        var end = text.Length;
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '#' && char.IsWhiteSpace(text[i - 1]))
            {
                end = i;
                break;
            }
        }

        return text[start..end].Trim();
    }
}

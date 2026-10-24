using System.Globalization;
using System.Text.RegularExpressions;

namespace DotnetEnvDoctor;

/// <summary>
/// Pure heuristics used by <see cref="SecretDetector"/> to recognize placeholders,
/// credential-like values and committed-file names.
/// </summary>
public static class SecretHeuristics
{
    private static readonly Regex[] TemplatePlaceholderPatterns =
    [
        new(@"^<.*>$", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"^\$\{.*\}$", RegexOptions.Compiled | RegexOptions.CultureInvariant),
        new(@"^%.*%$", RegexOptions.Compiled | RegexOptions.CultureInvariant),
    ];

    /// <summary>
    /// Computes the Shannon entropy of <paramref name="value"/> over its character frequencies.
    /// </summary>
    /// <param name="value">The string to measure.</param>
    /// <returns>Bits per character; <c>0</c> for an empty string, about <c>3.0</c> for eight distinct equally frequent characters.</returns>
    public static double ShannonEntropy(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            return 0;
        }

        var frequencies = new Dictionary<char, int>();
        foreach (char c in value)
        {
            frequencies.TryGetValue(c, out int count);
            frequencies[c] = count + 1;
        }

        double length = value.Length;
        double entropy = 0;
        foreach (int count in frequencies.Values)
        {
            double probability = count / length;
            entropy -= probability * Math.Log2(probability);
        }

        return entropy;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> is null, empty or whitespace, equals one of
    /// <paramref name="placeholders"/> (case-insensitive, after trimming), or has a template form
    /// such as <c>&lt;value&gt;</c>, <c>${VALUE}</c> or <c>%VALUE%</c>.
    /// </summary>
    /// <param name="value">The configuration value to check.</param>
    /// <param name="placeholders">Known placeholder strings.</param>
    /// <returns><c>true</c> when the value is a placeholder or carries no value at all.</returns>
    public static bool IsPlaceholder(string? value, IEnumerable<string> placeholders)
    {
        ArgumentNullException.ThrowIfNull(placeholders);

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string trimmed = value.Trim();
        foreach (string placeholder in placeholders)
        {
            if (string.Equals(trimmed, placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (Regex template in TemplatePlaceholderPatterns)
        {
            if (template.IsMatch(trimmed))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> starts with one of <paramref name="prefixes"/>
    /// using ordinal, case-sensitive comparison. Empty prefixes are ignored.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="prefixes">Known token prefixes such as <c>ghp_</c> or <c>AKIA</c>.</param>
    /// <returns><c>true</c> when a non-empty prefix matches.</returns>
    public static bool HasKnownTokenPrefix(string value, IEnumerable<string> prefixes)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(prefixes);

        foreach (string prefix in prefixes)
        {
            if (prefix.Length > 0 && value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> looks like a credential: it is non-null, contains no
    /// whitespace, is at least <see cref="SecretDetectorOptions.MinSecretLikeLength"/> characters long, and either
    /// starts with a known token prefix or has Shannon entropy at or above
    /// <see cref="SecretDetectorOptions.EntropyThreshold"/>.
    /// </summary>
    /// <remarks>
    /// Values that parse as an absolute URI, a boolean or a number are never secret-like, whatever their entropy.
    /// </remarks>
    /// <param name="value">The configuration value to check.</param>
    /// <param name="options">Options supplying the length, entropy and prefix thresholds.</param>
    /// <returns><c>true</c> when the value appears to be a credential.</returns>
    public static bool LooksLikeSecret(string? value, SecretDetectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (value is null || value.Length < options.MinSecretLikeLength || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out _)
            || bool.TryParse(value, out _)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        return HasKnownTokenPrefix(value, options.KnownTokenPrefixes)
            || ShannonEntropy(value) >= options.EntropyThreshold;
    }

    /// <summary>
    /// Matches <paramref name="name"/> against a glob <paramref name="pattern"/> case-insensitively.
    /// <c>*</c> matches any sequence of characters and <c>?</c> matches exactly one character; the whole name must match.
    /// </summary>
    /// <param name="name">The name to test, such as a snapshot or file name.</param>
    /// <param name="pattern">The glob pattern.</param>
    /// <returns><c>true</c> when the whole name matches the pattern.</returns>
    public static bool MatchesGlob(string name, string pattern)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(pattern);

        string regex = Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".");
        return Regex.IsMatch(name, @"\A" + regex + @"\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="name"/> matches any of the glob <paramref name="patterns"/>.
    /// </summary>
    /// <param name="name">The name to test.</param>
    /// <param name="patterns">Glob patterns, see <see cref="MatchesGlob"/>.</param>
    /// <returns><c>true</c> when at least one pattern matches.</returns>
    public static bool MatchesAnyPattern(string name, IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(patterns);

        foreach (string pattern in patterns)
        {
            if (MatchesGlob(name, pattern))
            {
                return true;
            }
        }

        return false;
    }
}

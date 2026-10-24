using System.Text.RegularExpressions;

namespace DotnetEnvDoctor;

/// <summary>
/// Heuristics for detecting placeholder values and credential-like strings.
/// </summary>
public static class SecretHeuristics
{
    private static readonly Regex KnownTokenPrefixPattern = new(
        @"^(sk-|pk-|ghp_|gho_|ghu_|ghs_|ghr_|glpat-|AKIA[A-Z0-9]|eyJ[A-Za-z0-9]|xox[bpsa]-|Bearer\s)",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> matches one of the known
    /// placeholder strings in <paramref name="placeholderValues"/> (case-insensitive).
    /// </summary>
    /// <param name="value">The configuration value to check.</param>
    /// <param name="placeholderValues">Known placeholder strings.</param>
    /// <returns><c>true</c> when the value is a placeholder.</returns>
    public static bool IsPlaceholder(string value, IEnumerable<string> placeholderValues)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(placeholderValues);

        foreach (string placeholder in placeholderValues)
        {
            if (string.Equals(value, placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> looks like a credential
    /// based on entropy analysis or known token prefixes.
    /// </summary>
    /// <param name="value">The configuration value to check.</param>
    /// <returns><c>true</c> when the value appears to be a secret.</returns>
    public static bool LooksLikeSecret(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value) || value.Length < 8)
        {
            return false;
        }

        // Known token prefixes.
        if (KnownTokenPrefixPattern.IsMatch(value))
        {
            return true;
        }

        // High Shannon entropy suggests a random token.
        double entropy = ShannonEntropy(value);
        return entropy >= 3.5 && value.Length >= 16;
    }

    /// <summary>
    /// Checks whether a snapshot name matches any of the given glob patterns.
    /// Supports <c>*</c> as a wildcard for any sequence of characters (simple file-name glob).
    /// </summary>
    /// <param name="snapshotName">The snapshot name to test.</param>
    /// <param name="patterns">Glob patterns to match against.</param>
    /// <returns><c>true</c> when at least one pattern matches.</returns>
    public static bool MatchesAnyPattern(string snapshotName, IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(snapshotName);
        ArgumentNullException.ThrowIfNull(patterns);

        foreach (string pattern in patterns)
        {
            if (GlobMatch(snapshotName, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool GlobMatch(string input, string pattern)
    {
        // Convert a simple glob pattern to a regex.
        string escaped = Regex.Escape(pattern).Replace(@"\*", ".*");
        return Regex.IsMatch(input, "^" + escaped + "$", RegexOptions.IgnoreCase);
    }

    private static double ShannonEntropy(string s)
    {
        var freq = new Dictionary<char, int>(s.Length);
        foreach (char c in s)
        {
            if (freq.TryGetValue(c, out int count))
            {
                freq[c] = count + 1;
            }
            else
            {
                freq[c] = 1;
            }
        }

        double entropy = 0.0;
        double len = s.Length;
        foreach (int count in freq.Values)
        {
            double p = count / len;
            if (p > 0)
            {
                entropy -= p * Math.Log2(p);
            }
        }

        return entropy;
    }
}

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DotnetEnvDoctor;

/// <summary>
/// Evaluates <see cref="ConfigKeySpec.Pattern"/> against a configuration value.
/// </summary>
/// <remarks>
/// Compiled expressions are cached per pattern string for the lifetime of the instance. An invalid pattern
/// or a match that exceeds the timeout is reported as <see cref="FindingCodes.InvalidPattern"/> instead of
/// throwing. Findings never include the value, because it may be a secret.
/// </remarks>
public sealed class RegexPatternRule
{
    private readonly ConcurrentDictionary<string, Regex> _cache = new(StringComparer.Ordinal);
    private readonly TimeSpan _matchTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegexPatternRule"/> class.
    /// </summary>
    /// <param name="matchTimeout">
    /// Maximum time a single match may take before it is reported as invalid. Defaults to one second.
    /// <see cref="Regex.InfiniteMatchTimeout"/> disables the timeout.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="matchTimeout"/> is zero or negative.</exception>
    public RegexPatternRule(TimeSpan? matchTimeout = null)
    {
        TimeSpan timeout = matchTimeout ?? TimeSpan.FromSeconds(1);
        if (timeout != Regex.InfiniteMatchTimeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(matchTimeout), "Match timeout must be positive.");
        }

        _matchTimeout = timeout;
    }

    /// <summary>
    /// Checks <paramref name="value"/> against the pattern declared by <paramref name="spec"/>.
    /// </summary>
    /// <param name="spec">The key specification whose <see cref="ConfigKeySpec.Pattern"/> is evaluated.</param>
    /// <param name="value">The raw configuration value, or <c>null</c> when the key is absent.</param>
    /// <param name="source">Name of the source the value was read from, if any.</param>
    /// <returns>
    /// <c>null</c> when the spec has no pattern, the value is <c>null</c>, or the value matches;
    /// otherwise an <see cref="Severity.Error"/> finding with <see cref="FindingCodes.PatternMismatch"/>,
    /// or <see cref="FindingCodes.InvalidPattern"/> when the pattern cannot be compiled or times out.
    /// </returns>
    public Finding? Evaluate(ConfigKeySpec spec, string? value, string? source)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.Pattern is null || value is null)
        {
            return null;
        }

        try
        {
            Regex regex = _cache.GetOrAdd(spec.Pattern, pattern => new Regex(pattern, RegexOptions.CultureInvariant, _matchTimeout));
            if (regex.IsMatch(value))
            {
                return null;
            }

            return new Finding(
                spec.Key,
                Severity.Error,
                FindingCodes.PatternMismatch,
                $"Value does not match pattern '{spec.Pattern}'",
                source);
        }
        catch (ArgumentException)
        {
            return InvalidPatternFinding(spec, source);
        }
        catch (RegexMatchTimeoutException)
        {
            return InvalidPatternFinding(spec, source);
        }
    }

    private static Finding InvalidPatternFinding(ConfigKeySpec spec, string? source) =>
        new(spec.Key, Severity.Error, FindingCodes.InvalidPattern, "Pattern is invalid or timed out", source);
}

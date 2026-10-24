using System.Globalization;

namespace DotnetEnvDoctor;

/// <summary>
/// Checks whether a raw configuration value conforms to a <see cref="ConfigValueKind"/>.
/// </summary>
public static class ValueKindChecker
{
    /// <summary>
    /// Determines whether <paramref name="value"/> is valid for <paramref name="kind"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ConfigValueKind.String"/>, <see cref="ConfigValueKind.Secret"/> and
    /// <see cref="ConfigValueKind.ConnectionString"/> accept any non-null value. The
    /// <paramref name="reason"/> never includes <paramref name="value"/>, because it may be a secret.
    /// </remarks>
    /// <param name="kind">The expected value kind.</param>
    /// <param name="value">The raw configuration value to check.</param>
    /// <param name="reason">
    /// <c>null</c> when the value is valid; otherwise a short sentence describing the expected form.
    /// </param>
    /// <returns><c>true</c> when the value is valid for <paramref name="kind"/>; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a defined <see cref="ConfigValueKind"/>.</exception>
    public static bool IsValid(ConfigValueKind kind, string value, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(value);

        switch (kind)
        {
            case ConfigValueKind.String:
            case ConfigValueKind.Secret:
            case ConfigValueKind.ConnectionString:
                reason = null;
                return true;

            case ConfigValueKind.Integer:
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    reason = null;
                    return true;
                }

                reason = "expected an integer (invariant culture)";
                return false;

            case ConfigValueKind.Boolean:
                if (bool.TryParse(value, out _))
                {
                    reason = null;
                    return true;
                }

                reason = "expected a boolean (true or false)";
                return false;

            case ConfigValueKind.Uri:
                if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                    && !string.IsNullOrEmpty(uri.Scheme)
                    && !string.IsNullOrEmpty(uri.Host))
                {
                    reason = null;
                    return true;
                }

                reason = "expected an absolute URI with a scheme and host";
                return false;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown configuration value kind.");
        }
    }
}

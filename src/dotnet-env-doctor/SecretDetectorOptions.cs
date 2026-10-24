namespace DotnetEnvDoctor;

/// <summary>
/// Options controlling behavior of <see cref="SecretDetector"/>.
/// </summary>
public sealed class SecretDetectorOptions
{
    /// <summary>
    /// Gets or sets well-known placeholder values that should be flagged
    /// (e.g. <c>CHANGEME</c>, <c>your-api-key</c>). Matching is case-insensitive.
    /// </summary>
    public IList<string> PlaceholderValues { get; set; } =
    [
        "CHANGEME",
        "changeme",
        "TODO",
        "your-api-key",
        "your_api_key",
        "<secret>",
        "xxx",
        "password",
        "secret",
        "123456",
    ];

    /// <summary>
    /// Gets or sets glob patterns for snapshot names that correspond to files
    /// committed to source control (e.g. <c>appsettings.json</c>, <c>appsettings.*.json</c>).
    /// Patterns are matched against the snapshot name case-insensitively.
    /// A secret value found in a matching snapshot triggers a <see cref="SecretIssueKind.SecretInCommittedFile"/> issue.
    /// <c>.env</c> is not included by default, since it is expected to stay out of source control.
    /// </summary>
    public IList<string> CommittedFilePatterns { get; set; } =
    [
        "appsettings.json",
        "appsettings.*.json",
        "*.env.example",
    ];

    /// <summary>
    /// Gets or sets the Shannon entropy threshold, in bits per character, above which a value
    /// is considered random enough to look like a credential. Must be in the range (0, 8].
    /// Default: <c>4.0</c>.
    /// </summary>
    public double EntropyThreshold { get; set; } = 4.0;

    /// <summary>
    /// Gets or sets the minimum length a value must have to be considered secret-like by entropy analysis.
    /// Must be at least <c>1</c>. Default: <c>16</c>.
    /// </summary>
    public int MinSecretLikeLength { get; set; } = 16;

    /// <summary>
    /// Gets or sets prefixes of well-known token formats (e.g. <c>sk-</c>, <c>ghp_</c>, <c>AKIA</c>)
    /// that mark a value as secret-like regardless of entropy.
    /// </summary>
    public IList<string> KnownTokenPrefixes { get; set; } =
    [
        "sk-",
        "ghp_",
        "gho_",
        "xoxb-",
        "AKIA",
        "AIza",
        "eyJ",
    ];

    /// <summary>
    /// Gets or sets a value indicating whether the detector should flag values
    /// that look like credentials even when the key is not declared as a secret in the schema.
    /// Default: <c>true</c>.
    /// </summary>
    public bool DetectUndeclaredSecretLike { get; set; } = true;

    /// <summary>
    /// Validates the option values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="EntropyThreshold"/> is outside (0, 8], or <see cref="MinSecretLikeLength"/> is less than 1.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <see cref="PlaceholderValues"/>, <see cref="CommittedFilePatterns"/> or <see cref="KnownTokenPrefixes"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// One of the lists contains a <c>null</c>, empty or whitespace-only entry.
    /// </exception>
    public void Validate()
    {
        // Written as a negated range check so that NaN is rejected as well.
        if (!(EntropyThreshold > 0 && EntropyThreshold <= 8))
        {
            throw new ArgumentOutOfRangeException(nameof(EntropyThreshold), EntropyThreshold,
                "Entropy threshold must be greater than 0 and at most 8 bits per character.");
        }

        if (MinSecretLikeLength < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinSecretLikeLength), MinSecretLikeLength,
                "Minimum secret-like length must be at least 1.");
        }

        ValidateList(PlaceholderValues, nameof(PlaceholderValues));
        ValidateList(CommittedFilePatterns, nameof(CommittedFilePatterns));
        ValidateList(KnownTokenPrefixes, nameof(KnownTokenPrefixes));
    }

    private static void ValidateList(IList<string>? values, string paramName)
    {
        ArgumentNullException.ThrowIfNull(values, paramName);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("List entries must not be null, empty or whitespace.", paramName);
            }
        }
    }
}

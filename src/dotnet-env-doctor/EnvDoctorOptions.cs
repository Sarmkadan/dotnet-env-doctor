namespace DotnetEnvDoctor;

/// <summary>
/// Options for the dotnet-env-doctor pipeline when it is registered through dependency injection.
/// </summary>
public sealed class EnvDoctorOptions
{
    /// <summary>
    /// Gets or sets the path to the JSON schema file that declares the expected configuration keys.
    /// </summary>
    public string? SchemaPath { get; set; }

    /// <summary>
    /// Gets the options controlling schema validation.
    /// </summary>
    public ValidationOptions Validation { get; } = new();

    /// <summary>
    /// Gets the options controlling secret detection.
    /// </summary>
    public SecretDetectorOptions Secrets { get; } = new();

    /// <summary>
    /// Gets or sets the timeout applied to regular expression pattern matching. Must be positive.
    /// Default: 1 second.
    /// </summary>
    public TimeSpan PatternMatchTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Validates the option values, including the nested <see cref="Secrets"/> options.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="PatternMatchTimeout"/> is zero or negative.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric value in <see cref="Secrets"/> is outside its valid range.</exception>
    /// <exception cref="ArgumentNullException">A list in <see cref="Secrets"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A list in <see cref="Secrets"/> contains a null, empty or white-space entry.</exception>
    public void Validate()
    {
        if (PatternMatchTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(PatternMatchTimeout), PatternMatchTimeout,
                "Pattern match timeout must be positive.");
        }

        Secrets.Validate();
    }
}

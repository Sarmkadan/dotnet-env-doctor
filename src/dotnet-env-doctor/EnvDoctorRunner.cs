namespace DotnetEnvDoctor;

/// <summary>
/// Facade that composes the pipeline: <see cref="IConfigSource"/> -> <see cref="ConfigSnapshot"/>
/// -> <see cref="IConfigValidator"/> / <see cref="IEnvironmentDiffer"/> / <see cref="ISecretDetector"/> -> findings.
/// </summary>
public sealed class EnvDoctorRunner
{
    private readonly IConfigSchemaLoader _schemaLoader;
    private readonly IConfigValidator _validator;
    private readonly IEnvironmentDiffer _differ;
    private readonly ISecretDetector _detector;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvDoctorRunner"/> class with the default components.
    /// </summary>
    public EnvDoctorRunner()
        : this(new JsonSchemaLoader(), new ConfigValidator(), new EnvironmentDiffer(), new SecretDetector())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvDoctorRunner"/> class.
    /// </summary>
    /// <param name="schemaLoader">Loads the schema file.</param>
    /// <param name="validator">Validates a merged snapshot against the schema.</param>
    /// <param name="differ">Compares two snapshots.</param>
    /// <param name="detector">Detects secret-related issues across sources.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public EnvDoctorRunner(
        IConfigSchemaLoader schemaLoader,
        IConfigValidator validator,
        IEnvironmentDiffer differ,
        ISecretDetector detector)
    {
        _schemaLoader = schemaLoader ?? throw new ArgumentNullException(nameof(schemaLoader));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _differ = differ ?? throw new ArgumentNullException(nameof(differ));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
    }

    /// <summary>
    /// Loads the schema, reads all sources in order, merges them and validates the result.
    /// </summary>
    /// <param name="schemaPath">Path to the schema file.</param>
    /// <param name="sources">Sources in override order (first = lowest priority, last = highest).</param>
    /// <param name="options">Validation options; defaults are used when <see langword="null"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The validation result for the merged configuration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schemaPath"/> or <paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">The schema or a source file does not exist.</exception>
    /// <exception cref="FormatException">The schema or a source file is malformed.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async Task<ValidationResult> ValidateAsync(
        string schemaPath,
        IReadOnlyList<IConfigSource> sources,
        ValidationOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);

        var schema = await _schemaLoader.LoadAsync(schemaPath, ct);
        var snapshots = await ReadAllAsync(sources, ct);
        var merged = SnapshotMerger.Merge(snapshots);

        return _validator.Validate(schema, merged.Snapshot, options);
    }

    /// <summary>
    /// Reads two sources and computes the differences between them.
    /// </summary>
    /// <param name="left">Left source.</param>
    /// <param name="right">Right source.</param>
    /// <param name="schemaPath">Optional schema used to redact secret values in the output.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The differences between the two sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="left"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">A source or the schema file does not exist.</exception>
    /// <exception cref="FormatException">A source or the schema file is malformed.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async Task<DiffResult> DiffAsync(
        IConfigSource left,
        IConfigSource right,
        string? schemaPath = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        IConfigSchema? schema = schemaPath is null
            ? null
            : await _schemaLoader.LoadAsync(schemaPath, ct);

        var leftSnapshot = await ReadOneAsync(left, ct);
        var rightSnapshot = await ReadOneAsync(right, ct);

        return _differ.Diff(leftSnapshot, rightSnapshot, schema);
    }

    /// <summary>
    /// Loads the schema, reads all sources in order and scans them for secret-related issues.
    /// </summary>
    /// <param name="schemaPath">Path to the schema file declaring which keys are secrets.</param>
    /// <param name="sources">Sources in override order (first = lowest priority, last = highest).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The issues found. Secret values are never included.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schemaPath"/> or <paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">The schema or a source file does not exist.</exception>
    /// <exception cref="FormatException">The schema or a source file is malformed.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async Task<IReadOnlyList<SecretIssue>> ScanSecretsAsync(
        string schemaPath,
        IReadOnlyList<IConfigSource> sources,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schemaPath);

        var schema = await _schemaLoader.LoadAsync(schemaPath, ct);
        var snapshots = await ReadAllAsync(sources, ct);

        return _detector.Scan(schema, snapshots);
    }

    /// <summary>
    /// Maps a validation result to a process exit code.
    /// </summary>
    /// <param name="result">The validation result.</param>
    /// <returns><c>0</c> when the configuration is valid, <c>1</c> otherwise.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static int ToExitCode(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsValid ? 0 : 1;
    }

    /// <summary>
    /// Maps secret-scan issues to a process exit code.
    /// </summary>
    /// <param name="issues">The issues returned by <see cref="ScanSecretsAsync"/>.</param>
    /// <returns>
    /// <c>0</c> when there are no issues, <c>2</c> when every issue is <see cref="SecretIssueKind.UndeclaredSecretLike"/>
    /// (a warning-level finding), <c>1</c> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="issues"/> is <see langword="null"/>.</exception>
    public static int ToExitCode(IReadOnlyList<SecretIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        if (issues.Count == 0)
        {
            return 0;
        }

        return issues.All(issue => issue.Kind == SecretIssueKind.UndeclaredSecretLike) ? 2 : 1;
    }

    /// <summary>
    /// Reads the sources one after another, preserving their order.
    /// </summary>
    private static async Task<List<ConfigSnapshot>> ReadAllAsync(
        IReadOnlyList<IConfigSource> sources,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var snapshots = new List<ConfigSnapshot>(sources.Count);
        foreach (var source in sources)
        {
            snapshots.Add(await ReadOneAsync(source, ct));
        }

        return snapshots;
    }

    private static async Task<ConfigSnapshot> ReadOneAsync(IConfigSource source, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ct.ThrowIfCancellationRequested();

        return await source.ReadAsync(ct);
    }
}

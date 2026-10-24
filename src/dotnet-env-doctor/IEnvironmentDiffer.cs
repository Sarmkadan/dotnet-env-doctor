namespace DotnetEnvDoctor;

/// <summary>
/// How a key differs between two snapshots.
/// </summary>
public enum DiffKind
{
    /// <summary>Present only in the left snapshot.</summary>
    OnlyInLeft,

    /// <summary>Present only in the right snapshot.</summary>
    OnlyInRight,

    /// <summary>Present in both with different values.</summary>
    Changed,
}

/// <summary>
/// A single key difference between two environments.
/// </summary>
/// <param name="Key">Configuration key.</param>
/// <param name="Kind">Kind of difference.</param>
/// <param name="LeftValue">Value in the left snapshot, redacted when the schema marks the key as a secret.</param>
/// <param name="RightValue">Value in the right snapshot, redacted when the schema marks the key as a secret.</param>
public sealed record DiffEntry(
    string Key,
    DiffKind Kind,
    string? LeftValue,
    string? RightValue);

/// <summary>
/// Result of diffing two snapshots.
/// </summary>
/// <param name="LeftName">Name of the left snapshot.</param>
/// <param name="RightName">Name of the right snapshot.</param>
/// <param name="Entries">Differences ordered by key.</param>
public sealed record DiffResult(
    string LeftName,
    string RightName,
    IReadOnlyList<DiffEntry> Entries)
{
    /// <summary>
    /// Gets a value indicating whether the two snapshots are identical.
    /// </summary>
    public bool IsIdentical => Entries.Count == 0;
}

/// <summary>
/// Compares two configuration snapshots (e.g. Staging vs Production).
/// </summary>
public interface IEnvironmentDiffer
{
    /// <summary>
    /// Computes the differences between two snapshots.
    /// </summary>
    /// <param name="left">Left snapshot.</param>
    /// <param name="right">Right snapshot.</param>
    /// <param name="schema">Optional schema used to redact secret values in the output.</param>
    /// <returns>Ordered list of differences.</returns>
    DiffResult Diff(ConfigSnapshot left, ConfigSnapshot right, IConfigSchema? schema = null);
}

/// <summary>
/// Default key-by-key environment differ.
/// </summary>
public sealed class EnvironmentDiffer : IEnvironmentDiffer
{
    /// <summary>
    /// Placeholder written in place of values of keys the schema marks as <see cref="ConfigValueKind.Secret"/>
    /// or <see cref="ConfigValueKind.ConnectionString"/>. Alias of <see cref="SecretRedactor.Mask"/>.
    /// </summary>
    public const string RedactedValue = SecretRedactor.Mask;

    /// <inheritdoc />
    public DiffResult Diff(ConfigSnapshot left, ConfigSnapshot right, IConfigSchema? schema = null)
    {
        var leftIndex = Index(left);
        var rightIndex = Index(right);

        var keys = leftIndex.Keys
            .Union(rightIndex.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);

        var entries = new List<DiffEntry>();
        foreach (var key in keys)
        {
            var inLeft = leftIndex.TryGetValue(key, out var l);
            var inRight = rightIndex.TryGetValue(key, out var r);

            DiffEntry entry;
            if (inLeft && inRight)
            {
                if (string.Equals(l.Value, r.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                entry = new DiffEntry(l.Key, DiffKind.Changed, l.Value, r.Value);
            }
            else if (inLeft)
            {
                entry = new DiffEntry(l.Key, DiffKind.OnlyInLeft, l.Value, null);
            }
            else
            {
                entry = new DiffEntry(r.Key, DiffKind.OnlyInRight, null, r.Value);
            }

            entry = entry with
            {
                LeftValue = SecretRedactor.RedactIfSecret(schema, entry.Key, entry.LeftValue),
                RightValue = SecretRedactor.RedactIfSecret(schema, entry.Key, entry.RightValue),
            };

            entries.Add(entry);
        }

        return new DiffResult(left.Name, right.Name, entries);
    }

    private static Dictionary<string, (string Key, string? Value)> Index(ConfigSnapshot snapshot)
    {
        var index = new Dictionary<string, (string Key, string? Value)>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in snapshot.Values)
        {
            index[pair.Key] = (pair.Key, pair.Value);
        }

        return index;
    }
}

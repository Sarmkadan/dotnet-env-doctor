namespace DotnetEnvDoctor;

/// <summary>
/// The result of merging several <see cref="ConfigSnapshot"/> instances into one effective view.
/// </summary>
/// <param name="Snapshot">
/// The merged snapshot. Its <see cref="ConfigSnapshot.Name"/> is the names of the input snapshots joined with
/// <c> + </c> (or <c>empty</c> when there were none), and its values hold the effective value of every key.
/// </param>
/// <param name="Origins">
/// Maps each key (case-insensitive) to the <see cref="ConfigSnapshot.Name"/> of the snapshot that supplied the winning value.
/// </param>
public sealed record MergedSnapshot(
    ConfigSnapshot Snapshot,
    IReadOnlyDictionary<string, string> Origins);

/// <summary>
/// Merges configuration snapshots using the same override rules as <c>Microsoft.Extensions.Configuration</c>:
/// later sources override earlier ones, and keys are compared case-insensitively.
/// </summary>
public static class SnapshotMerger
{
    /// <summary>
    /// Merges the snapshots into a single effective snapshot.
    /// </summary>
    /// <param name="snapshots">All snapshots, in override order (first = lowest priority, last = highest).</param>
    /// <returns>The merged snapshot together with the origin of each winning value.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="snapshots"/> is <see langword="null"/>, or one of its elements is <see langword="null"/>.
    /// </exception>
    public static MergedSnapshot Merge(IReadOnlyList<ConfigSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>(snapshots.Count);

        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index]
                ?? throw new ArgumentNullException(nameof(snapshots), $"Snapshot at index {index} is null.");

            names.Add(snapshot.Name);

            // A null value in a later snapshot still overrides an earlier value.
            foreach (var kvp in snapshot.Values)
            {
                values[kvp.Key] = kvp.Value;
                origins[kvp.Key] = snapshot.Name;
            }
        }

        var name = names.Count == 0 ? "empty" : string.Join(" + ", names);
        return new MergedSnapshot(new ConfigSnapshot(name, values), origins);
    }

    /// <summary>
    /// Merges the snapshots into a single effective snapshot.
    /// </summary>
    /// <param name="snapshots">All snapshots, in override order (first = lowest priority, last = highest).</param>
    /// <returns>The merged snapshot together with the origin of each winning value.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="snapshots"/> is <see langword="null"/>, or one of its elements is <see langword="null"/>.
    /// </exception>
    public static MergedSnapshot Merge(params ConfigSnapshot[] snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        return Merge((IReadOnlyList<ConfigSnapshot>)snapshots);
    }
}

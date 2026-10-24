namespace DotnetEnvDoctor;

/// <summary>
/// Stable machine-readable codes for <see cref="Finding"/> instances. Codes never change once published.
/// </summary>
public static class FindingCodes
{
    /// <summary>ED001: a key declared as required is missing or has an empty value.</summary>
    public const string MissingRequired = "ED001";

    /// <summary>ED002: a key's value does not match the <see cref="ConfigValueKind"/> declared for it.</summary>
    public const string KindMismatch = "ED002";

    /// <summary>ED003: a key's value does not match the regular expression declared for it.</summary>
    public const string PatternMismatch = "ED003";

    /// <summary>ED004: a key is present in the snapshot but not declared in the schema.</summary>
    public const string UndeclaredKey = "ED004";
}

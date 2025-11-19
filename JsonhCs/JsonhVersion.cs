namespace JsonhCs;

/// <summary>
/// The major versions of the JSONH specification.
/// </summary>
public enum JsonhVersion {
    /// <summary>
    /// Indicates that the latest version should be used (currently <see cref="V1"/>).
    /// </summary>
    Latest = 0,
    /// <summary>
    /// Version 1 of the specification, released 2025/03/19.
    /// </summary>
    V1 = 1,
    /// <summary>
    /// Version 2 of the specification, released 2025/11/19.
    /// </summary>
    V2 = 2,
}
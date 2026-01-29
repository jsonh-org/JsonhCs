using ExtendedNumerics;

namespace JsonhCs;

/// <summary>
/// Options for a <see cref="JsonhReader"/>.
/// </summary>
public record struct JsonhReaderOptions() {
    /// <summary>
    /// Specifies the major version of the JSONH specification to use.
    /// </summary>
    public JsonhVersion Version { get; set; } = JsonhVersion.Latest;
    /// <summary>
    /// Enables/disables checks for exactly one element when parsing.
    /// <code>
    /// "cat"
    /// "dog" // Error: Expected single element
    /// </code>
    /// </summary>
    /// <remarks>
    /// This option does not apply when reading elements, only when parsing elements.
    /// </remarks>
    public bool ParseSingleElement { get; set; } = false;
    /// <summary>
    /// Sets the maximum recursion depth allowed when reading JSONH.
    /// <code>
    /// // Max depth: 2
    /// {
    ///   a: {
    ///     b: {
    ///       // Error: Exceeded max depth
    ///     }
    ///   }
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// The default value is 64 to defend against DOS attacks.
    /// </remarks>
    public int MaxDepth { get; set; } = 64;
    /// <summary>
    /// Enables/disables parsing unclosed inputs.
    /// <code>
    /// {
    ///   "key": "val
    /// </code>
    /// </summary>
    /// <remarks>
    /// This is potentially useful for large language models that stream responses.<br/>
    /// Only some tokens can be incomplete in this mode, so it should not be relied upon.
    /// </remarks>
    public bool IncompleteInputs { get; set; } = false;
    /// <summary>
    /// Enables/disables parsing of numbers outside the range/precision of a double-precision float.
    /// <code>
    /// 1e99999 // parsed as exact number instead of infinity
    /// </code>
    /// </summary>
    /// <remarks>
    /// Numbers will be parsed as <see cref="BigReal"/> instead of <see cref="double"/>.
    /// </remarks>
    public bool BigNumbers { get; set; } = false;

    /// <summary>
    /// Returns whether <see cref="Version"/> is greater than or equal to <paramref name="MinimumVersion"/>.
    /// </summary>
    public readonly bool SupportsVersion(JsonhVersion MinimumVersion) {
        const JsonhVersion LatestVersion = JsonhVersion.V2;

        JsonhVersion OptionsVersion = Version is JsonhVersion.Latest ? LatestVersion : Version;
        JsonhVersion GivenVersion = MinimumVersion is JsonhVersion.Latest ? LatestVersion : MinimumVersion;

        return OptionsVersion >= GivenVersion;
    }
}
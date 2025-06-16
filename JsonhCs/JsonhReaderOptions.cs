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
}
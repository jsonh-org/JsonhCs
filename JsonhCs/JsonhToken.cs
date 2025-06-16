using System.Text.Json;

namespace JsonhCs;

/// <summary>
/// A single JSONH token.
/// </summary>
public readonly record struct JsonhToken(JsonTokenType JsonType, string Value = "") {
    /// <summary>
    /// The type of the token.
    /// </summary>
    public JsonTokenType JsonType { get; } = JsonType;
    /// <summary>
    /// The value of the token, or an empty string.
    /// </summary>
    public string Value { get; } = Value;
}
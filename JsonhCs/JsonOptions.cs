using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace JsonhCs;

/// <summary>
/// Presets for <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonOptions {
    /// <summary>
    /// A preset for reading and writing JSON with minimal formatting.<br/>
    /// <list type="bullet">
    /// <item>Allows reading numbers from strings</item>
    /// <item>Allows named floating point literals</item>
    /// <item>Allows trailing commas</item>
    /// <item>Always writes newlines as <c>\n</c></item>
    /// <item>Allows but skips comments</item>
    /// <item>Uses relaxed JSON escaping</item>
    /// </list>
    /// </summary>
    public static JsonSerializerOptions Mini { get; } = new() {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals | JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        IncludeFields = true,
        NewLine = "\n",
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    /// <summary>
    /// A preset for reading and writing JSON with indented formatting.<br/>
    /// <list type="bullet">
    /// <item>Allows reading numbers from strings</item>
    /// <item>Allows named floating point literals</item>
    /// <item>Allows trailing commas</item>
    /// <item>Always writes newlines as <c>\n</c></item>
    /// <item>Allows but skips comments</item>
    /// <item>Uses relaxed JSON escaping</item>
    /// <item>Writes indents as tabs</item>
    /// </list>
    /// </summary>
    public static JsonSerializerOptions Pretty { get; } = new(Mini) {
        WriteIndented = true,
        IndentCharacter = '\t',
        IndentSize = 1,
    };
}
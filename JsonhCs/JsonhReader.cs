﻿using System.Text;
using System.Text.Json;
using System.Globalization;
using LinkDotNet.StringBuilder;
using ResultZero;

namespace JsonhCs;

public sealed class JsonhReader : IDisposable {
    /// <summary>
    /// The text reader to read characters from.
    /// </summary>
    public TextReader TextReader { get; set; }
    /// <summary>
    /// The options to use when reading JSONH.
    /// </summary>
    public JsonhReaderOptions Options { get; set; }

    //private const char LineSeparatorChar = '\u2028';
    //private const char ParagraphSeparatorChar = '\u2029';

    private static ReadOnlySpan<char> ReservedChars => [',', ':', '[', ']', '{', '}', '/', '#', '\\'];
    private static ReadOnlySpan<char> NewlineChars => ['\n', '\r', '\u2028', '\u2029'];

    /// <summary>
    /// Constructs a reader that reads JSONH from a text reader.
    /// </summary>
    public JsonhReader(TextReader TextReader, JsonhReaderOptions? Options = null) {
        this.TextReader = TextReader;
        this.Options = Options ?? new JsonhReaderOptions();
    }
    /// <summary>
    /// Constructs a reader that reads JSONH from a stream using a given encoding.
    /// </summary>
    public JsonhReader(Stream Stream, Encoding Encoding)
        : this(new StreamReader(Stream, Encoding)) {
    }
    /// <summary>
    /// Constructs a reader that reads JSONH from a stream using the byte-order marks to detect the encoding.
    /// </summary>
    public JsonhReader(Stream Stream)
        : this(new StreamReader(Stream, detectEncodingFromByteOrderMarks: true)) {
    }
    /// <summary>
    /// Constructs a reader that reads JSONH from a string.
    /// </summary>
    public JsonhReader(string String)
        : this(new StringReader(String)) {
    }

    /// <summary>
    /// Releases all unmanaged resources used by the reader.
    /// </summary>
    public void Dispose() {
        TextReader.Dispose();
    }

    public IEnumerable<Result<JsonhToken>> ReadElement() {
        // Comments & whitespace
        foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
            if (Token.IsError) {
                yield return Token;
                yield break;
            }
            yield return Token;
        }

        // Peek char
        if (Peek() is not char Char) {
            yield return new Error("Expected token, got end of input");
            yield break;
        }

        // Object
        if (Char is '{') {
            foreach (Result<JsonhToken> Token in ReadObject(OmitBraces: false)) {
                if (Token.IsError) {
                    yield return Token;
                    yield break;
                }
                yield return Token;
            }
        }
        // String
        else if (Char is '"' or '\'') {
            yield return ReadString();
        }
        // Ambiguous (null, true, false, quoteless string, braceless object)
        else {
            foreach (Result<JsonhToken> Token in ReadAmbiguous()) {
                if (Token.IsError) {
                    yield return Token;
                    yield break;
                }
                yield return Token;
            }
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadObject(bool OmitBraces) {
        // Opening brace
        if (!OmitBraces) {
            if (!ReadWhen('{')) {
                yield return new Error("Expected `{` to start object");
                yield break;
            }
        }
        // Start of object
        yield return new JsonhToken(this, JsonTokenType.StartObject);

        // Comments & whitespace
        foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
            if (Token.IsError) {
                yield return Token;
                yield break;
            }
            yield return Token;
        }

        while (true) {
            if (Peek() is not char Char) {
                // End of incomplete object
                if (Options.IncompleteInputs) {
                    yield return new JsonhToken(this, JsonTokenType.EndObject);
                    yield break;
                }
                // End of object with omitted braces
                if (OmitBraces) {
                    yield return new JsonhToken(this, JsonTokenType.EndObject);
                    yield break;
                }
                // Missing closing brace
                yield return new Error("Expected `}` to end object, got end of input");
                yield break;
            }

            // Closing brace
            if (Char is '}') {
                // End of object with omitted braces
                if (OmitBraces) {
                    yield return new JsonhToken(this, JsonTokenType.EndObject);
                    yield break;
                }
                // End of object
                Read();
                yield return new JsonhToken(this, JsonTokenType.EndObject);
                yield break;
            }
            // Closing bracket
            else if (Char is ']') {
                // End of object with omitted braces
                if (OmitBraces) {
                    yield return new JsonhToken(this, JsonTokenType.EndObject);
                    yield break;
                }
                // Unexpected closing bracket
                yield return new Error("Unexpected `]`");
                yield break;
            }
            // Property name
            else {
                // Property name
                foreach (Result<JsonhToken> Token in ReadPropertyName()) {
                    if (Token.IsError) {
                        yield return Token;
                        yield break;
                    }
                    yield return Token;
                }

                // Comments & whitespace
                foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                    if (Token.IsError) {
                        yield return Token;
                        yield break;
                    }
                    yield return Token;
                }

                // Property value
                foreach (Result<JsonhToken> Token in ReadElement()) {
                    if (Token.IsError) {
                        yield return Token;
                        yield break;
                    }
                    yield return Token;
                }

                // Comments & whitespace
                foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                    if (Token.IsError) {
                        yield return Token;
                        yield break;
                    }
                    yield return Token;
                }

                // Optional comma
                ReadWhen(',');

                // Comments & whitespace
                foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                    if (Token.IsError) {
                        yield return Token;
                        yield break;
                    }
                    yield return Token;
                }
            }
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadPropertyName() {
        // String
        if (!ReadString().TryGetValue(out JsonhToken String, out Error StringError)) {
            yield return StringError;
            yield break;
        }

        // Comments & whitespace
        foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
            yield return Token;
            if (Token.IsError) {
                yield break;
            }
        }

        // Colon
        if (!ReadWhen(':')) {
            yield return new Error("Expected `:` after property name in object");
            yield break;
        }

        // End of property name
        yield return new JsonhToken(this, JsonTokenType.PropertyName, String.Value);
    }
    private Result<JsonhToken> ReadString() {
        char Quote;

        // Start double-quote
        if (ReadWhen('"')) {
            Quote = '"';
        }
        // Start single-quote
        else if (ReadWhen('\'')) {
            Quote = '\'';
        }
        else {
            return new Error("Expected quote to start string");
        }

        // Read string
        ValueStringBuilder StringBuilder = new();

        while (true) {
            if (Read() is not char Char) {
                return new Error("Expected end of string, got end of input");
            }

            // End quote
            if (Char == Quote) {
                return new JsonhToken(this, JsonTokenType.String, StringBuilder.ToString());
            }
            // Escape sequence
            else if (Char is '\\') {
                if (Read() is not char EscapeChar) {
                    return new Error("Expected escape character after `\\`, got end of input");
                }

                // Reverse solidus
                if (EscapeChar is '\\') {
                    StringBuilder.Append('\\');
                }
                // Backspace
                else if (EscapeChar is 'b') {
                    StringBuilder.Append('\b');
                }
                // Form feed
                else if (EscapeChar is 'f') {
                    StringBuilder.Append('\f');
                }
                // Newline
                else if (EscapeChar is 'n') {
                    StringBuilder.Append('\n');
                }
                // Carriage return
                else if (EscapeChar is 'r') {
                    StringBuilder.Append('\r');
                }
                // Tab
                else if (EscapeChar is 't') {
                    StringBuilder.Append('\t');
                }
                // Vertical tab
                else if (EscapeChar is 'v') {
                    StringBuilder.Append('\v');
                }
                // Null
                else if (EscapeChar is '0') {
                    StringBuilder.Append('\0');
                }
                // Alert
                else if (EscapeChar is 'a') {
                    StringBuilder.Append('\a');
                }
                // Escape
                else if (EscapeChar is 'e') {
                    StringBuilder.Append('\e');
                }
                // Unicode hex sequence
                else if (EscapeChar is 'u') {
                    if (!ReadHexSequence(4).TryGetValue(out uint Result, out Error Error)) {
                        return Error;
                    }
                    StringBuilder.Append((char)Result);
                }
                // Short unicode hex sequence
                else if (EscapeChar is 'x') {
                    if (!ReadHexSequence(2).TryGetValue(out uint Result, out Error Error)) {
                        return Error;
                    }
                    StringBuilder.Append((char)Result);
                }
                // Long unicode hex sequence
                else if (EscapeChar is 'U') {
                    if (!ReadHexSequence(8).TryGetValue(out uint Result, out Error Error)) {
                        return Error;
                    }
                    StringBuilder.Append((Rune)Result);
                }
                // Escaped newline
                else if (NewlineChars.Contains(EscapeChar)) {
                    // Skip
                }
                // Rune
                else {
                    StringBuilder.Append(EscapeChar);

                    // Surrogate pair
                    if (char.IsHighSurrogate(EscapeChar)) {
                        StringBuilder.Append(Read()!.Value);
                    }
                }
            }
            // Literal character
            else {
                StringBuilder.Append(Char);
            }
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadCommentsAndWhitespace() {
        while (true) {
            // Whitespace
            if (ReadWhitespace().TryGetError(out Error WhitespaceError)) {
                yield return WhitespaceError;
                yield break;
            }

            // Comment
            if (Peek() is '#' or '/') {
                yield return ReadComment();
            }
            // End of comments
            else {
                yield break;
            }
        }
    }
    private Result<JsonhToken> ReadComment() {
        /*bool HashComment = false;*/
        bool BlockComment = false;

        // Hash-style comment
        if (ReadWhen('#')) {
            /*HashComment = true;*/
        }
        else if (ReadWhen('/')) {
            // Line-style comment
            if (ReadWhen('/')) {
            }
            // Block-style comment
            else if (ReadWhen('*')) {
                BlockComment = true;
            }
            else {
                return new Error("Unexpected '/'");
            }
        }
        else {
            return new Error("Unexpected character");
        }

        // Read comment
        ValueStringBuilder StringBuilder = new();

        while (true) {
            // Peek char
            char? Char = Peek();

            if (BlockComment) {
                // Error
                if (Char is null) {
                    return new Error("Expected end of block comment, got end of input");
                }
                // End of block comment
                if (Char is '*' && ReadWhen('/')) {
                    return new JsonhToken(this, JsonTokenType.Comment/*, JsonhTokenType.BlockComment*/, StringBuilder.ToString());
                }
            }
            else {
                // End of line comment
                if (Char is null || NewlineChars.Contains(Char.Value)) {
                    return new JsonhToken(this, JsonTokenType.Comment/*, HashComment ? JsonhTokenType.HashComment : JsonhTokenType.LineComment*/, StringBuilder.ToString());
                }
            }

            // Comment char
            StringBuilder.Append(Char.Value);
        }
    }
    private Result ReadWhitespace() {
        while (true) {
            // Peek char
            if (Peek() is not char Char) {
                return Result.Success;
            }

            // Whitespace
            if (char.IsWhiteSpace(Char)) {
                Read();
            }
            // End of whitespace
            else {
                return Result.Success;
            }
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadAmbiguous() {
        // Read ambiguous token
        ValueStringBuilder StringBuilder = new();

        while (true) {
            // Read char
            if (Read() is not char Char) {
                //yield return new Error("Expected token, got end of input");
                //yield break;
                return [new Error("Expected token, got end of input")];
            }

            // Null
            if (StringBuilder.Equals("nul") && Char is 'l') {
                //yield return new JsonhToken(this, JsonTokenType.Null);
                //yield break;
                return [new JsonhToken(this, JsonTokenType.Null)];
            }
            // True
            else if (StringBuilder.Equals("tru") && Char is 'e') {
                //yield return new JsonhToken(this, JsonTokenType.True);
                //yield break;
                return [new JsonhToken(this, JsonTokenType.True)];
            }
            // False
            else if (StringBuilder.Equals("fals") && Char is 'e') {
                //yield return new JsonhToken(this, JsonTokenType.False);
                //yield break;
                return [new JsonhToken(this, JsonTokenType.False)];
            }
            // Braceless object
            else if (Char is ':' && StringBuilder.Length != 0) {
                //yield return new JsonhToken(this, JsonTokenType.StartObject);
                //yield return new JsonhToken(this, JsonTokenType.PropertyName, StringBuilder.ToString());
                return ReadObject(OmitBraces: true);
            }
            // Quoteless string
            else if (NewlineChars.Contains(Char) || ReservedChars.Contains(Char)) {
                //yield return new JsonhToken(this, JsonTokenType.String, StringBuilder.ToString());
                return [new JsonhToken(this, JsonTokenType.String, StringBuilder.ToString())];
            }

            StringBuilder.Append(Char);
        }
    }
    private Result<uint> ReadHexSequence(int Length) {
        Span<char> HexChars = stackalloc char[Length];

        for (int Index = 0; Index < Length; Index++) {
            char? Char = Read();

            // Hex digit
            if (Char is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f')) {
                HexChars[Index] = Char.Value;
            }
            // Unexpected char
            else {
                return new Error("Incorrect number of hexadecimal digits in unicode escape sequence");
            }
        }

        // Parse unicode character from hex digits
        return uint.Parse(HexChars, NumberStyles.AllowHexSpecifier);
    }
    private char? Peek() {
        int Char = TextReader.Peek();
        if (Char < 0) {
            return null;
        }
        return (char)Char;
    }
    private char? Read() {
        int Char = TextReader.Read();
        if (Char < 0) {
            return null;
        }
        return (char)Char;
    }
    private bool ReadWhen(char Char) {
        if (Peek() == Char) {
            Read();
            return true;
        }
        else {
            return false;
        }
    }
}

public struct JsonhReaderOptions {
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
    public bool IncompleteInputs { get; set; }
}

/// <summary>
/// A single JSONH token with a <see cref="JsonTokenType"/>.
/// </summary>
public readonly record struct JsonhToken(JsonhReader Reader, JsonTokenType JsonType/*, JsonhTokenType JsonhType*/, string Value = "") {
    public readonly JsonhReader Reader { get; } = Reader;
    public readonly JsonTokenType JsonType { get; } = JsonType;
    //public readonly JsonhTokenType JsonhType { get; } = JsonhType;
    public readonly string Value { get; } = Value;
}

/*/// <summary>
/// Defines the various tokens that make up a JSONH text.<br/>
/// Unlike <see cref="JsonTokenType"/>, this enum specifies the specific JSONH syntax.
/// </summary>
public enum JsonhTokenType {
    None,
    StartObject,
    EndObject,
    StartBracelessObject,
    EndBracelessObject,
    StartArray,
    EndArray,
    PropertyName,
    HashComment,
    LineComment,
    BlockComment,
    DoubleQuotedString,
    SingleQuotedString,
    MultiQuotedString,
    QuotelessString,
    Number,
    HexadecimalInteger,
    BinaryInteger,
    OctalInteger,
    NamedNumber,
    True,
    False,
    Null,
}*/
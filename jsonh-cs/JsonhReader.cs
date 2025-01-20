using LinkDotNet.StringBuilder;
using System.Text;
using System.Text.Json;
using ResultZero;

namespace JsonhCs;

public sealed class JsonhReader : IDisposable {
    /// <summary>
    /// The text reader to read characters from.
    /// </summary>
    public TextReader TextReader { get; }

    private const char LineSeparatorChar = '\u2028';
    private const char ParagraphSeparatorChar = '\u2029';

    /// <summary>
    /// Constructs a reader that reads JSONH from a text reader.
    /// </summary>
    public JsonhReader(TextReader TextReader) {
        this.TextReader = TextReader;
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

        // String
        if (Char is '"' or '\'') {
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
    private Result<JsonhToken> ReadString() {
        char Quote;

        // Start double-quote
        if (TryRead('"')) {
            Quote = '"';
        }
        // Start single-quote
        else if (TryRead('\'')) {
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
            else if (Char is '\\') {
                if (Read() is not char EscapeChar) {
                    return new Error("Expected escape character after `\\`, got end of input");
                }

                switch (EscapeChar) {
                    // Reverse solidus
                    case '\\':
                        StringBuilder.Append('\\');
                        break;
                    // Backspace
                    case 'b':
                        StringBuilder.Append('\b');
                        break;
                    // Form feed
                    case 'f':
                        StringBuilder.Append('\f');
                        break;
                    // Newline
                    case 'n':
                        StringBuilder.Append('\n');
                        break;
                    // Carriage return
                    case 'r':
                        StringBuilder.Append('\r');
                        break;
                    // Tab
                    case 't':
                        StringBuilder.Append('\t');
                        break;
                    // Vertical tab
                    case 'v':
                        StringBuilder.Append('\v');
                        break;
                    // Null
                    case '0':
                        StringBuilder.Append('\0');
                        break;
                    // Alert
                    case 'a':
                        StringBuilder.Append('\a');
                        break;
                    // Escape
                    case 'e':
                        StringBuilder.Append('\e');
                        break;
                    // Newline
                    case 'n' or 'r' or LineSeparatorChar or ParagraphSeparatorChar:
                        break;
                    // Rune
                    default:
                        // Surrogate pair
                        if (char.IsHighSurrogate(EscapeChar)) {
                            char EscapeCharLowSurrogate = Read()!.Value;
                            StringBuilder.Append(new Rune(EscapeChar, EscapeCharLowSurrogate));
                        }
                        // BMP character
                        else {
                            StringBuilder.Append(EscapeChar);
                        }
                        break;
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
        if (TryRead('#')) {
            /*HashComment = true;*/
        }
        else if (TryRead('/')) {
            // Line-style comment
            if (TryRead('/')) {
            }
            // Block-style comment
            else if (TryRead('*')) {
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
                if (Char is '*' && TryRead('/')) {
                    return new JsonhToken(this, JsonTokenType.Comment/*, JsonhTokenType.BlockComment*/, StringBuilder.ToString());
                }
            }
            else {
                // End of line comment
                if (Char is null or '\n' or '\r' or LineSeparatorChar or ParagraphSeparatorChar) {
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
    private bool TryPeek(char Char) {
        return Peek() == Char;
    }
    private bool TryRead(char Char) {
        if (TryPeek(Char)) {
            Read();
            return true;
        }
        else {
            return false;
        }
    }
}

public struct JsonhReaderOptions {

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
using LinkDotNet.StringBuilder;
using System.Text;
using System.Text.Json;
using ValueResults;

namespace Jsonh;

public sealed class JsonhReader : IDisposable {
    /// <summary>
    /// The text reader to read characters from.
    /// </summary>
    public TextReader TextReader { get; }

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
            yield return Token;
            if (Token.IsError) {
                yield break;
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

            // Peek char
            if (Peek() is not char Char) {
                yield break;
            }

            // Comment
            if (Char is '#' or '/') {
                yield return ReadComment();
            }
            // End of comments
            else {
                yield break;
            }
        }
    }
    private Result<JsonhToken> ReadComment() {
        bool BlockComment;

        // Hash-style comment
        if (TryRead('#')) {
            BlockComment = false;
        }
        else if (TryRead('/')) {
            // Line-style comment
            if (TryRead('/')) {
                BlockComment = false;
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
                    return new JsonhToken(this, JsonTokenType.Comment, StringBuilder.ToString());
                }
            }
            else {
                // End of line comment
                if (Char is null or '\n' or '\r' or '\u2028' or '\u2029') {
                    return new JsonhToken(this, JsonTokenType.Comment, StringBuilder.ToString());
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

/// <summary>
/// A single JSONH token with a <see cref="JsonTokenType"/>.
/// </summary>
public readonly record struct JsonhToken(JsonhReader Reader, JsonTokenType Type, string Value = "");

public struct JsonhOptions {

}
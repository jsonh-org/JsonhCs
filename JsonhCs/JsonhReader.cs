using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    /// <summary>
    /// Characters that cannot be used in quoteless strings.
    /// </summary>
    private static readonly SearchValues<char> ReservedChars = SearchValues.Create([',', ':', '[', ']', '{', '}', '/', '#', '\\']);
    /// <summary>
    /// Characters that serve as newlines in strings.
    /// </summary>
    private static readonly SearchValues<char> NewlineChars = SearchValues.Create(['\n', '\r', '\u2028', '\u2029']);

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

    /// <summary>
    /// Parses a single element from the stream.
    /// </summary>
    public Result<T?> ParseElement<T>() {
        return ParseNode().Try(Value => Value.Deserialize<T>(GlobalJsonOptions.Mini));
    }
    /// <inheritdoc cref="ParseElement{T}(bool)"/>
    public Result<JsonElement> ParseElement() {
        return ParseElement<JsonElement>();
    }
    /// <summary>
    /// Parses a single <see cref="JsonNode"/> from the stream.
    /// </summary>
    public Result<JsonNode?> ParseNode() {
        JsonNode? CurrentNode = null;
        string? CurrentPropertyName = null;

        bool SubmitNode(JsonNode? Node) {
            // Root value
            if (CurrentNode is null) {
                return true;
            }
            // Array item
            if (CurrentPropertyName is null) {
                CurrentNode.AsArray().Add(Node);
                return false;
            }
            // Object property
            else {
                CurrentNode.AsObject().Add(CurrentPropertyName, Node);
                CurrentPropertyName = null;
                return false;
            }
        }
        void StartNode(JsonNode Node) {
            SubmitNode(Node);
            CurrentNode = Node;
        }

        foreach (Result<JsonhToken> TokenResult in ReadElement()) {
            // Check error
            if (!TokenResult.TryGetValue(out JsonhToken Token, out Error Error)) {
                return Error;
            }

            // Null
            if (Token.JsonType is JsonTokenType.Null) {
                JsonValue? Node = null;
                if (SubmitNode(Node)) {
                    return Node;
                }
            }
            // True
            else if (Token.JsonType is JsonTokenType.True) {
                JsonValue Node = JsonValue.Create(true);
                if (SubmitNode(Node)) {
                    return Node;
                }
            }
            // False
            else if (Token.JsonType is JsonTokenType.False) {
                JsonValue Node = JsonValue.Create(false);
                if (SubmitNode(Node)) {
                    return Node;
                }
            }
            // String
            else if (Token.JsonType is JsonTokenType.String) {
                JsonValue Node = JsonValue.Create(Token.Value);
                if (SubmitNode(Node)) {
                    return Node;
                }
            }
            // Number
            else if (Token.JsonType is JsonTokenType.Number) {
                // TODO:
                // A number node can't be created from a string yet, so create a string node instead.
                // See https://github.com/dotnet/runtime/discussions/111373
                JsonNode Node = JsonValue.Create(Token.Value);
                if (SubmitNode(Node)) {
                    return Node;
                }
            }
            // Start Object
            else if (Token.JsonType is JsonTokenType.StartObject) {
                JsonObject Node = [];
                StartNode(Node);
            }
            // Start Array
            else if (Token.JsonType is JsonTokenType.StartArray) {
                JsonArray Node = [];
                StartNode(Node);
            }
            // End Object/Array
            else if (Token.JsonType is JsonTokenType.EndObject or JsonTokenType.EndArray) {
                // Nested node
                if (CurrentNode?.Parent is not null) {
                    CurrentNode = CurrentNode.Parent;
                }
                // Root node
                else {
                    return CurrentNode;
                }
            }
            // Property Name
            else if (Token.JsonType is JsonTokenType.PropertyName) {
                CurrentPropertyName = Token.Value;
            }
            // Comment
            else if (Token.JsonType is JsonTokenType.Comment) {
                // Pass
            }
            // Not implemented
            else {
                throw new NotImplementedException(Token.JsonType.ToString());
            }
        }

        // End of input
        return new Error("Expected token, got end of input");
    }
    /// <summary>
    /// Tries to find the given property name in the stream.<br/>
    /// For example, to find <c>c</c>:
    /// <code>
    /// // Original position
    /// {
    ///   "a": "1",
    ///   "b": {
    ///     "c": "2"
    ///   },
    ///   "c":/* Final position */ "3"
    /// }
    /// </code>
    /// </summary>
    public bool FindPropertyValue(string PropertyName) {
        long CurrentDepth = 0;

        foreach (Result<JsonhToken> TokenResult in ReadElement()) {
            // Check error
            if (!TokenResult.TryGetValue(out JsonhToken Token)) {
                return false;
            }

            // Start structure
            if (Token.JsonType is JsonTokenType.StartObject or JsonTokenType.StartArray) {
                CurrentDepth++;
            }
            // End structure
            else if (Token.JsonType is JsonTokenType.EndObject or JsonTokenType.EndArray) {
                CurrentDepth--;
            }
            // Property name
            else if (Token.JsonType is JsonTokenType.PropertyName) {
                if (CurrentDepth == 1 && Token.Value == PropertyName) {
                    // Path found
                    return true;
                }
            }
        }

        // Path not found
        return false;
    }
    public IEnumerable<Result<JsonhToken>> ReadElement() {
        // Comments & whitespace
        foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
            if (Token.IsError) {
                yield return Token.Error;
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
            foreach (Result<JsonhToken> Token in ReadObject()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }
        }
        // Array
        else if (Char is '[') {
            foreach (Result<JsonhToken> Token in ReadArray()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }
        }
        // Primitive value (null, true, false, string, number, braceless object, bracketless array)
        else {
            foreach (Result<JsonhToken> Token in ReadPrimitiveElement()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadObject() {
        // Opening brace
        bool OmitBraces = false;
        if (!ReadOne('{')) {
            OmitBraces = true;
        }
        // Start of object
        yield return new JsonhToken(this, JsonTokenType.StartObject);

        // Comments & whitespace
        foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
            if (Token.IsError) {
                yield return Token.Error;
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

            // End of object with omitted braces
            if (OmitBraces && Char is '}' or ']') {
                yield return new JsonhToken(this, JsonTokenType.EndObject);
                yield break;
            }
            // Closing brace
            else if (Char is '}') {
                // End of object
                Read();
                yield return new JsonhToken(this, JsonTokenType.EndObject);
                yield break;
            }
            // Property
            else {
                // Property name
                foreach (Result<JsonhToken> Token in ReadPropertyName()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }

                // Comments & whitespace
                foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }

                // Property value
                foreach (Result<JsonhToken> Token in ReadElement()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }

                // Comments & whitespace
                foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }

                // Optional comma
                ReadOne(',');

                // Comments & whitespace
                foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }
            }
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadArray() {
        // Opening bracket
        bool OmitBrackets = false;
        if (!ReadOne('[')) {
            OmitBrackets = true;
        }
        // Start of array
        yield return new JsonhToken(this, JsonTokenType.StartArray);

        // Comments & whitespace
        foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
            if (Token.IsError) {
                yield return Token.Error;
                yield break;
            }
            yield return Token;
        }

        while (true) {
            if (Peek() is not char Char) {
                // End of incomplete array
                if (Options.IncompleteInputs) {
                    yield return new JsonhToken(this, JsonTokenType.EndArray);
                    yield break;
                }
                // Missing closing bracket
                yield return new Error("Expected `]` to end array, got end of input");
                yield break;
            }

            // End of array with omitted brackets
            if (OmitBrackets && Char is '}' or ']') {
                yield return new JsonhToken(this, JsonTokenType.EndArray);
                yield break;
            }
            // Closing bracket
            else if (Char is ']') {
                // End of array
                Read();
                yield return new JsonhToken(this, JsonTokenType.EndArray);
                yield break;
            }
            // Item
            else {
                // Element
                foreach (Result<JsonhToken> Token in ReadElement()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }

                // Comments & whitespace
                foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }

                // Optional comma
                ReadOne(',');

                // Comments & whitespace
                foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                    if (Token.IsError) {
                        yield return Token.Error;
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
            if (Token.IsError) {
                yield return Token.Error;
                yield break;
            }
            yield return Token;
        }

        // Colon
        if (!ReadOne(':')) {
            yield return new Error("Expected `:` after property name in object");
            yield break;
        }

        // End of property name
        yield return new JsonhToken(this, JsonTokenType.PropertyName, String.Value);
    }
    private Result<JsonhToken> ReadString() {
        // Start quote
        if (ReadAny('"', '\'') is not char StartQuote) {
            return new Error("Expected quote to start string");
        }

        // Count multiple start quotes
        int StartQuoteCounter = 1;
        while (ReadOne(StartQuote)) {
            StartQuoteCounter++;
        }

        // Empty string
        if (StartQuoteCounter == 2) {
            return new JsonhToken(this, JsonTokenType.String, "");
        }

        // Count multiple end quotes
        int EndQuoteCounter = 0;

        // Read string
        using ValueStringBuilder StringBuilder = new(stackalloc char[32]);

        while (true) {
            if (Read() is not char Char) {
                return new Error("Expected end of string, got end of input");
            }

            // Partial end quote was actually part of string
            if (Char != StartQuote) {
                while (EndQuoteCounter > 0) {
                    StringBuilder.Append(StartQuote);
                    EndQuoteCounter--;
                }
            }

            // End quote
            if (Char == StartQuote) {
                EndQuoteCounter++;
                if (EndQuoteCounter == StartQuoteCounter) {
                    break;
                }
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
                    // Join CR LF
                    if (EscapeChar is '\r') {
                        ReadOne('\n');
                    }
                }
                // Other
                else {
                    StringBuilder.Append(EscapeChar);
                }
            }
            // Literal character
            else {
                StringBuilder.Append(Char);
            }
        }

        // Trim leading whitespace in multiline string
        if (StartQuoteCounter > 1) {
            // Count leading whitespace preceding closing quotes
            int LastNewlineIndex = StringBuilder.AsSpan().LastIndexOfAny(NewlineChars);
            if (LastNewlineIndex != -1) {
                int LeadingWhitespaceCount = StringBuilder.Length - LastNewlineIndex;

                // Remove leading whitespace from each line
                if (LeadingWhitespaceCount > 0) {
                    int CurrentLeadingWhitespace = 0;
                    bool IsLeadingWhitespace = true;

                    for (int Index = 0; Index < StringBuilder.Length; Index++) {
                        char Char = StringBuilder[Index];

                        // Newline
                        if (NewlineChars.Contains(Char)) {
                            // Reset leading whitespace counter
                            CurrentLeadingWhitespace = 0;
                            // Enter leading whitespace
                            IsLeadingWhitespace = true;
                        }
                        // Leading whitespace
                        else if (IsLeadingWhitespace && CurrentLeadingWhitespace <= LeadingWhitespaceCount) {
                            // Whitespace
                            if (char.IsWhiteSpace(Char)) {
                                // Increment leading whitespace counter
                                CurrentLeadingWhitespace++;
                                // Maximum leading whitespace reached
                                if (CurrentLeadingWhitespace == LeadingWhitespaceCount) {
                                    // Remove leading whitespace
                                    StringBuilder.Remove(Index - CurrentLeadingWhitespace, CurrentLeadingWhitespace);
                                    // Exit leading whitespace
                                    IsLeadingWhitespace = false;
                                }
                            }
                            // Non-whitespace
                            else {
                                // Remove partial leading whitespace
                                StringBuilder.Remove(Index - CurrentLeadingWhitespace, CurrentLeadingWhitespace);
                                // Exit leading whitespace
                                IsLeadingWhitespace = false;
                            }
                        }
                    }

                    // Remove leading whitespace from last line
                    StringBuilder.Remove(StringBuilder.Length - LeadingWhitespaceCount, LeadingWhitespaceCount);

                    // Remove leading newline
                    if (StringBuilder.Length >= 1) {
                        char LeadingChar = StringBuilder[0];
                        if (NewlineChars.Contains(LeadingChar)) {
                            int NewlineLength = 1;
                            // Join CR LF
                            if (LeadingChar is '\r' && StringBuilder.Length >= 2 && StringBuilder[1] is '\n') {
                                NewlineLength = 2;
                            }

                            // Remove leading newline
                            StringBuilder.Remove(0, NewlineLength);
                        }
                    }
                }
            }
        }

        // End of string
        return new JsonhToken(this, JsonTokenType.String, StringBuilder.ToString());
    }
    private Result<JsonhToken> ReadNumber() {
        // Read number
        using ValueStringBuilder StringBuilder = new(stackalloc char[32]);

        // Read base
        ReadOnlySpan<char> BaseDigits = "0123456789";
        if (ReadOne('0')) {
            StringBuilder.Append('0');

            if (ReadAny('x', 'X') is char HexBaseChar) {
                StringBuilder.Append(HexBaseChar);
                BaseDigits = "0123456789ABCDEFabcdef";
            }
            else if (ReadAny('b', 'B') is char BinaryBaseChar) {
                StringBuilder.Append(BinaryBaseChar);
                BaseDigits = "01";
            }
            else if (ReadAny('o', 'O') is char OctalBaseChar) {
                StringBuilder.Append(OctalBaseChar);
                BaseDigits = "01234567";
            }
        }

        // Read integer + fraction
        ReadNumberCore(StringBuilder, BaseDigits);

        // Exponent
        if (ReadAny('e', 'E') is char ExponentChar) {
            StringBuilder.Append(ExponentChar);
            // Read integer + fraction
            ReadNumberCore(StringBuilder, BaseDigits);
        }

        // End of number
        return new JsonhToken(this, JsonTokenType.Number, StringBuilder.ToString());
    }
    private Result ReadNumberCore(ValueStringBuilder StringBuilder, ReadOnlySpan<char> BaseDigits) {
        // Read sign
        ReadAny('-', '+');

        // Disallow leading underscores
        if (ReadOne('_')) {
            return new Error("Leading `_` in number");
        }

        bool IsFraction = false;

        while (true) {
            // Peek char
            if (Peek() is not char Char) {
                break;
            }

            // Digit
            if (BaseDigits.Contains(Char)) {
                StringBuilder.Append(Char);
            }
            // Decimal point
            else if (Char is '.') {
                if (IsFraction) {
                    return new Error("Duplicate `.` in number");
                }
                IsFraction = true;
                StringBuilder.Append(Char);
            }
            // Underscore
            else if (Char is '_') {
                StringBuilder.Append(Char);
            }
            // Other
            else {
                break;
            }
        }

        // Disallow trailing underscores
        if (StringBuilder.AsSpan().EndsWith("_")) {
            return new Error("Trailing `_` in number");
        }
        // End of number
        return Result.Success;
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
        bool BlockComment = false;

        // Hash-style comment
        if (ReadOne('#')) {
        }
        else if (ReadOne('/')) {
            // Line-style comment
            if (ReadOne('/')) {
            }
            // Block-style comment
            else if (ReadOne('*')) {
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
        using ValueStringBuilder StringBuilder = new(stackalloc char[32]);

        while (true) {
            // Peek char
            char? Char = Peek();

            if (BlockComment) {
                // Error
                if (Char is null) {
                    return new Error("Expected end of block comment, got end of input");
                }
                // End of block comment
                if (Char is '*' && ReadOne('/')) {
                    return new JsonhToken(this, JsonTokenType.Comment, StringBuilder.ToString());
                }
            }
            else {
                // End of line comment
                if (Char is null || NewlineChars.Contains(Char.Value)) {
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
    private IEnumerable<Result<JsonhToken>> ReadPrimitiveElement() {
        /*// Read ambiguous token
        StringBuilder StringBuilder = new();

        while (true) {
            // Read char
            if (Read() is not char Char) {
                yield return new Error("Expected token, got end of input");
                yield break;
            }

            // Null
            if (StringBuilder.Equals("nul") && Char is 'l') {
                yield return new JsonhToken(this, JsonTokenType.Null);
            }
            // True
            else if (StringBuilder.Equals("tru") && Char is 'e') {
                yield return new JsonhToken(this, JsonTokenType.True);
            }
            // False
            else if (StringBuilder.Equals("fals") && Char is 'e') {
                yield return new JsonhToken(this, JsonTokenType.False);
            }
            // Braceless object
            else if (Char is ':' && StringBuilder.Length != 0) {
                foreach (Result<JsonhToken> Token in ReadObject(OmitBraces: true)) {
                    yield return Token;
                }
            }
            // Quoteless string
            else if (NewlineChars.Contains(Char) || ReservedChars.Contains(Char)) {
                yield return new JsonhToken(this, JsonTokenType.String, StringBuilder.ToString());
            }
            // String
            else if (Char is '"' or '\'') {
                yield return ReadString();
            }

            StringBuilder.Append(Char);
        }*/

        if (ReadString().TryGetValue(out JsonhToken String)) {
            // Comments & whitespace
            List<JsonhToken>? Tokens = null;
            foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                Tokens ??= [];
                Tokens.Add(Token.Value);
            }

            // TODO: Add pending tokens

            // Braceless object
            if (ReadOne(':')) {
                foreach (Result<JsonhToken> Token in ReadObject()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }
                yield break;
            }
            // Bracketless array
            else if (ReadOne(',')) {
                foreach (Result<JsonhToken> Token in ReadArray()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }
                yield break;
            }
            // String
            else {
                yield return new JsonhToken(this, JsonTokenType.String, String.Value);
                yield break;
            }
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
    private bool ReadOne(char Char) {
        if (Peek() == Char) {
            Read();
            return true;
        }
        return false;
    }
    private char? ReadAny(params ReadOnlySpan<char> Options) {
        // Peek char
        if (Peek() is not char Char) {
            return null;
        }
        // Match option
        int OptionIndex = Options.IndexOf(Char);
        if (OptionIndex < 0) {
            return null;
        }
        // Option matched
        return Options[OptionIndex];
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
public readonly record struct JsonhToken(JsonhReader Reader, JsonTokenType JsonType, string Value = "") {
    public JsonhReader Reader { get; } = Reader;
    public JsonTokenType JsonType { get; } = JsonType;
    public string Value { get; } = Value;
}
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExtendedNumerics;
using LinkDotNet.StringBuilder;
using ResultZero;

namespace JsonhCs;

/// <summary>
/// A reader that reads JSONH tokens from a <see cref="System.IO.TextReader"/>.
/// </summary>
public sealed partial class JsonhReader : IDisposable {
    /// <summary>
    /// The text reader to read characters from.
    /// </summary>
    public TextReader TextReader { get; set; }
    /// <summary>
    /// The options to use when reading JSONH.
    /// </summary>
    public JsonhReaderOptions Options { get; set; }
    /// <summary>
    /// The number of characters read from <see cref="TextReader"/>.
    /// </summary>
    public long CharCounter { get; set; }

    /// <summary>
    /// Characters that cannot be used unescaped in quoteless strings.
    /// </summary>
    private static readonly SearchValues<char> ReservedChars = SearchValues.Create(['\\', ',', ':', '[', ']', '{', '}', '/', '#', '"', '\'']);
    /// <summary>
    /// Characters that are considered newlines.
    /// </summary>
    private static readonly SearchValues<char> NewlineChars = SearchValues.Create(['\n', '\r', '\u2028', '\u2029']);

    /// <summary>
    /// Warning message for dynamic serialization.
    /// </summary>
    private const string UnreferencedCodeMessage = "JSON serialization and deserialization might require types that cannot be statically analyzed.";

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
    public JsonhReader(Stream Stream, Encoding Encoding, JsonhReaderOptions? Options = null)
        : this(new StreamReader(Stream, Encoding), Options) {
    }
    /// <summary>
    /// Constructs a reader that reads JSONH from a stream using the byte-order marks to detect the encoding.
    /// </summary>
    public JsonhReader(Stream Stream, JsonhReaderOptions? Options = null)
        : this(new StreamReader(Stream, detectEncodingFromByteOrderMarks: true), Options) {
    }
    /// <summary>
    /// Constructs a reader that reads JSONH from a string.
    /// </summary>
    public JsonhReader(string String, JsonhReaderOptions? Options = null)
        : this(new StringReader(String), Options) {
    }

    /// <summary>
    /// Releases all unmanaged resources used by the reader.
    /// </summary>
    public void Dispose() {
        TextReader.Dispose();
    }

    /// <summary>
    /// Parses a single element from a text reader.
    /// </summary>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public static Result<T?> ParseElement<T>(TextReader TextReader, JsonhReaderOptions? Options = null) {
        return new JsonhReader(TextReader, Options).ParseElement<T>();
    }
    /// <inheritdoc cref="ParseElement{T}(TextReader, JsonhReaderOptions?)"/>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public static Result<JsonElement> ParseElement(TextReader TextReader, JsonhReaderOptions? Options = null) {
        return ParseElement<JsonElement>(TextReader, Options);
    }
    /// <inheritdoc cref="ParseElement{T}(TextReader, JsonhReaderOptions?)"/>
    public static Result<JsonNode?> ParseNode(TextReader TextReader, JsonhReaderOptions? Options = null) {
        return new JsonhReader(TextReader, Options).ParseNode();
    }
    /// <summary>
    /// Parses a single element from a stream using a given encoding.
    /// </summary>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public static Result<T?> ParseElement<T>(Stream Stream, Encoding Encoding, JsonhReaderOptions? Options = null) {
        return new JsonhReader(Stream, Encoding, Options).ParseElement<T>();
    }
    /// <inheritdoc cref="ParseElement{T}(Stream, Encoding, JsonhReaderOptions?)"/>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public static Result<JsonElement> ParseElement(Stream Stream, Encoding Encoding, JsonhReaderOptions? Options = null) {
        return ParseElement<JsonElement>(Stream, Encoding, Options);
    }
    /// <inheritdoc cref="ParseElement{T}(Stream, Encoding, JsonhReaderOptions?)"/>
    public static Result<JsonNode?> ParseNode(Stream Stream, Encoding Encoding, JsonhReaderOptions? Options = null) {
        return new JsonhReader(Stream, Encoding, Options).ParseNode();
    }
    /// <summary>
    /// Parses a single element from a stream using the byte-order marks to detect the encoding.
    /// </summary>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public static Result<T?> ParseElement<T>(Stream Stream, JsonhReaderOptions? Options = null) {
        return new JsonhReader(Stream, Options).ParseElement<T>();
    }
    /// <inheritdoc cref="ParseElement{T}(Stream, JsonhReaderOptions?)"/>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public static Result<JsonElement> ParseElement(Stream Stream, JsonhReaderOptions? Options = null) {
        return ParseElement<JsonElement>(Stream, Options);
    }
    /// <inheritdoc cref="ParseElement{T}(Stream, JsonhReaderOptions?)"/>
    public static Result<JsonNode?> ParseNode(Stream Stream, JsonhReaderOptions? Options = null) {
        return new JsonhReader(Stream, Options).ParseNode();
    }
    /// <summary>
    /// Parses a single element from a string.
    /// </summary>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public static Result<T?> ParseElement<T>(string String, JsonhReaderOptions? Options = null) {
        return new JsonhReader(String, Options).ParseElement<T>();
    }
    /// <inheritdoc cref="ParseElement{T}(string, JsonhReaderOptions?)"/>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public static Result<JsonElement> ParseElement(string String, JsonhReaderOptions? Options = null) {
        return ParseElement<JsonElement>(String, Options);
    }
    /// <inheritdoc cref="ParseElement{T}(string, JsonhReaderOptions?)"/>
    public static Result<JsonNode?> ParseNode(string String, JsonhReaderOptions? Options = null) {
        return new JsonhReader(String, Options).ParseNode();
    }

    /// <summary>
    /// Parses a single element from the reader.
    /// </summary>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public Result<T?> ParseElement<T>() {
        return ParseNode().Try(Value => Value.Deserialize<T>(MiniJson));
    }
    /// <inheritdoc cref="ParseElement{T}()"/>
    [RequiresUnreferencedCode(UnreferencedCodeMessage), RequiresDynamicCode(UnreferencedCodeMessage)]
    public Result<JsonElement> ParseElement() {
        return ParseElement<JsonElement>();
    }
    /// <summary>
    /// Parses a single <see cref="JsonNode"/> from the reader.
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
                CurrentNode.AsObject()[CurrentPropertyName] = Node;
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

            switch (Token.JsonType) {
                // Null
                case JsonTokenType.Null: {
                    JsonValue? Node = null;
                    if (SubmitNode(Node)) {
                        return Node;
                    }
                    break;
                }
                // True
                case JsonTokenType.True: {
                    JsonValue Node = JsonValue.Create(true);
                    if (SubmitNode(Node)) {
                        return Node;
                    }
                    break;
                }
                // False
                case JsonTokenType.False: {
                    JsonValue Node = JsonValue.Create(false);
                    if (SubmitNode(Node)) {
                        return Node;
                    }
                    break;
                }
                // String
                case JsonTokenType.String: {
                    JsonValue Node = JsonValue.Create(Token.Value);
                    if (SubmitNode(Node)) {
                        return Node;
                    }
                    break;
                }
                // Number
                case JsonTokenType.Number: {
                    if (JsonhNumberParser.Parse(Token.Value).TryGetError(out Error NumberError, out BigReal Number)) {
                        return NumberError;
                    }
                    JsonNode Node = JsonNode.Parse(Number.ToString())!;
                    if (SubmitNode(Node)) {
                        return Node;
                    }
                    break;
                }
                // Start Object
                case JsonTokenType.StartObject: {
                    JsonObject Node = [];
                    StartNode(Node);
                    break;
                }
                // Start Array
                case JsonTokenType.StartArray: {
                    JsonArray Node = [];
                    StartNode(Node);
                    break;
                }
                // End Object/Array
                case JsonTokenType.EndObject or JsonTokenType.EndArray: {
                    // Nested node
                    if (CurrentNode?.Parent is not null) {
                        CurrentNode = CurrentNode.Parent;
                    }
                    // Root node
                    else {
                        return CurrentNode;
                    }
                    break;
                }
                // Property Name
                case JsonTokenType.PropertyName: {
                    CurrentPropertyName = Token.Value;
                    break;
                }
                // Comment
                case JsonTokenType.Comment: {
                    break;
                }
                // Not implemented
                default: {
                    throw new NotImplementedException(Token.JsonType.ToString());
                }
            }
        }

        // End of input
        return new Error("Expected token, got end of input");
    }
    /// <summary>
    /// Tries to find the given property name in the reader.<br/>
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

            switch (Token.JsonType) {
                // Start structure
                case JsonTokenType.StartObject or JsonTokenType.StartArray: {
                    CurrentDepth++;
                    break;
                }
                // End structure
                case JsonTokenType.EndObject or JsonTokenType.EndArray: {
                    CurrentDepth--;
                    break;
                }
                // Property name
                case JsonTokenType.PropertyName: {
                    if (CurrentDepth == 1 && Token.Value == PropertyName) {
                        // Path found
                        return true;
                    }
                    break;
                }
            }
        }

        // Path not found
        return false;
    }
    /// <summary>
    /// Reads a single element from the reader.
    /// </summary>
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
        if (Peek() is not char Next) {
            yield return new Error("Expected token, got end of input");
            yield break;
        }

        // Object
        if (Next is '{') {
            foreach (Result<JsonhToken> Token in ReadObject()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }
        }
        // Array
        else if (Next is '[') {
            foreach (Result<JsonhToken> Token in ReadArray()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }
        }
        // Primitive value (null, true, false, string, number)
        else {
            Result<JsonhToken> Token = ReadPrimitiveElement();
            if (Token.IsError) {
                yield return Token.Error;
                yield break;
            }

            // Detect braceless object from property name
            if (Token.Value.JsonType is JsonTokenType.String) {
                // Try read property name
                List<JsonhToken> PropertyNameTokens = [];
                foreach (Result<JsonhToken> PropertyNameToken in ReadPropertyName(Token.Value.Value)) {
                    // Possible braceless object
                    if (PropertyNameToken.TryGetValue(out JsonhToken Value)) {
                        PropertyNameTokens.Add(Value);
                    }
                    // Primitive value (error reading property name)
                    else {
                        yield return Token.Value;
                        foreach (Result<JsonhToken> NonPropertyNameToken in PropertyNameTokens) {
                            yield return NonPropertyNameToken;
                        }
                        yield break;
                    }
                }
                // Braceless object
                foreach (Result<JsonhToken> ObjectToken in ReadBracelessObject(PropertyNameTokens)) {
                    if (ObjectToken.IsError) {
                        yield return ObjectToken.Error;
                        yield break;
                    }
                    yield return ObjectToken;
                }
            }
            // Primitive value
            else {
                yield return Token;
            }
        }
    }

    private IEnumerable<Result<JsonhToken>> ReadObject() {
        // Opening brace
        if (!ReadOne('{')) {
            // Braceless object
            foreach (Result<JsonhToken> Token in ReadBracelessObject()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }
            yield break;
        }
        // Start of object
        yield return new JsonhToken(JsonTokenType.StartObject);

        while (true) {
            // Comments & whitespace
            foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }

            if (Peek() is not char Next) {
                // End of incomplete object
                if (Options.IncompleteInputs) {
                    yield return new JsonhToken(JsonTokenType.EndObject);
                    yield break;
                }
                // Missing closing brace
                yield return new Error("Expected `}` to end object, got end of input");
                yield break;
            }

            // Closing brace
            if (Next is '}') {
                // End of object
                Read();
                yield return new JsonhToken(JsonTokenType.EndObject);
                yield break;
            }
            // Property
            else {
                foreach (Result<JsonhToken> Token in ReadProperty()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }
            }
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadBracelessObject(IEnumerable<JsonhToken>? PropertyNameTokens = null) {
        // Start of object
        yield return new JsonhToken(JsonTokenType.StartObject);

        // Initial tokens
        if (PropertyNameTokens is not null) {
            foreach (Result<JsonhToken> InitialToken in ReadProperty(PropertyNameTokens)) {
                if (InitialToken.IsError) {
                    yield return InitialToken.Error;
                    yield break;
                }
                yield return InitialToken;
            }
        }

        while (true) {
            // Comments & whitespace
            foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }

            if (Peek() is not char) {
                // End of braceless object
                yield return new JsonhToken(JsonTokenType.EndObject);
                yield break;
            }

            // Property
            foreach (Result<JsonhToken> Token in ReadProperty()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadProperty(IEnumerable<JsonhToken>? PropertyNameTokens = null) {
        // Property name
        if (PropertyNameTokens is not null) {
            foreach (JsonhToken Token in PropertyNameTokens) {
                yield return Token;
            }
        }
        else {
            foreach (Result<JsonhToken> Token in ReadPropertyName()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }
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
    }
    private IEnumerable<Result<JsonhToken>> ReadPropertyName(string? String = null) {
        // String
        if (String is null) {
            if (ReadString().TryGetError(out Error StringError, out JsonhToken StringToken)) {
                yield return StringError;
                yield break;
            }
            String = StringToken.Value;
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
        yield return new JsonhToken(JsonTokenType.PropertyName, String);
    }
    private IEnumerable<Result<JsonhToken>> ReadArray() {
        // Opening bracket
        if (!ReadOne('[')) {
            yield return new Error("Expected `[` to start array");
            yield break;
        }
        // Start of array
        yield return new JsonhToken(JsonTokenType.StartArray);

        while (true) {
            // Comments & whitespace
            foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }

            if (Peek() is not char Next) {
                // End of incomplete array
                if (Options.IncompleteInputs) {
                    yield return new JsonhToken(JsonTokenType.EndArray);
                    yield break;
                }
                // Missing closing bracket
                yield return new Error("Expected `]` to end array, got end of input");
                yield break;
            }

            // Closing bracket
            if (Next is ']') {
                // End of array
                Read();
                yield return new JsonhToken(JsonTokenType.EndArray);
                yield break;
            }
            // Item
            else {
                foreach (Result<JsonhToken> Token in ReadItem()) {
                    if (Token.IsError) {
                        yield return Token.Error;
                        yield break;
                    }
                    yield return Token;
                }
            }
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadItem() {
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
    }
    private Result<JsonhToken> ReadString() {
        // Start quote
        if (ReadAny('"', '\'') is not char StartQuote) {
            return ReadQuotelessString();
        }

        // Count multiple start quotes
        int StartQuoteCounter = 1;
        while (ReadOne(StartQuote)) {
            StartQuoteCounter++;
        }

        // Empty string
        if (StartQuoteCounter == 2) {
            return new JsonhToken(JsonTokenType.String, "");
        }

        // Count multiple end quotes
        int EndQuoteCounter = 0;

        // Read string
        ValueStringBuilder StringBuilder = new(stackalloc char[64]);
        using ValueStringBuilder ReadOnlyStringBuilder = StringBuilder; // Can't pass using variables by-ref

        while (true) {
            if (Read() is not char Next) {
                return new Error("Expected end of string, got end of input");
            }

            // Partial end quote was actually part of string
            if (Next != StartQuote) {
                for (; EndQuoteCounter > 0; EndQuoteCounter--) {
                    StringBuilder.Append(StartQuote);
                }
            }

            // End quote
            if (Next == StartQuote) {
                EndQuoteCounter++;
                if (EndQuoteCounter == StartQuoteCounter) {
                    break;
                }
            }
            // Escape sequence
            else if (Next is '\\') {
                if (ReadEscapeSequence(ref StringBuilder).TryGetError(out Error EscapeSequenceError)) {
                    return EscapeSequenceError;
                }
            }
            // Literal character
            else {
                StringBuilder.Append(Next);
            }
        }

        // Condition: skip remaining steps unless started with multiple quotes
        if (StartQuoteCounter > 1) {
            // Pass 1: count leading whitespace -> newline
            bool HasLeadingWhitespaceNewline = false;
            int LeadingWhitespaceNewlineCounter = 0;
            for (int Index = 0; Index < StringBuilder.Length; Index++) {
                char Next = StringBuilder[Index];

                // Newline
                if (NewlineChars.Contains(Next)) {
                    // Join CR LF
                    if (Next is '\r' && Index + 1 < StringBuilder.Length && StringBuilder[Index + 1] is '\n') {
                        Index++;
                    }

                    HasLeadingWhitespaceNewline = true;
                    LeadingWhitespaceNewlineCounter = Index + 1;
                    break;
                }
                // Non-whitespace
                else if (!char.IsWhiteSpace(Next)) {
                    break;
                }
            }

            // Condition: skip remaining steps if pass 1 failed
            if (HasLeadingWhitespaceNewline) {
                // Pass 2: count trailing newline -> whitespace
                bool HasTrailingNewlineWhitespace = false;
                int LastNewlineIndex = 0;
                int TrailingWhitespaceCounter = 0;
                for (int Index = 0; Index < StringBuilder.Length; Index++) {
                    char Next = StringBuilder[Index];

                    // Newline
                    if (NewlineChars.Contains(Next)) {
                        HasTrailingNewlineWhitespace = true;
                        LastNewlineIndex = Index;
                        TrailingWhitespaceCounter = 0;

                        // Join CR LF
                        if (Next is '\r' && Index + 1 < StringBuilder.Length && StringBuilder[Index + 1] is '\n') {
                            Index++;
                        }
                    }
                    // Whitespace
                    else if (char.IsWhiteSpace(Next)) {
                        TrailingWhitespaceCounter++;
                    }
                    // Non-whitespace
                    else {
                        HasTrailingNewlineWhitespace = false;
                        TrailingWhitespaceCounter = 0;
                    }
                }

                // Condition: skip remaining steps if pass 2 failed
                if (HasTrailingNewlineWhitespace) {
                    // Pass 3: strip trailing newline -> whitespace
                    StringBuilder.Remove(LastNewlineIndex, StringBuilder.Length - LastNewlineIndex);

                    // Pass 4: strip leading whitespace -> newline
                    StringBuilder.Remove(0, LeadingWhitespaceNewlineCounter);

                    // Condition: skip remaining steps if no trailing whitespace
                    if (TrailingWhitespaceCounter > 0) {
                        // Pass 5: strip line-leading whitespace
                        bool IsLineLeadingWhitespace = true;
                        int LineLeadingWhitespaceCounter = 0;
                        for (int Index = 0; Index < StringBuilder.Length; Index++) {
                            char Next = StringBuilder[Index];

                            // Newline
                            if (NewlineChars.Contains(Next)) {
                                IsLineLeadingWhitespace = true;
                                LineLeadingWhitespaceCounter = 0;
                            }
                            // Whitespace
                            else if (char.IsWhiteSpace(Next)) {
                                if (IsLineLeadingWhitespace) {
                                    // Increment line-leading whitespace
                                    LineLeadingWhitespaceCounter++;

                                    // Maximum line-leading whitespace reached
                                    if (LineLeadingWhitespaceCounter == TrailingWhitespaceCounter) {
                                        // Remove line-leading whitespace
                                        StringBuilder.Remove(Index + 1 - LineLeadingWhitespaceCounter, LineLeadingWhitespaceCounter);
                                        Index -= LineLeadingWhitespaceCounter;
                                        // Exit line-leading whitespace
                                        IsLineLeadingWhitespace = false;
                                    }
                                }
                            }
                            // Non-whitespace
                            else {
                                if (IsLineLeadingWhitespace) {
                                    // Remove partial line-leading whitespace
                                    StringBuilder.Remove(Index - LineLeadingWhitespaceCounter, LineLeadingWhitespaceCounter);
                                    Index -= LineLeadingWhitespaceCounter;
                                    // Exit line-leading whitespace
                                    IsLineLeadingWhitespace = false;
                                }
                            }
                        }
                    }
                }
            }
        }

        // End of string
        return new JsonhToken(JsonTokenType.String, StringBuilder.ToString());
    }
    private Result<JsonhToken> ReadQuotelessString(ReadOnlySpan<char> InitialChars = default) {
        bool IsNamedLiteralPossible = true;

        // Read quoteless string
        ValueStringBuilder StringBuilder = new(stackalloc char[64]);
        using ValueStringBuilder ReadOnlyStringBuilder = StringBuilder; // Can't pass using variables by-ref
        StringBuilder.Append(InitialChars);

        while (true) {
            // Peek char
            if (Peek() is not char Next) {
                break;
            }

            // Escape sequence
            if (Next is '\\') {
                Read();
                if (ReadEscapeSequence(ref StringBuilder).TryGetError(out Error EscapeSequenceError)) {
                    return EscapeSequenceError;
                }
                IsNamedLiteralPossible = false;
            }
            // End on reserved character
            else if (ReservedChars.Contains(Next)) {
                break;
            }
            // End on newline
            else if (NewlineChars.Contains(Next)) {
                break;
            }
            // Literal character
            else {
                Read();
                StringBuilder.Append(Next);
            }
        }

        // Ensure not empty
        if (StringBuilder.AsSpan().IsEmpty) {
            return new Error("Empty quoteless string");
        }

        // Trim whitespace
        StringBuilder.Trim();

        // Match named literal
        if (IsNamedLiteralPossible) {
            if (StringBuilder.Equals("null")) {
                return new JsonhToken(JsonTokenType.Null);
            }
            else if (StringBuilder.Equals("true")) {
                return new JsonhToken(JsonTokenType.True);
            }
            else if (StringBuilder.Equals("false")) {
                return new JsonhToken(JsonTokenType.False);
            }
        }

        // End of quoteless string
        return new JsonhToken(JsonTokenType.String, StringBuilder.ToString());
    }
    private bool DetectQuotelessString(out ReadOnlySpan<char> WhitespaceChars) {
        // Read whitespace
        using ValueStringBuilder WhitespaceBuilder = new();

        while (true) {
            // Read char
            if (Peek() is not char Next) {
                break;
            }

            // Newline
            if (NewlineChars.Contains(Next)) {
                // Quoteless strings cannot contain unescaped newlines
                WhitespaceChars = WhitespaceBuilder.AsSpan();
                return false;
            }

            // End of whitespace
            if (!char.IsWhiteSpace(Next)) {
                break;
            }

            // Whitespace
            WhitespaceBuilder.Append(Next);
            Read();
        }

        // End of whitespace
        WhitespaceChars = WhitespaceBuilder.AsSpan();

        // Found quoteless string if found backslash or non-reserved char
        return Peek() is char NextChar && (NextChar is '\\' || !ReservedChars.Contains(NextChar));
    }
    private Result<JsonhToken> ReadNumber(out ReadOnlySpan<char> PartialCharsRead) {
        // Read number
        ValueStringBuilder NumberBuilder = new(stackalloc char[64]);
        using ValueStringBuilder ReadOnlyNumberBuilder = NumberBuilder; // Can't pass using variables by-ref

        // Read sign
        if (ReadAny('-', '+') is char Sign) {
            NumberBuilder.Append(Sign);
        }

        // Read base
        string BaseDigits = "0123456789";
        bool HasBaseSpecifier = false;
        if (ReadOne('0')) {
            NumberBuilder.Append('0');

            if (ReadAny('x', 'X') is char HexBaseChar) {
                NumberBuilder.Append(HexBaseChar);
                BaseDigits = "0123456789abcdef";
                HasBaseSpecifier = true;
            }
            else if (ReadAny('b', 'B') is char BinaryBaseChar) {
                NumberBuilder.Append(BinaryBaseChar);
                BaseDigits = "01";
                HasBaseSpecifier = true;
            }
            else if (ReadAny('o', 'O') is char OctalBaseChar) {
                NumberBuilder.Append(OctalBaseChar);
                BaseDigits = "01234567";
                HasBaseSpecifier = true;
            }
        }

        // Read main number
        if (ReadNumberNoExponent(ref NumberBuilder, BaseDigits, HasBaseSpecifier).TryGetError(out Error MainError)) {
            PartialCharsRead = NumberBuilder.ToString();
            return MainError;
        }

        // Hexadecimal exponent
        if (NumberBuilder[^1] is 'e' or 'E') {
            // Read sign
            if (ReadAny('+', '-') is char ExponentSign) {
                NumberBuilder.Append(ExponentSign);

                // Read exponent number
                if (ReadNumberNoExponent(ref NumberBuilder, BaseDigits, HasBaseSpecifier).TryGetError(out Error ExponentError)) {
                    PartialCharsRead = NumberBuilder.ToString();
                    return ExponentError;
                }
            }
        }
        // Exponent
        else if (ReadAny('e', 'E') is char ExponentChar) {
            NumberBuilder.Append(ExponentChar);

            // Read sign
            if (ReadAny('-', '+') is char ExponentSign) {
                NumberBuilder.Append(ExponentSign);
            }

            // Read exponent number
            if (ReadNumberNoExponent(ref NumberBuilder, BaseDigits, HasBaseSpecifier).TryGetError(out Error ExponentError)) {
                PartialCharsRead = NumberBuilder.ToString();
                return ExponentError;
            }
        }

        // End of number
        PartialCharsRead = default;
        return new JsonhToken(JsonTokenType.Number, NumberBuilder.ToString());
    }
    private Result ReadNumberNoExponent(scoped ref ValueStringBuilder NumberBuilder, ReadOnlySpan<char> BaseDigits, bool HasBaseSpecifier) {
        // Leading underscore
        if (!HasBaseSpecifier && Peek() is '_') {
            return new Error("Leading `_` in number");
        }

        bool IsFraction = false;

        while (true) {
            // Peek char
            if (Peek() is not char Next) {
                break;
            }

            // Digit
            if (BaseDigits.Contains(char.ToLowerInvariant(Next))) {
                Read();
                NumberBuilder.Append(Next);
            }
            // Dot
            else if (Next is '.') {
                Read();
                NumberBuilder.Append(Next);

                // Duplicate dot
                if (IsFraction) {
                    return new Error("Duplicate `.` in number");
                }
                IsFraction = true;
            }
            // Underscore
            else if (Next is '_') {
                Read();
                NumberBuilder.Append(Next);
            }
            // Other
            else {
                break;
            }
        }

        // Ensure not empty
        if (NumberBuilder.AsSpan().IsEmpty) {
            return new Error("Empty number");
        }

        // Ensure at least one digit
        if (!NumberBuilder.AsSpan().ContainsAnyExcept(['.', '-', '+', '_'])) {
            return new Error("Number must have at least one digit");
        }

        // Trailing underscore
        if (NumberBuilder.AsSpan().EndsWith("_")) {
            return new Error("Trailing `_` in number");
        }

        // End of number
        return Result.Success;
    }
    private Result<JsonhToken> ReadNumberOrQuotelessString() {
        // Read number
        if (ReadNumber(out ReadOnlySpan<char> PartialCharsRead).TryGetValue(out JsonhToken Number)) {
            // Try read quoteless string starting with number
            if (DetectQuotelessString(out ReadOnlySpan<char> WhitespaceChars)) {
                return ReadQuotelessString(string.Concat(Number.Value, WhitespaceChars));
            }
            // Otherwise, accept number
            else {
                return Number;
            }
        }
        // Read quoteless string starting with malformed number
        else {
            return ReadQuotelessString(PartialCharsRead);
        }
    }
    private Result<JsonhToken> ReadPrimitiveElement() {
        // Peek char
        if (Peek() is not char Next) {
            return new Error("Expected primitive element, got end of input");
        }

        // Number
        if (Next is (>= '0' and <= '9') or ('-' or '+') or '.') {
            return ReadNumberOrQuotelessString();
        }
        // String
        else if (Next is '"' or '\'') {
            return ReadString();
        }
        // Quoteless string (or named literal)
        else {
            return ReadQuotelessString();
        }
    }
    private IEnumerable<Result<JsonhToken>> ReadCommentsAndWhitespace() {
        while (true) {
            // Whitespace
            ReadWhitespace();

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
                return new Error("Unexpected `/`");
            }
        }
        else {
            return new Error("Unexpected character");
        }

        // Read comment
        using ValueStringBuilder CommentBuilder = new(stackalloc char[64]);

        while (true) {
            // Read char
            char? Next = Read();

            if (BlockComment) {
                // Error
                if (Next is null) {
                    return new Error("Expected end of block comment, got end of input");
                }
                // End of block comment
                if (Next is '*' && ReadOne('/')) {
                    return new JsonhToken(JsonTokenType.Comment, CommentBuilder.ToString());
                }
            }
            else {
                // End of line comment
                if (Next is null || NewlineChars.Contains(Next.Value)) {
                    return new JsonhToken(JsonTokenType.Comment, CommentBuilder.ToString());
                }
            }

            // Comment char
            CommentBuilder.Append(Next.Value);
        }
    }
    private void ReadWhitespace() {
        while (true) {
            // Peek char
            if (Peek() is not char Next) {
                return;
            }

            // Whitespace
            if (char.IsWhiteSpace(Next)) {
                Read();
            }
            // End of whitespace
            else {
                return;
            }
        }
    }
    private Result<uint> ReadHexSequence(int Length) {
        Span<char> HexChars = stackalloc char[Length];

        for (int Index = 0; Index < Length; Index++) {
            char? Next = Read();

            // Hex digit
            if (Next is (>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f')) {
                HexChars[Index] = Next.Value;
            }
            // Unexpected char
            else {
                return new Error("Incorrect number of hexadecimal digits in unicode escape sequence");
            }
        }

        // Parse unicode character from hex digits
        return uint.Parse(HexChars, NumberStyles.AllowHexSpecifier);
    }
    private Result ReadEscapeSequence(scoped ref ValueStringBuilder StringBuilder) {
        if (Read() is not char EscapeChar) {
            return new Error("Expected escape sequence, got end of input");
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
        return Result.Success;
    }
    private char? Peek() {
        int NextAsInt = TextReader.Peek();
        if (NextAsInt < 0) {
            return null;
        }
        return (char)NextAsInt;
    }
    private char? Read() {
        int NextAsInt = TextReader.Read();
        if (NextAsInt < 0) {
            return null;
        }
        CharCounter++;
        return (char)NextAsInt;
    }
    private bool ReadOne(char Option) {
        if (Peek() == Option) {
            Read();
            return true;
        }
        return false;
    }
    private char? ReadAny(params ReadOnlySpan<char> Options) {
        // Peek char
        if (Peek() is not char Next) {
            return null;
        }
        // Match option
        if (!Options.Contains(Next)) {
            return null;
        }
        // Option matched
        Read();
        return Next;
    }
}
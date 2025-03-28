﻿using System.Buffers;
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
    /// Characters that cannot be used in quoteless strings.
    /// </summary>
    private static readonly SearchValues<char> ReservedChars = SearchValues.Create(['\\', ',', ':', '[', ']', '{', '}', '/', '#', '"', '\'']);
    /// <summary>
    /// Characters that serve as newlines in strings.
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
                try {
                    BigReal Result = JsonhNumberParser.Parse(Token.Value);
                    JsonNode Node = JsonNode.Parse(Result.ToString())!;
                    if (SubmitNode(Node)) {
                        return Node;
                    }
                }
                catch (Exception Ex) {
                    return Ex;
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
        yield return new JsonhToken(this, JsonTokenType.StartObject);

        while (true) {
            // Comments & whitespace
            foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }

            if (Peek() is not char Char) {
                // End of incomplete object
                if (Options.IncompleteInputs) {
                    yield return new JsonhToken(this, JsonTokenType.EndObject);
                    yield break;
                }
                // Missing closing brace
                yield return new Error("Expected `}` to end object, got end of input");
                yield break;
            }

            // Closing brace
            if (Char is '}') {
                // End of object
                Read();
                yield return new JsonhToken(this, JsonTokenType.EndObject);
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
        yield return new JsonhToken(this, JsonTokenType.StartObject);

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
                yield return new JsonhToken(this, JsonTokenType.EndObject);
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
    private IEnumerable<Result<JsonhToken>> ReadArray() {
        // Opening bracket
        if (!ReadOne('[')) {
            yield return new Error("Expected '[' to start array");
            yield break;
        }
        // Start of array
        yield return new JsonhToken(this, JsonTokenType.StartArray);

        while (true) {
            // Comments & whitespace
            foreach (Result<JsonhToken> Token in ReadCommentsAndWhitespace()) {
                if (Token.IsError) {
                    yield return Token.Error;
                    yield break;
                }
                yield return Token;
            }

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

            // Closing bracket
            if (Char is ']') {
                // End of array
                Read();
                yield return new JsonhToken(this, JsonTokenType.EndArray);
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
        yield return new JsonhToken(this, JsonTokenType.PropertyName, String);
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
            return new JsonhToken(this, JsonTokenType.String, "");
        }

        // Count multiple end quotes
        int EndQuoteCounter = 0;

        // Read string
        ValueStringBuilder StringBuilder = new(stackalloc char[64]);
        using ValueStringBuilder ReadOnlyStringBuilder = StringBuilder; // Can't pass using variables by-ref

        while (true) {
            if (Read() is not char Char) {
                return new Error("Expected end of string, got end of input");
            }

            // Partial end quote was actually part of string
            if (Char != StartQuote) {
                for (; EndQuoteCounter > 0; EndQuoteCounter--) {
                    StringBuilder.Append(StartQuote);
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
                if (ReadEscapeSequence(ref StringBuilder).TryGetError(out Error EscapeSequenceError)) {
                    return EscapeSequenceError;
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
    private Result<JsonhToken> ReadNumber(out ReadOnlySpan<char> PartialCharsRead) {
        // Read number
        ValueStringBuilder StringBuilder = new(stackalloc char[64]);
        using ValueStringBuilder ReadOnlyStringBuilder = StringBuilder; // Can't pass using variables by-ref

        // Read base
        string BaseDigits = "0123456789";
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

        // Read main number
        if (ReadNumberNoExponent(ref StringBuilder, BaseDigits).TryGetError(out Error NumberCoreError)) {
            PartialCharsRead = StringBuilder.ToString();
            return NumberCoreError;
        }

        // Exponent
        if (ReadAny('e', 'E') is char ExponentChar) {
            StringBuilder.Append(ExponentChar);

            // Read exponent number
            if (ReadNumberNoExponent(ref StringBuilder, BaseDigits).TryGetError(out Error ExponentCoreError)) {
                PartialCharsRead = StringBuilder.ToString();
                return ExponentCoreError;
            }
        }

        // End of number
        PartialCharsRead = default;
        return new JsonhToken(this, JsonTokenType.Number, StringBuilder.ToString());
    }
    private Result ReadNumberNoExponent(scoped ref ValueStringBuilder StringBuilder, ReadOnlySpan<char> BaseDigits) {
        // Read sign
        ReadAny('-', '+');

        // Leading underscore
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
                Read();
                StringBuilder.Append(Char);
            }
            // Decimal point
            else if (Char is '.') {
                Read();
                StringBuilder.Append(Char);

                // Duplicate decimal point
                if (IsFraction) {
                    return new Error("Duplicate `.` in number");
                }
                IsFraction = true;
            }
            // Underscore
            else if (Char is '_') {
                Read();
                StringBuilder.Append(Char);
            }
            // Other
            else {
                break;
            }
        }

        // Trailing underscore
        if (StringBuilder.AsSpan().EndsWith("_")) {
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
    private Result<JsonhToken> ReadQuotelessString(ReadOnlySpan<char> InitialChars = default) {
        bool IsNamedLiteralPossible = true;

        // Read quoteless string
        ValueStringBuilder StringBuilder = new(stackalloc char[64]);
        using ValueStringBuilder ReadOnlyStringBuilder = StringBuilder; // Can't pass using variables by-ref
        StringBuilder.Append(InitialChars);

        while (true) {
            // Read char
            if (Peek() is not char Char) {
                break;
            }

            // Read escape sequence
            if (Char is '\\') {
                Read();
                if (ReadEscapeSequence(ref StringBuilder).TryGetError(out Error EscapeSequenceError)) {
                    return EscapeSequenceError;
                }
                IsNamedLiteralPossible = false;
            }
            // End on reserved character
            else if (ReservedChars.Contains(Char)) {
                break;
            }
            // End on newline
            else if (NewlineChars.Contains(Char)) {
                break;
            }
            // Append string char
            else {
                Read();
                StringBuilder.Append(Char);
            }
        }

        // Ensure not empty
        if (StringBuilder.AsSpan().IsEmpty) {
            return new Error("Empty quoteless string");
        }

        // Trim trailing whitespace
        StringBuilder.TrimEnd();

        // Match named literal
        if (IsNamedLiteralPossible) {
            if (StringBuilder.Equals("null")) {
                return new JsonhToken(this, JsonTokenType.Null, StringBuilder.ToString());
            }
            else if (StringBuilder.Equals("true")) {
                return new JsonhToken(this, JsonTokenType.True, StringBuilder.ToString());
            }
            else if (StringBuilder.Equals("false")) {
                return new JsonhToken(this, JsonTokenType.False, StringBuilder.ToString());
            }
        }

        // End of quoteless string
        return new JsonhToken(this, JsonTokenType.String, StringBuilder.ToString());
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
                return new Error("Unexpected '/'");
            }
        }
        else {
            return new Error("Unexpected character");
        }

        // Read comment
        using ValueStringBuilder StringBuilder = new(stackalloc char[64]);

        while (true) {
            // Read char
            char? Char = Read();

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
    private void ReadWhitespace() {
        while (true) {
            // Peek char
            if (Peek() is not char Char) {
                return;
            }

            // Whitespace
            if (char.IsWhiteSpace(Char)) {
                Read();
            }
            // End of whitespace
            else {
                return;
            }
        }
    }
    private Result<JsonhToken> ReadPrimitiveElement() {
        // Peek char
        if (Peek() is not char Char) {
            return new Error();
        }

        // Number
        if (Char is (>= '0' and <= '9') or ('-' or '+') or '.') {
            return ReadNumberOrQuotelessString();
        }
        // String
        else if (Char is '"' or '\'') {
            return ReadString();
        }
        // Quoteless string (or named literal)
        else {
            return ReadQuotelessString();
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
    private bool DetectQuotelessString(out ReadOnlySpan<char> WhitespaceChars) {
        // Read whitespace
        using ValueStringBuilder StringBuilder = new();

        while (true) {
            // Read char
            if (Peek() is not char Char) {
                break;
            }

            // Newline
            if (NewlineChars.Contains(Char)) {
                // Quoteless strings cannot contain unescaped newlines
                WhitespaceChars = StringBuilder.AsSpan();
                return false;
            }

            // End of whitespace
            if (!char.IsWhiteSpace(Char)) {
                break;
            }

            // Whitespace
            StringBuilder.Append(Char);
            Read();
        }

        // End of whitespace
        WhitespaceChars = StringBuilder.AsSpan();

        // Found quoteless string if found backslash or non-reserved char
        return Peek() is char NextChar && (NextChar is '\\' || !ReservedChars.Contains(NextChar));
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
        CharCounter++;
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
        if (!Options.Contains(Char)) {
            return null;
        }
        // Option matched
        Read();
        return Char;
    }
}

/// <summary>
/// Options for a <see cref="JsonhReader"/>.
/// </summary>
public record struct JsonhReaderOptions() {
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

/// <summary>
/// A single JSONH token with a <see cref="JsonTokenType"/>.
/// </summary>
public readonly record struct JsonhToken(JsonhReader Reader, JsonTokenType JsonType, string Value = "") {
    /// <summary>
    /// The <see cref="JsonhReader"/> that read the token.
    /// </summary>
    public JsonhReader Reader { get; } = Reader;
    /// <summary>
    /// The type of the token.
    /// </summary>
    public JsonTokenType JsonType { get; } = JsonType;
    /// <summary>
    /// The value of the token, or an empty string.
    /// </summary>
    public string Value { get; } = Value;
}
using System.Text.Json;
using ResultZero;

namespace JsonhCs.Tests;

public class ReadTests {
    [Fact]
    public void BasicObjectTest() {
        string Jsonh = """
            {
              "a": "b"
            }
            """;
        using JsonhReader Reader = new(Jsonh);
        Result<JsonhToken>[] Tokens = [.. Reader.ReadElement()];

        Tokens.ShouldAllBe(Token => Token.IsValue);
        Tokens[0].Value.JsonType.ShouldBe(JsonTokenType.StartObject);
        Tokens[1].Value.JsonType.ShouldBe(JsonTokenType.PropertyName);
        Tokens[1].Value.Value.ShouldBe("a");
        Tokens[2].Value.JsonType.ShouldBe(JsonTokenType.String);
        Tokens[2].Value.Value.ShouldBe("b");
        Tokens[3].Value.JsonType.ShouldBe(JsonTokenType.EndObject);
    }
    [Fact]
    public void NestableBlockCommentTest() {
        string Jsonh = """
            /* */
            /=* *=/
            /==*/=**=/*==/
            /=*/==**==/*=/
            0
            """;
        using JsonhReader Reader = new(Jsonh);
        Result<JsonhToken>[] Tokens = [.. Reader.ReadElement()];

        Tokens.ShouldAllBe(Token => Token.IsValue);
        Tokens[0].Value.JsonType.ShouldBe(JsonTokenType.Comment);
        Tokens[0].Value.Value.ShouldBe(" ");
        Tokens[1].Value.JsonType.ShouldBe(JsonTokenType.Comment);
        Tokens[1].Value.Value.ShouldBe(" ");
        Tokens[2].Value.JsonType.ShouldBe(JsonTokenType.Comment);
        Tokens[2].Value.Value.ShouldBe("/=**=/");
        Tokens[3].Value.JsonType.ShouldBe(JsonTokenType.Comment);
        Tokens[3].Value.Value.ShouldBe("/==**==/");
        Tokens[4].Value.JsonType.ShouldBe(JsonTokenType.Number);
        Tokens[4].Value.Value.ShouldBe("0");

        using JsonhReader Reader2 = new(Jsonh, new JsonhReaderOptions() {
            Version = JsonhVersion.V1,
        });
        Result<JsonhToken>[] Tokens2 = [.. Reader2.ReadElement()];

        Tokens2[1].IsError.ShouldBeTrue();
    }
    [Fact]
    public void FindPropertyValueTest() {
        string Jsonh = """
            // Original position
            {
              "a": "1",
              "b": {
                "c": "2"
              },
              "c":/* Final position */ "3"
            }
            """;
        using JsonhReader Reader = new(Jsonh);

        Reader.FindPropertyValue("c").ShouldBeTrue();
        Reader.ParseElement<string>().Value.ShouldBe("3");
    }
    [Fact]
    public void InsertPropertyTest() {
        string Jsonh = """
            {
              a: { b: c },
              d: {
                e: f
              },/* hello */
            }
            """;

        string JsonhInserted1 = JsonhReader.InsertProperty(Jsonh, "g", "g: h", "h", "  ").Value;
        JsonhInserted1.ShouldBe("""
            {
              a: { b: c },
              d: {
                e: f
              },/* hello */
              g: h,
            }
            """);
        string JsonhInserted2 = JsonhReader.InsertProperty(JsonhInserted1, "d", "d: e", "e", "  ").Value;
        JsonhInserted2.ShouldBe("""
            {
              a: { b: c },
              d: e,/* hello */
              g: h,
            }
            """);

        string Jsonh2 = """
            {/* hi */}
            """;

        string Jsonh2Inserted1 = JsonhReader.InsertProperty(Jsonh2, "a", "a: b", "b", "  ").Value;
        Jsonh2Inserted1.ShouldBe("""
            {/* hi */
              a: b,
            }
            """);
        string Jsonh2Inserted2 = JsonhReader.InsertProperty(Jsonh2Inserted1, "c", "c: d", "d", "  ").Value;
        Jsonh2Inserted2.ShouldBe("""
            {/* hi */
              a: b,
              c: d,
            }
            """);
    }
}
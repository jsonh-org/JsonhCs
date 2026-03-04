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
    public void ParseJsonTest() {
        string Jsonh = """
            {
              // Hello /* test */ world
              a: 'b'
              "c": '''私'''
              x: [a,b,c]
              y: {}
              z: 0.05e1
            }
            """;

        using JsonhReader Reader = new(Jsonh);
        Reader.ParseJson().Value.ShouldBe("""
            {"a":"b","c":"私","x":["a","b","c"],"y":{},"z":0.5}
            """);

        using JsonhReader Reader2 = new(Jsonh);
        Reader2.ParseJson(IncludeComments: true).Value.ShouldBe("""
            {/* Hello / * test * / world*/"a":"b","c":"私","x":["a","b","c"],"y":{},"z":0.5}
            """);

        using JsonhReader Reader3 = new(Jsonh);
        Reader3.ParseJson(Indent: "  ").Value.ShouldBe("""
            {
              "a": "b",
              "c": "私",
              "x": [
                "a",
                "b",
                "c"
              ],
              "y": {},
              "z": 0.5
            }
            """);

        using JsonhReader Reader4 = new(Jsonh);
        Reader4.ParseJson(IncludeComments: true, Indent: "  ").Value.ShouldBe("""
            {
              /* Hello / * test * / world*/
              "a": "b",
              "c": "私",
              "x": [
                "a",
                "b",
                "c"
              ],
              "y": {},
              "z": 0.5
            }
            """);
    }
}
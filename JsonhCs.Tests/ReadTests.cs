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

        Result<JsonhToken>[] Tokens = [.. new JsonhReader(Jsonh).ReadElement()];

        Tokens.ShouldAllBe(Token => Token.IsValue);
        Tokens[0].Value.JsonType.ShouldBe(JsonTokenType.StartObject);
        Tokens[1].Value.JsonType.ShouldBe(JsonTokenType.PropertyName);
        Tokens[1].Value.Value.ShouldBe("a");
        Tokens[2].Value.JsonType.ShouldBe(JsonTokenType.String);
        Tokens[2].Value.Value.ShouldBe("b");
        Tokens[3].Value.JsonType.ShouldBe(JsonTokenType.EndObject);
    }
}
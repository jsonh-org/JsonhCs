using ResultZero;
using System.Text.Json;

namespace JsonhCs.Tests;

public class ReadTests {
    [Fact]
    public void BasicObjectTest() {
        string Jsonh = """
            {
              "a": "b"
            }
            """;

        Result<JsonhToken>[] Tokens = new JsonhReader(Jsonh).ReadElement().ToArray();

        Assert.All(Tokens, Token => Assert.True(Token.IsValue));
        Assert.Equal(JsonTokenType.StartObject, Tokens[0].Value.JsonType);
        Assert.Equal(JsonTokenType.PropertyName, Tokens[1].Value.JsonType);
        Assert.Equal("a", Tokens[1].Value.Value);
        Assert.Equal(JsonTokenType.String, Tokens[2].Value.JsonType);
        Assert.Equal("b", Tokens[2].Value.Value);
        Assert.Equal(JsonTokenType.EndObject, Tokens[3].Value.JsonType);
    }
}
using ResultZero;
using System.Text.Json;

namespace JsonhCs.Tests;

public class ParseTests {
    [Fact]
    public void EscapeSequenceTest() {
        string Jsonh = """
            "\U0001F47D and \uD83D\uDC7D"
            """;
        string Element = new JsonhReader(Jsonh).ParseElement<string>().Value!;

        Assert.Equal("👽 and 👽", Element);
    }
}
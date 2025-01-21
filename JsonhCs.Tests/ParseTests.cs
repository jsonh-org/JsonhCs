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
    [Fact]
    public void MultiQuotedStringTest() {
        string Jsonh = """""  
                """"
                  Hello! Here's a quote: ". Now a double quote: "". And a triple quote! """. Escape: \\\U0001F47D.
                 """"
            """"";
        string Element = new JsonhReader(Jsonh).ParseElement<string>().Value!;

        Assert.Equal(" Hello! Here's a quote: \". Now a double quote: \"\". And a triple quote! \"\"\". Escape: \\👽.", Element);
    }
}
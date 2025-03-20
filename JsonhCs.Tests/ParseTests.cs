using System.Text.Json;
using ExtendedNumerics;

namespace JsonhCs.Tests;

public class ParseTests {
    [Fact]
    public void EscapeSequenceTest() {
        string Jsonh = """
            "\U0001F47D and \uD83D\uDC7D"
            """;
        string Element = JsonhReader.ParseElement<string>(Jsonh).Value!;

        Element.ShouldBe("👽 and 👽");
    }
    [Fact]
    public void QuotelessEscapeSequenceTest() {
        string Jsonh = """
            \U0001F47D and \uD83D\uDC7D
            """;
        string Element = JsonhReader.ParseElement<string>(Jsonh).Value!;

        Element.ShouldBe("👽 and 👽");
    }
    [Fact]
    public void MultiQuotedStringTest() {
        string Jsonh = """""  
                """"
                  Hello! Here's a quote: ". Now a double quote: "". And a triple quote! """. Escape: \\\U0001F47D.
                 """"
            """"";
        string Element = JsonhReader.ParseElement<string>(Jsonh).Value!;

        Element.ShouldBe(" Hello! Here's a quote: \". Now a double quote: \"\". And a triple quote! \"\"\". Escape: \\👽.");
    }
    [Fact]
    public void ArrayTest() {
        string Jsonh = """
            [
                1, 2,
                3
                4 5,6
            ]
            """;
        JsonElement Element = JsonhReader.ParseElement(Jsonh).Value;

        Element.GetArrayLength().ShouldBe(5);
        Element[0].Deserialize<int>(JsonhReader.MiniJson).ShouldBe(1);
        Element[1].Deserialize<int>(JsonhReader.MiniJson).ShouldBe(2);
        Element[2].Deserialize<int>(JsonhReader.MiniJson).ShouldBe(3);
        Element[3].Deserialize<string>(JsonhReader.MiniJson).ShouldBe("4 5");
        Element[4].Deserialize<int>(JsonhReader.MiniJson).ShouldBe(6);
    }
    [Fact]
    public void NumberParserTest() {
        BigReal.Truncate(JsonhNumberParser.Parse("1.2e3.4")).ShouldBe(3014);
    }
    [Fact]
    public void BracelessObjectTest() {
        string Jsonh = """""  
            a: b
            c: d
            """"";
        JsonElement Element = JsonhReader.ParseElement(Jsonh).Value;

        Element.GetPropertyCount().ShouldBe(2);
        Element.GetProperty("a").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("b");
        Element.GetProperty("c").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("d");
    }
    [Fact]
    public void CommentTest() {
        string Jsonh = """""  
            [
                1 # hash comment
                2 // line comment
                3 /* block comment */,4
            ]
            """"";
        JsonElement Element = JsonhReader.ParseElement(Jsonh).Value;

        Element.Deserialize<int[]>(JsonhReader.MiniJson).ShouldBe([1, 2, 3, 4]);
    }
}
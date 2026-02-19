using System.Text.Json;
using System.Text.Json.Nodes;
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
        BigReal.Truncate(JsonhNumberParser.Parse("1.2e3.4").Value).ShouldBe(3014);
    }
    [Fact]
    public void BracelessObjectTest() {
        string Jsonh = """
            a: b
            c: d
            """;
        JsonElement Element = JsonhReader.ParseElement(Jsonh).Value;

        Element.GetPropertyCount().ShouldBe(2);
        Element.GetProperty("a").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("b");
        Element.GetProperty("c").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("d");
    }
    [Fact]
    public void CommentTest() {
        string Jsonh = """
            [
                1 # hash comment
                2 // line comment
                3 /* block comment */,4
            ]
            """;
        int[] Element = JsonhReader.ParseElement<int[]>(Jsonh).Value!;

        Element.ShouldBe([1, 2, 3, 4]);
    }
    [Fact]
    public void VerbatimStringTest() {
        string Jsonh = """
            {
                a\\: b\\
                @c\\: @d\\
                @e\\: f\\
            }
            """;
        JsonElement Element = JsonhReader.ParseElement(Jsonh).Value;

        Element.GetPropertyCount().ShouldBe(3);
        Element.GetProperty("a\\").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("b\\");
        Element.GetProperty("c\\\\").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("d\\\\");
        Element.GetProperty("e\\\\").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("f\\");

        JsonElement Element2 = JsonhReader.ParseElement(Jsonh, new JsonhReaderOptions() {
            Version = JsonhVersion.V1,
        }).Value;
        Element2.GetPropertyCount().ShouldBe(3);
        Element2.GetProperty("a\\").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("b\\");
        Element2.GetProperty("@c\\").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("@d\\");
        Element2.GetProperty("@e\\").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("f\\");

        string Jsonh2 = """
            @"a\\": @'''b\\'''
            """;
        JsonElement Element3 = JsonhReader.ParseElement(Jsonh2).Value;

        Element3.GetPropertyCount().ShouldBe(1);
        Element3.GetProperty("a\\\\").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("b\\\\");
    }
    [Fact]
    public void ParseSingleElementTest() {
        string Jsonh = """
            1
            2
            """;
        int Element = JsonhReader.ParseElement<int>(Jsonh).Value!;

        Element.ShouldBe(1);

        JsonhReader.ParseElement<int>(Jsonh, new JsonhReaderOptions() {
            ParseSingleElement = true,
        }).IsError.ShouldBeTrue();

        string Jsonh2 = """
            1


            """;

        JsonhReader.ParseElement<int>(Jsonh2, new JsonhReaderOptions() {
            ParseSingleElement = true,
        }).IsError.ShouldBeFalse();
    }
    [Fact]
    public void BigNumbersTest() {
        string Jsonh = """
            [
                3.5,
                1e99999,
                999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999
            ]
            """;
        double[] Element = JsonhReader.ParseElement<double[]>(Jsonh).Value!;

        Element.Length.ShouldBe(3);
        Element[0].ShouldBe(3.5);
        Element[1].ShouldBe(double.PositiveInfinity);
        Element[2].ShouldBe(double.PositiveInfinity);

        JsonArray Element2 = JsonhReader.ParseElement<JsonArray>(Jsonh, new JsonhReaderOptions() {
            BigNumbers = true,
        }).Value!;
        Element2.Count.ShouldBe(3);
        Element2[0]!.ToString().ShouldBe("3.5");
        Element2[1]!.ToString().ShouldBe(BigReal.Parse("1e99999").ToString());
        Element2[2]!.ToString().ShouldBe(BigReal.Parse("999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999").ToString());
    }
    [Fact]
    public void MaxDepthTest() {
        string Jsonh = """
            {
              a: {
                b: {
                  c: ""
                }
                d: {
                }
              }
            }
            """;

        JsonhReader.ParseElement(Jsonh, new JsonhReaderOptions() {
            MaxDepth = 2,
        }).IsError.ShouldBeTrue();

        JsonhReader.ParseElement(Jsonh, new JsonhReaderOptions() {
            MaxDepth = 3,
        }).IsError.ShouldBeFalse();
    }
}
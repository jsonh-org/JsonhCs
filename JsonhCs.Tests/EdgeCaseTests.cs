using System.Text.Json;

namespace JsonhCs.Tests;

public class EdgeCaseTests {
    [Fact]
    public void QuotelessStringStartingWithKeywordTest() {
        string Jsonh = """
            [nulla, null b, null]
            """;
        string?[] Element = JsonhReader.ParseElement<string?[]>(Jsonh).Value!;

        Element.ShouldBe(["nulla", "null b", null]);
    }
    [Fact]
    public void BracelessObjectWithInvalidValueTest() {
        string Jsonh = """
            a: {
            """;

        JsonhReader.ParseElement(Jsonh).IsError.ShouldBeTrue();
    }
    [Fact]
    public void NestedBracelessObjectTest() {
        string Jsonh = """
            [
                a: b
                c: d
            ]
            """;

        JsonhReader.ParseElement<string[]>(Jsonh).IsError.ShouldBeTrue();
    }
    [Fact]
    public void QuotelessStringsLeadingTrailingWhitespaceTest() {
        string Jsonh = """
            [
              a b  , 
            ]
            """;

        JsonhReader.ParseElement<string[]>(Jsonh).Value.ShouldBe(["a b"]);
    }
    [Fact]
    public void SpaceInQuotelessPropertyNameTest() {
        string Jsonh = """
            {
                a b: c d
            }
            """;
        JsonElement Element = JsonhReader.ParseElement(Jsonh).Value;

        Element.GetPropertyCount().ShouldBe(1);
        Element.GetProperty("a b").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("c d");
    }
    [Fact]
    public void QuotelessStringsEscapeTest() {
        string Jsonh = """
            a: \"5
            b: \\z
            c: 5 \\
            """;
        JsonElement Element = JsonhReader.ParseElement(Jsonh).Value;

        Element.GetPropertyCount().ShouldBe(3);
        Element.GetProperty("a").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("\"5");
        Element.GetProperty("b").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("\\z");
        Element.GetProperty("c").Deserialize<string>(JsonhReader.MiniJson).ShouldBe("5 \\");
    }
    [Fact]
    public void MultiQuotedStringsNoLastNewlineWhitespaceTest() {
        string Jsonh = """"
            """
              hello world  """
            """";

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<string>(JsonhReader.MiniJson).ShouldBe("\n  hello world  ");
    }
    [Fact]
    public void MultiQuotedStringsNoFirstWhitespaceNewlineTest() {
        string Jsonh = """"
            """  hello world
              """
            """";

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<string>(JsonhReader.MiniJson).ShouldBe("  hello world\n  ");
    }
    [Fact]
    public void QuotelessStringsEscapedLeadingTrailingWhitespaceTest() {
        string Jsonh = """
            \nZ\ \r
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<string>(JsonhReader.MiniJson).ShouldBe("Z");
    }
    [Fact]
    public void HexNumberWithETest() {
        string Jsonh = """
            0x5e3
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<int>(JsonhReader.MiniJson).ShouldBe(0x5e3);

        string Jsonh2 = """
            0x5e+3
            """;

        JsonhReader.ParseElement(Jsonh2).Value.Deserialize<int>(JsonhReader.MiniJson).ShouldBe(5000);
    }
    [Fact]
    public void NumberWithRepeatedUnderscoresTest() {
        string Jsonh = """
            100__000
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<int>(JsonhReader.MiniJson).ShouldBe(100__000);
    }
    [Fact]
    public void NumberWithUnderscoreAfterBaseSpecifierTest() {
        string Jsonh = """
            0b_100
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<int>(JsonhReader.MiniJson).ShouldBe(0b_100);
    }
    [Fact]
    public void NegativeNumberWithBaseSpecifierTest() {
        string Jsonh = """
            -0x5
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<int>(JsonhReader.MiniJson).ShouldBe(-0x5);
    }
    [Fact]
    public void NumberDot() {
        string Jsonh = """
            .
            """;

        JsonhReader.ParseElement(Jsonh).Value.ValueKind.ShouldBe(JsonValueKind.String);
        JsonhReader.ParseElement(Jsonh).Value.Deserialize<string>(JsonhReader.MiniJson).ShouldBe(".");

        string Jsonh2 = """
            -.
            """;

        JsonhReader.ParseElement(Jsonh2).Value.ValueKind.ShouldBe(JsonValueKind.String);
        JsonhReader.ParseElement(Jsonh2).Value.Deserialize<string>(JsonhReader.MiniJson).ShouldBe("-.");
    }
    [Fact]
    public void DuplicatePropertyNameTest() {
        string Jsonh = """
            {
              a: 1,
              c: 2,
              a: 3,
            }
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<Dictionary<string, int>>(JsonhReader.MiniJson).ShouldBe(new() {
            ["a"] = 3,
            ["c"] = 2,
        });
    }
}
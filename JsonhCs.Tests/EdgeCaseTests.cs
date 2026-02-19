using System.Text.Json;

namespace JsonhCs.Tests;

public class EdgeCaseTests {
    [Fact]
    public void QuotelessStringStartingWithKeywordTest() {
        string Jsonh = """
            [nulla, null b, null, @null]
            """;
        string?[] Element = JsonhReader.ParseElement<string?[]>(Jsonh).Value!;

        Element.ShouldBe(["nulla", "null b", null, "null"]);
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
    public void NumberDotTest() {
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
    [Fact]
    public void EmptyNumberTest() {
        string Jsonh = """
            0e
            """;

        JsonhReader.ParseElement(Jsonh).Value.ValueKind.ShouldBe(JsonValueKind.String);
        JsonhReader.ParseElement(Jsonh).Value.Deserialize<string>(JsonhReader.MiniJson).ShouldBe("0e");
    }
    [Fact]
    public void LeadingZeroWithExponentTest() {
        string Jsonh = """
            [0e4, 0xe, 0xEe+2]
            """;

        JsonhReader.ParseElement(Jsonh).Value.ValueKind.ShouldBe(JsonValueKind.Array);
        JsonhReader.ParseElement(Jsonh).Value.Deserialize<double[]>(JsonhReader.MiniJson).ShouldBe([0e4, 0xe, 1400]);

        string Jsonh2 = """
            [e+2, 0xe+2, 0oe+2, 0be+2]
            """;

        JsonhReader.ParseElement(Jsonh2).Value.ValueKind.ShouldBe(JsonValueKind.Array);
        JsonhReader.ParseElement(Jsonh2).Value.Deserialize<string[]>(JsonhReader.MiniJson).ShouldBe(["e+2", "0xe+2", "0oe+2", "0be+2"]);

        string Jsonh3 = """
            [0x0e+, 0b0e+_1]
            """;

        JsonhReader.ParseElement(Jsonh3).Value.ValueKind.ShouldBe(JsonValueKind.Array);
        JsonhReader.ParseElement(Jsonh3).Value.Deserialize<string[]>(JsonhReader.MiniJson).ShouldBe(["0x0e+", "0b0e+_1"]);
    }
    [Fact]
    public void ErrorInBracelessPropertyNameTest() {
        string Jsonh = """
            a /
            """;

        JsonhReader.ParseElement(Jsonh).IsError.ShouldBeTrue();
    }
    [Fact]
    public void FirstPropertyNameInBracelessObjectTest() {
        string Jsonh = """
            a: b
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<Dictionary<string, string>>().ShouldBe(new() { ["a"] = "b" });

        string Jsonh2 = """
            0: b
            """;

        JsonhReader.ParseElement(Jsonh2).Value.Deserialize<Dictionary<string, string>>().ShouldBe(new() { ["0"] = "b" });

        string Jsonh3 = """
            true: b
            """;

        JsonhReader.ParseElement(Jsonh3).Value.Deserialize<Dictionary<string, string>>().ShouldBe(new() { ["true"] = "b" });
    }
    [Fact]
    public void FractionLeadingZeroesTest() {
        string Jsonh = """
            0.04
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<double>().ShouldBe(0.04);
    }
    [Fact]
    public void UnderscoreAfterLeadingZeroTest() {
        string Jsonh = """
            0_0
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<int>().ShouldBe(0_0);
    }
    [Fact]
    public void UnderscoreBesideDotTest() {
        string Jsonh = """
            [0_.0, 0._0]
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<string[]>().ShouldBe(["0_.0", "0._0"]);
    }
    [Fact]
    public void MultiQuotedStringWithNonAsciiIndentsTest() {
        string Jsonh = """"
            　
            """
            　　 a
            　　"""
            """";

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<string>().ShouldBe(" a");
    }
    [Fact]
    public void JoinCrLfInMultiQuotedStringTest() {
        string Jsonh = " ''' \\r\\nHello\r\n ''' ";

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<string>().ShouldBe("Hello");
    }
    [Fact]
    public void MassiveNumbersTest() {
        string Jsonh = """
            [
                0x999_999_999_999_999_999_999_999,
                0x999_999_999_999_999_999_999_999.0,
            ]
            """;

        JsonhReader.ParseElement(Jsonh).Value.Deserialize<double[]>().ShouldBe([
            47_536_897_508_558_602_556_126_370_201.0,
            47_536_897_508_558_602_556_126_370_201.0,
        ]);
    }
}
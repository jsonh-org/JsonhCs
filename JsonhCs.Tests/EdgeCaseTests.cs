﻿using System.Text.Json;

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
        JsonhReader.ParseElement<string[]>(Jsonh).IsError.ShouldBeTrue();
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
}
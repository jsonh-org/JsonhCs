namespace JsonhCs.Tests;

public class EdgeCaseTests {
    [Fact]
    public void QuotelessStringStartingWithKeywordTest() {
        string Jsonh = """
            [nulla, null b, null]
            """;
        string?[] Element = JsonhReader.ParseElement<string?[]>(Jsonh).Value!;

        Element[0].ShouldBe("nulla");
        Element[1].ShouldBe("null b");
        Element[2].ShouldBeNull();
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
}
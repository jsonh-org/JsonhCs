namespace JsonhCs.Tests;

public class EdgeCaseTests {
    [Fact]
    public void QuotelessStringStartingWithKeywordTest() {
        string Jsonh = """
            [nulla, null b, null]
            """;
        string?[] Element = JsonhReader.ParseElement<string?[]>(Jsonh).Value!;

        Assert.Equal("nulla", Element[0]);
        Assert.Equal("null b", Element[1]);
        Assert.Null(Element[2]);
    }
    [Fact]
    public void BracelessObjectWithInvalidValueTest() {
        string Jsonh = """
            a: {
            """;
        Assert.True(JsonhReader.ParseElement<string[]>(Jsonh).IsError);
    }
}
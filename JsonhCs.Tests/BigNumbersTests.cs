using System.Text.Json;

namespace JsonhCs.Tests;

public class BigNumbersTests {
    [Fact]
    public void HugeNumberTest() {
        string Jsonh = """
            1e99999
            """;

        JsonElement Element = JsonhReader.ParseElement(Jsonh, new JsonhReaderOptions() {
            BigNumbers = false,
        }).Value;
        Element.ValueKind.ShouldBe(JsonValueKind.String);
        Element.ToString().ShouldBe("Infinity");

        JsonElement BigElement = JsonhReader.ParseElement(Jsonh, new JsonhReaderOptions() {
            BigNumbers = true,
        }).Value;
        BigElement.ValueKind.ShouldBe(JsonValueKind.Number);
        BigElement.ToString().ShouldBe("1" + new string('0', 99999));
    }
}
using System.Numerics;
using ExtendedNumerics;

namespace JsonhCs;

public static class JsonhNumberParser {
    /// <summary>
    /// Converts a JSONH number to a base-10 decimal.
    /// For example:<br/>
    /// Input: <c>+5.2e3.0</c><br/>
    /// Output: <c>5200</c>
    /// </summary>
    public static BigDecimal Parse(string JsonhNumber) {
        // Decimal
        string BaseDigits = "0123456789";
        // Hexadecimal
        if (JsonhNumber.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            BaseDigits = "0123456789ABCDEFabcdef";
            JsonhNumber = JsonhNumber[2..];
        }
        // Binary
        else if (JsonhNumber.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) {
            BaseDigits = "01";
            JsonhNumber = JsonhNumber[2..];
        }
        // Octal
        else if (JsonhNumber.StartsWith("0o", StringComparison.OrdinalIgnoreCase)) {
            BaseDigits = "01234567";
            JsonhNumber = JsonhNumber[2..];
        }

        // Remove underscores
        JsonhNumber = JsonhNumber.Replace("_", "");

        // Parse number of base
        return ParseFractionalNumberWithExponent(JsonhNumber, BaseDigits);
    }

    /// <summary>
    /// Converts a fractional number with an exponent (e.g. <c>12.3e4.5</c>) from the given base (e.g. <c>01234567</c>) to a base-10 decimal.
    /// </summary>
    private static BigDecimal ParseFractionalNumberWithExponent(ReadOnlySpan<char> Digits, ReadOnlySpan<char> BaseDigits) {
        // Find exponent
        int DotIndex = Digits.IndexOfAny('e', 'E');
        // If no exponent then normalize real
        if (DotIndex < 0) {
            return ParseFractionalNumber(Digits, BaseDigits);
        }

        // Get parts of number
        ReadOnlySpan<char> MantissaPart = Digits[..DotIndex];
        ReadOnlySpan<char> ExponentPart = Digits[(DotIndex + 1)..];

        // Normalize each part and combine parts
        return BigDecimal.Parse(ParseFractionalNumber(MantissaPart, BaseDigits) + "E" + ParseFractionalNumber(ExponentPart, BaseDigits));
    }
    /// <summary>
    /// Converts a fractional number (e.g. <c>123.45</c>) from the given base (e.g. <c>01234567</c>) to a base-10 decimal.
    /// </summary>
    private static BigDecimal ParseFractionalNumber(ReadOnlySpan<char> Digits, ReadOnlySpan<char> BaseDigits) {
        // Optimization for base-10 digits
        if (BaseDigits is "0123456789") {
            return BigDecimal.Parse(Digits.ToString()); // TODO: Pass span not string when overload is added
        }

        // Find dot
        int DotIndex = Digits.IndexOf('.');
        // If no dot then normalize integer
        if (DotIndex < 0) {
            return ParseWholeNumber(Digits, BaseDigits);
        }

        // Get parts of number
        ReadOnlySpan<char> WholePart = Digits[..DotIndex];
        ReadOnlySpan<char> FractionPart = Digits[(DotIndex + 1)..];

        // Normalize each part and combine parts
        return BigDecimal.Parse(ParseWholeNumber(WholePart, BaseDigits) + "." + ParseWholeNumber(FractionPart, BaseDigits));
    }
    /// <summary>
    /// Converts a whole number (e.g. <c>12345</c>) from the given base (e.g. <c>01234567</c>) to a base-10 integer.
    /// </summary>
    private static BigInteger ParseWholeNumber(ReadOnlySpan<char> Digits, ReadOnlySpan<char> BaseDigits) {
        // Optimization for base-10 digits
        if (BaseDigits is "0123456789") {
            return BigInteger.Parse(Digits);
        }

        // Get sign
        string Sign = "";
        if (Digits.StartsWith("-")) {
            Sign = "-";
            Digits = Digits[1..];
        }
        else if (Digits.StartsWith("+")) {
            Sign = "+";
            Digits = Digits[1..];
        }

        // Add each column of digits
        BigInteger Integer = 0;
        for (int Index = 0; Index < Digits.Length; Index++) {
            // Get current digit
            char DigitChar = Digits[Index];
            int DigitInt = BaseDigits.IndexOf(DigitChar);

            // Ensure digit is valid
            if (DigitInt < 0) {
                throw new ArgumentException($"Invalid digit: '{DigitChar}'", nameof(Digits));
            }

            // Get magnitude of current digit column
            int ColumnNumber = Digits.Length - 1 - Index;
            BigInteger ColumnMagnitude = BigInteger.Pow(BaseDigits.Length, ColumnNumber);

            // Add value of column
            Integer += (BigInteger)DigitInt * ColumnMagnitude;
        }

        // Apply sign
        if (Sign is "-") {
            Integer *= -1;
        }
        return Integer;
    }
}
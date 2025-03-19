using System.Numerics;
using ExtendedNumerics;

namespace JsonhCs;

public static class JsonhNumberParser {
    /// <summary>
    /// Converts a JSONH number to a base-10 real.
    /// For example:<br/>
    /// Input: <c>+5.2e3.0</c><br/>
    /// Output: <c>5200</c>
    /// </summary>
    /// <param name="Precision">Used when a fractional exponent is given.</param>
    public static BigReal Parse(string JsonhNumber, int Precision = 10) {
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

        // Parse number with base digits
        return ParseFractionalNumberWithExponent(JsonhNumber, BaseDigits, Precision);
    }

    /// <summary>
    /// Converts a fractional number with an exponent (e.g. <c>12.3e4.5</c>) from the given base (e.g. <c>01234567</c>) to a base-10 real.
    /// </summary>
    private static BigReal ParseFractionalNumberWithExponent(ReadOnlySpan<char> Digits, ReadOnlySpan<char> BaseDigits, int Precision) {
        // Find exponent
        int DotIndex = Digits.IndexOfAny('e', 'E');
        // If no exponent then normalize real
        if (DotIndex < 0) {
            return ParseFractionalNumber(Digits, BaseDigits);
        }

        // Get mantissa and exponent
        ReadOnlySpan<char> MantissaPart = Digits[..DotIndex];
        ReadOnlySpan<char> ExponentPart = Digits[(DotIndex + 1)..];

        // Parse mantissa and exponent
        BigReal Mantissa = ParseFractionalNumber(MantissaPart, BaseDigits);
        BigReal Exponent = ParseFractionalNumber(ExponentPart, BaseDigits);

        // Multiply mantissa by 10 ^ exponent
        return Mantissa * BigReal.Pow(10, Exponent, Precision);
    }
    /// <summary>
    /// Converts a fractional number (e.g. <c>123.45</c>) from the given base (e.g. <c>01234567</c>) to a base-10 real.
    /// </summary>
    private static BigReal ParseFractionalNumber(ReadOnlySpan<char> Digits, ReadOnlySpan<char> BaseDigits) {
        // Optimization for base-10 digits
        if (BaseDigits is "0123456789") {
            return BigReal.Parse(Digits);
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

        // Parse parts of number
        BigInteger Whole = ParseWholeNumber(WholePart, BaseDigits);
        BigInteger Fraction = ParseWholeNumber(FractionPart, BaseDigits);

        // Combine whole and fraction
        return BigReal.Parse(Whole + "." + Fraction);
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
        int Sign = 1;
        if (Digits.StartsWith("-")) {
            Sign = -1;
            Digits = Digits[1..];
        }
        else if (Digits.StartsWith("+")) {
            Sign = 1;
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
        if (Sign != 1) {
            Integer *= Sign;
        }
        return Integer;
    }
}
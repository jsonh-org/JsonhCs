using System.Buffers;
using System.Numerics;
using System.Text;

namespace JsonhCs;

internal ref struct SimpleValueStringBuilder : IDisposable {
    private Span<char> Buffer;
    private int BufferPosition;
    private char[]? RentedArray;

    public SimpleValueStringBuilder(Span<char> InitialBuffer) {
        Buffer = InitialBuffer;
    }
    public readonly int Length {
        get => BufferPosition;
    }
    public readonly char this[int Index] {
        get => Buffer[..BufferPosition][Index];
    }
    public void Dispose() {
        if (RentedArray is not null) {
            ArrayPool<char>.Shared.Return(RentedArray, clearArray: true);
        }
        this = default;
    }
    public readonly override string ToString() {
        return AsSpan().ToString();
    }
    public readonly ReadOnlySpan<char> AsSpan() {
        return Buffer[..BufferPosition];
    }
    public void EnsureCapacity(int MinimumCapacity) {
        checked {
            if (Buffer.Length >= MinimumCapacity) {
                return;
            }

            int RentCapacity = (int)BitOperations.RoundUpToPowerOf2((uint)MinimumCapacity);

            char[]? ExistingRentedArray = RentedArray;

            RentedArray = ArrayPool<char>.Shared.Rent(RentCapacity);
            Buffer[..BufferPosition].CopyTo(RentedArray);
            Buffer = RentedArray;

            if (ExistingRentedArray is not null) {
                ArrayPool<char>.Shared.Return(ExistingRentedArray, clearArray: true);
            }
        }
    }
    public void Append(char Value) {
        checked {
            EnsureCapacity(BufferPosition + 1);
            Buffer[BufferPosition] = Value;
            BufferPosition++;
        }
    }
    public void Append(Rune Value) {
        checked {
            Span<char> ValueChars = stackalloc char[2];
            int ValueCharsWritten = Value.EncodeToUtf16(ValueChars);
            ReadOnlySpan<char> ValueCharsSlice = ValueChars[..ValueCharsWritten];

            Append(ValueCharsSlice);
        }
    }
    public void Append(scoped ReadOnlySpan<char> Value) {
        checked {
            if (Value.IsEmpty) {
                return;
            }

            EnsureCapacity(BufferPosition + Value.Length);
            Value.CopyTo(Buffer[BufferPosition..]);
            BufferPosition += Value.Length;
        }
    }
    public void Append<T>(T Value, int BufferSize = 36, IFormatProvider? FormatProvider = null) where T : ISpanFormattable {
        checked {
            EnsureCapacity(BufferPosition + BufferSize);
            if (Value.TryFormat(Buffer[BufferPosition..], out int CharsWritten, default, FormatProvider)) {
                BufferPosition += CharsWritten;
                return;
            }

            Append(Value.ToString(null, FormatProvider));
        }
    }
    public void Remove(int StartIndex, int Count) {
        checked {
            ArgumentOutOfRangeException.ThrowIfNegative(Count);
            ArgumentOutOfRangeException.ThrowIfNegative(StartIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(StartIndex + Count, BufferPosition, nameof(Count));

            if (Count <= 0) {
                return;
            }
            Buffer[(StartIndex + Count)..BufferPosition].CopyTo(Buffer[StartIndex..]);
            BufferPosition -= Count;
        }
    }
    public void Trim() {
        checked {
            ReadOnlySpan<char> Trimmed = Buffer[..BufferPosition].Trim();
            Trimmed.CopyTo(Buffer);
            BufferPosition = Trimmed.Length;
        }
    }
}
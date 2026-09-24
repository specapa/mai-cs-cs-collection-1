using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

// Умножение Карацубы: O(n^log2(3)). Половинки берутся срезами Span, без копирования цифр.
internal class KaratsubaMultiplier : IMultiplier
{
    private const int Threshold = 8;

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        bool isNegative = a.IsNegative != b.IsNegative;
        return new BetterBigInteger(MultiplySpans(a.GetDigits(), b.GetDigits()), isNegative);
    }

    private static uint[] MultiplySpans(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        a = Trim(a);
        b = Trim(b);
        if (a.IsEmpty || b.IsEmpty)
        {
            return [0];
        }

        int maxLength = Math.Max(a.Length, b.Length);
        if (maxLength < Threshold)
        {
            return SimpleMultiplier.MultiplyMagnitudes(a, b);
        }

        int half = maxLength / 2;
        ReadOnlySpan<uint> aLow = a.Length > half ? a[..half] : a;
        ReadOnlySpan<uint> bLow = b.Length > half ? b[..half] : b;
        ReadOnlySpan<uint> aHigh = a.Length > half ? a[half..] : default;
        ReadOnlySpan<uint> bHigh = b.Length > half ? b[half..] : default;

        uint[] z0 = MultiplySpans(aLow, bLow);
        uint[] z2 = MultiplySpans(aHigh, bHigh);
        uint[] cross = MultiplySpans(
            BetterBigInteger.AddMagnitudes(aLow, aHigh),
            BetterBigInteger.AddMagnitudes(bLow, bHigh));
        uint[] z1 = SubtractNonNegative(SubtractNonNegative(cross, z0), z2);

        return Combine(z0, z1, z2, half);
    }

    private static ReadOnlySpan<uint> Trim(ReadOnlySpan<uint> digits)
    {
        int length = digits.Length;
        while (length > 0 && digits[length - 1] == 0)
        {
            length--;
        }

        return digits[..length];
    }

    private static uint[] SubtractNonNegative(uint[] minuend, uint[] subtrahend)
    {
        if (minuend.Length < subtrahend.Length)
        {
            uint[] padded = new uint[subtrahend.Length];
            minuend.AsSpan().CopyTo(padded);
            minuend = padded;
        }

        return BetterBigInteger.SubtractMagnitudes(minuend, subtrahend);
    }

    private static uint[] Combine(ReadOnlySpan<uint> z0, ReadOnlySpan<uint> z1, ReadOnlySpan<uint> z2, int half)
    {
        int length = Math.Max(z0.Length, Math.Max(z1.Length + half, z2.Length + 2 * half));
        uint[] sum = new uint[length + 2];
        AddAt(sum, 0, z0);
        AddAt(sum, half, z1);
        AddAt(sum, 2 * half, z2);
        return BetterBigInteger.NormalizeLittleEndian(sum);
    }

    private static void AddAt(uint[] buffer, int offset, ReadOnlySpan<uint> addend)
    {
        uint carry = 0;
        int index = 0;
        for (; index < addend.Length; index++)
        {
            uint sum = buffer[offset + index] + addend[index];
            uint carry1 = sum < buffer[offset + index] ? 1u : 0u;
            uint total = sum + carry;
            uint carry2 = total < sum ? 1u : 0u;
            buffer[offset + index] = total;
            carry = carry1 + carry2;
        }

        while (carry != 0)
        {
            uint total = buffer[offset + index] + carry;
            carry = total < buffer[offset + index] ? 1u : 0u;
            buffer[offset + index] = total;
            index++;
        }
    }
}

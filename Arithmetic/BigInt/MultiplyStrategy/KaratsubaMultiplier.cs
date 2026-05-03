using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

// Умножение Карацубы: O(n^log2(3)), рекурсивное «разделяй и властвуй»
internal class KaratsubaMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        bool isNegative = a.IsNegative != b.IsNegative;
        uint[] aDigits = a.GetDigits().ToArray();
        uint[] bDigits = b.GetDigits().ToArray();

        return new BetterBigInteger(MultiplyArrays(aDigits, bDigits), isNegative);
    }

    private static uint[] MultiplyArrays(uint[] a, uint[] b)
    {
        int maxLen = Math.Max(a.Length, b.Length);

        // Для коротких чисел выгоднее обычное умножение в столбик
        if (maxLen < 8)
        {
            return SimpleMultiply(a, b);
        }

        // Делим числа пополам: m — длина младшей части
        int m = maxLen / 2;
        int highLen = maxLen - m;

        // Старшие половины (дополняем нулём, если число короче m)
        uint[] aHigh = a.Length > m ? new uint[highLen] : [0];
        if (a.Length > m)
        {
            Array.Copy(a, m, aHigh, 0, highLen);
        }

        uint[] bHigh = b.Length > m ? new uint[highLen] : [0];
        if (b.Length > m)
        {
            Array.Copy(b, m, bHigh, 0, highLen);
        }

        // Младшие половины
        uint[] aLow = new uint[Math.Min(m, a.Length)];
        Array.Copy(a, 0, aLow, 0, aLow.Length);
        uint[] bLow = new uint[Math.Min(m, b.Length)];
        Array.Copy(b, 0, bLow, 0, bLow.Length);

        // z0 = aLow * bLow,  z2 = aHigh * bHigh
        uint[] z0 = MultiplyArrays(aLow, bLow);
        uint[] z2 = MultiplyArrays(aHigh, bHigh);

        // z1 = (aLow + aHigh) * (bLow + bHigh) - z0 - z2
        uint[] z1 = SubtractArrays(
            SubtractArrays(MultiplyArrays(AddArrays(aLow, aHigh), AddArrays(bLow, bHigh)), z0),
            z2);

        // Сдвигаем z2 и z1 на 2m и m позиций соответственно
        uint[] z2Shifted = new uint[z2.Length + 2 * m];
        Array.Copy(z2, 0, z2Shifted, 2 * m, z2.Length);
        uint[] z1Shifted = new uint[z1.Length + m];
        Array.Copy(z1, 0, z1Shifted, m, z1.Length);

        // Итог: z2 * 10^(2m) + z1 * 10^m + z0
        return AddArrays(AddArrays(z2Shifted, z1Shifted), z0);
    }

    // Базовое умножение в столбик (тот же приём, что в SimpleMultiplier)
    private static uint[] SimpleMultiply(uint[] a, uint[] b)
    {
        uint[] result = new uint[a.Length + b.Length];

        for (int i = 0; i < a.Length; i++)
        {
            uint carry = 0;
            uint aVal = a[i];
            uint a0 = aVal & 0xFFFF;
            uint a1 = aVal >> 16;

            for (int j = 0; j < b.Length; j++)
            {
                uint bVal = b[j];
                uint b0 = bVal & 0xFFFF;
                uint b1 = bVal >> 16;

                uint p00 = b0 * a0;
                uint p11 = a1 * b1;
                uint p01 = a0 * b1;
                uint p10 = a1 * b0;

                uint c0 = p00 & 0xFFFF;
                uint c1 = (p00 >> 16) + (p01 & 0xFFFF) + (p10 & 0xFFFF);
                uint c2 = (p01 >> 16) + (p10 >> 16) + (p11 & 0xFFFF) + (c1 >> 16);
                uint c3 = (p11 >> 16) + (c2 >> 16);

                uint low = c0 | (c1 << 16);
                uint high = (c2 & 0xFFFF) | (c3 << 16);

                uint current = result[i + j];
                uint sum = current + low;
                uint carry1 = sum < current ? 1u : 0u;

                uint total = sum + carry;
                uint carry2 = total < sum ? 1u : 0u;

                result[i + j] = total;
                carry = high + carry1 + carry2;
            }

            result[i + b.Length] = carry;
        }

        return result;
    }

    // Поразрядное сложение с переносом
    private static uint[] AddArrays(uint[] first, uint[] second)
    {
        uint[] result = new uint[Math.Max(first.Length, second.Length) + 1];

        uint carry = 0;
        for (int i = 0; i < result.Length - 1; i++)
        {
            uint left = i < first.Length ? first[i] : 0;
            uint right = i < second.Length ? second[i] : 0;

            uint sum = left + right;
            bool overflow1 = sum < left;

            sum += carry;
            bool overflow2 = sum < carry;

            result[i] = sum;
            carry = overflow1 || overflow2 ? 1u : 0u;
        }

        result[^1] = carry;
        return result;
    }

    // Поразрядное вычитание с займом
    private static uint[] SubtractArrays(uint[] minuend, uint[] subtrahend)
    {
        uint[] result = new uint[minuend.Length];
        uint borrow = 0;

        for (int i = 0; i < minuend.Length; i++)
        {
            uint sub = i < subtrahend.Length ? subtrahend[i] : 0;

            uint value = minuend[i] - borrow;
            bool borrowedFromThis = borrow > minuend[i];

            uint diff = value - sub;
            bool borrowedForSub = value < sub;

            result[i] = diff;
            borrow = borrowedFromThis || borrowedForSub ? 1u : 0u;
        }

        return result;
    }
}

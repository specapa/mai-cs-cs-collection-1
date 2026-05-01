using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

// Умножение «в столбик»: O(n*m), базовый алгоритм для небольших чисел
internal class SimpleMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        // Знак результата: отрицательный, если знаки операндов различаются
        bool isNegative = a.IsNegative ^ b.IsNegative;
        ReadOnlySpan<uint> aDigits = a.GetDigits();
        ReadOnlySpan<uint> bDigits = b.GetDigits();

        // Результат не длиннее суммы длин операндов
        uint[] result = new uint[aDigits.Length + bDigits.Length];

        // Перебираем цифры a; каждая умножается на все цифры b
        for (int i = 0; i < aDigits.Length; i++)
        {
            uint carry = 0;
            uint aVal = aDigits[i];
            // Разбиваем 32-битную цифру на две 16-битные половины
            uint a0 = aVal & 0xFFFF;
            uint a1 = aVal >> 16;

            for (int j = 0; j < bDigits.Length; j++)
            {
                uint bVal = bDigits[j];
                uint b0 = bVal & 0xFFFF;
                uint b1 = bVal >> 16;

                // Четыре частичных произведения половин
                uint p00 = b0 * a0;
                uint p11 = a1 * b1;
                uint p01 = a0 * b1;
                uint p10 = a1 * b0;

                // Складываем частичные произведения с учётом переносов
                uint c0 = p00 & 0xFFFF;
                uint c1 = (p00 >> 16) + (p01 & 0xFFFF) + (p10 & 0xFFFF);
                uint c2 = (p01 >> 16) + (p10 >> 16) + (p11 & 0xFFFF) + (c1 >> 16);
                uint c3 = (p11 >> 16) + (c2 >> 16);

                uint low = c0 | (c1 << 16);
                uint high = (c2 & 0xFFFF) | (c3 << 16);

                // Добавляем произведение в ячейку результата с переносом
                uint current = result[i + j];
                uint sum = current + low;
                uint carry1 = sum < current ? 1u : 0u;

                uint total = sum + carry;
                uint carry2 = total < sum ? 1u : 0u;

                result[i + j] = total;
                carry = high + carry1 + carry2;
            }

            // Остаточный перенос записываем в следующую позицию
            result[i + bDigits.Length] = carry;
        }

        return new BetterBigInteger(result, isNegative);
    }
}

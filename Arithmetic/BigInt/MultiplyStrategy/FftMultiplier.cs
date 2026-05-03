using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

// Умножение через БПФ (NTT): O(n log n), для очень больших чисел
internal class FftMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        bool isNegative = a.IsNegative != b.IsNegative;

        // Разбиваем 32-битные цифры на байты — так NTT работает над полиномами
        uint[] aBytes = SplitToBytes(a.GetDigits());
        uint[] bBytes = SplitToBytes(b.GetDigits());

        // Длина свёртки полиномов; дополняем до степени двойки
        int resultLength = aBytes.Length + bBytes.Length - 1;
        int n = 1;
        while (n < resultLength)
        {
            n <<= 1;
        }

        Array.Resize(ref aBytes, n);
        Array.Resize(ref bBytes, n);

        // Три взаимно простых модуля для китайской теоремы об остатках
        uint[] mods = [167772161u, 469762049u, 754974721u];
        uint[] generators = [3u, 3u, 11u];
        uint[][] results = new uint[3][];

        for (int m = 0; m < 3; m++)
        {
            uint mod = mods[m];
            uint gen = generators[m];
            // Примитивный корень степени n из единицы по модулю mod
            uint exp = (mod - 1) / (uint)n;
            uint w = PowMod(gen, exp, mod);

            uint[] left = new uint[n];
            uint[] right = new uint[n];
            Array.Copy(aBytes, left, n);
            Array.Copy(bBytes, right, n);

            // Прямое NTT обоих операндов
            Ntt(left, mod, w);
            Ntt(right, mod, w);

            // Покомпонентное умножение в частотной области
            for (int i = 0; i < n; i++)
            {
                left[i] = MultiplyMod(left[i], right[i], mod);
            }

            // Обратное NTT — получаем коэффициенты произведения по mod
            InverseNtt(left, mod, w);
            results[m] = left;
        }

        // Собираем точный результат из трёх остатков через CRT
        uint[] chunks = CrtReconstruct(results, resultLength);
        return ConvertToBigInteger(chunks, resultLength, isNegative);
    }

    // Каждую 32-битную цифру раскладываем на 4 байта (младшие разряды первыми)
    private static uint[] SplitToBytes(ReadOnlySpan<uint> digits)
    {
        uint[] bytes = new uint[digits.Length * 4];
        for (int i = 0; i < digits.Length; i++)
        {
            bytes[4 * i] = digits[i] & 0xFF;
            bytes[4 * i + 1] = (digits[i] >> 8) & 0xFF;
            bytes[4 * i + 2] = (digits[i] >> 16) & 0xFF;
            bytes[4 * i + 3] = digits[i] >> 24;
        }

        return bytes;
    }

    // Сложение по модулю mod
    private static uint AddMod(uint a, uint b, uint mod)
    {
        uint sum = a + b;
        if (sum < a || sum >= mod)
        {
            sum -= mod;
        }

        return sum;
    }

    // Вычитание по модулю mod
    private static uint SubtractMod(uint a, uint b, uint mod)
    {
        if (a >= b)
        {
            return a - b;
        }

        return a + (mod - b);
    }

    // Умножение 32-битных чисел с приведением по mod (без переполнения ulong)
    private static uint MultiplyMod(uint a, uint b, uint mod)
    {
        uint aHigh = a >> 16;
        uint aLow = a & 0xFFFF;
        uint bHigh = b >> 16;
        uint bLow = b & 0xFFFF;

        uint p00 = bLow * aLow;
        uint p11 = bHigh * aHigh;
        uint p10 = aHigh * bLow;
        uint p01 = aLow * bHigh;

        uint low = p00 + ((p01 & 0xFFFF) << 16) + ((p10 & 0xFFFF) << 16);
        uint carry = (p00 >> 16) + (p01 & 0xFFFF) + (p10 & 0xFFFF);
        uint high = p11 + (p01 >> 16) + (p10 >> 16) + (carry >> 16);
        low = (low & 0xFFFF) | ((carry & 0xFFFF) << 16);

        return Reduce64(high, low, mod);
    }

    // Приведение 64-битного числа (high:low) по модулю mod
    private static uint Reduce64(uint high, uint low, uint mod)
    {
        if (mod == 1)
        {
            return 0;
        }

        while (high != 0 || low >= mod)
        {
            if (high == 0)
            {
                return low % mod;
            }

            uint quotient = high % mod;
            high /= mod;

            // quotient * 2^32 ≡ quotient * (2^32 mod mod) (mod mod)
            uint shifted = quotient;
            for (int i = 0; i < 32; i++)
            {
                shifted = AddMod(shifted, shifted, mod);
            }

            low = AddMod(low, shifted, mod);
        }

        return low;
    }

    // Быстрое возведение в степень по модулю
    private static uint PowMod(uint baseValue, uint exponent, uint mod)
    {
        if (mod == 1)
        {
            return 0;
        }

        uint result = 1;
        baseValue %= mod;

        while (exponent > 0)
        {
            if ((exponent & 1) == 1)
            {
                result = MultiplyMod(result, baseValue, mod);
            }

            baseValue = MultiplyMod(baseValue, baseValue, mod);
            exponent >>= 1;
        }

        return result;
    }

    // Обратный элемент по модулю (расширенный алгоритм Евклида)
    private static uint InverseMod(uint a, uint mod)
    {
        if (mod == 0)
        {
            throw new DivideByZeroException();
        }

        if (mod == 1)
        {
            return 0;
        }

        a %= mod;

        uint m0 = mod;
        uint x0 = 0;
        uint x1 = 1;
        uint a0 = a;

        while (a0 > 1)
        {
            uint quotient = a0 / m0;

            uint temp = m0;
            m0 = a0 % m0;
            a0 = temp;

            uint quotientTimesX0 = MultiplyMod(quotient, x0, mod);
            uint xNew = x1 >= quotientTimesX0 ? x1 - quotientTimesX0 : x1 + (mod - quotientTimesX0);

            temp = x0;
            x0 = xNew;
            x1 = temp;
        }

        return x1;
    }

    // Прямое численное преобразование Фурье (рекурсивное, по основанию 2)
    private static void Ntt(uint[] data, uint mod, uint root)
    {
        int n = data.Length;
        if (n <= 1)
        {
            return;
        }

        // Разделяем на чётные и нечётные индексы
        uint[] even = new uint[n / 2];
        uint[] odd = new uint[n / 2];

        for (int i = 0; i < n / 2; i++)
        {
            even[i] = data[2 * i];
            odd[i] = data[2 * i + 1];
        }

        uint rootSquared = MultiplyMod(root, root, mod);
        Ntt(even, mod, rootSquared);
        Ntt(odd, mod, rootSquared);

        // Бабочка Кули–Тьюки: объединяем чётную и нечётную части
        uint w = 1;
        for (int i = 0; i < n / 2; i++)
        {
            uint t = MultiplyMod(w, odd[i], mod);
            data[i] = AddMod(even[i], t, mod);
            data[n / 2 + i] = SubtractMod(even[i], t, mod);
            w = MultiplyMod(w, root, mod);
        }
    }

    // Обратное NTT: инвертируем корень и делим на n
    private static void InverseNtt(uint[] data, uint mod, uint root)
    {
        uint inverseRoot = InverseMod(root, mod);
        Ntt(data, mod, inverseRoot);

        uint inverseLength = InverseMod((uint)data.Length, mod);
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = MultiplyMod(data[i], inverseLength, mod);
        }
    }

    // Восстановление полного 96-битного значения из трёх остатков (CRT)
    private static uint[] CrtReconstruct(uint[][] results, int resultLength)
    {
        const uint p1 = 167772161u;
        const uint p2 = 469762049u;
        const uint p3 = 754974721u;

        (uint p1p2Low, uint p1p2High) = MultiplyFull(p1, p2);
        uint inverseP1ModP2 = InverseMod(p1 % p2, p2);
        uint p1p2ModP3 = Reduce64(p1p2High, p1p2Low, p3);
        uint inverseP1P2ModP3 = InverseMod(p1p2ModP3, p3);

        uint[] result = new uint[resultLength * 3];

        for (int i = 0; i < resultLength; i++)
        {
            uint r1 = results[0][i];
            uint r2 = results[1][i];
            uint r3 = results[2][i];

            // Шаг 1 CRT: x12 ≡ r1 (mod p1), x12 ≡ r2 (mod p2)
            uint diff = SubtractMod(r2, r1 % p2, p2);
            uint k = MultiplyMod(diff, inverseP1ModP2, p2);

            (uint kP1Low, uint kP1High) = MultiplyFull(k, p1);
            uint carry = AddWithCarry(ref kP1Low, r1);
            uint x12High = kP1High + carry;
            uint x12Low = kP1Low;

            // Шаг 2 CRT: x ≡ x12 (mod p1*p2), x ≡ r3 (mod p3)
            uint x12ModP3 = Reduce64(x12High, x12Low, p3);
            diff = SubtractMod(r3, x12ModP3, p3);
            uint t = MultiplyMod(diff, inverseP1P2ModP3, p3);

            (uint tP1P2Low, uint tP1P2High) = MultiplyFull(t, p1p2Low);
            (uint tP1P2Mid, uint tP1P2High2) = MultiplyFull(t, p1p2High);

            carry = AddWithCarry(ref tP1P2Low, 0);
            carry = AddWithCarry(ref tP1P2Mid, tP1P2High + carry);
            uint totalHigh = tP1P2High2 + carry;

            carry = AddWithCarry(ref tP1P2Low, x12Low);
            carry = AddWithCarry(ref tP1P2Mid, x12High + carry);
            totalHigh += carry;

            // Храним 96-битный результат как три uint (low, mid, high)
            result[3 * i] = tP1P2Low;
            result[3 * i + 1] = tP1P2Mid;
            result[3 * i + 2] = totalHigh;
        }

        return result;
    }

    private static uint AddWithCarry(ref uint a, uint b)
    {
        uint sum = a + b;
        uint carry = sum < a ? 1u : 0u;
        a = sum;
        return carry;
    }

    // Полное 32×32 → 64-битное умножение без ulong
    private static (uint Low, uint High) MultiplyFull(uint a, uint b)
    {
        uint aLow = a & 0xFFFF;
        uint aHigh = a >> 16;
        uint bLow = b & 0xFFFF;
        uint bHigh = b >> 16;

        uint lowLow = aLow * bLow;
        uint lowHigh = aLow * bHigh;
        uint highLow = aHigh * bLow;
        uint highHigh = aHigh * bHigh;

        uint low = lowLow + ((lowHigh & 0xFFFF) << 16) + ((highLow & 0xFFFF) << 16);
        uint carry = (lowLow >> 16) + (lowHigh & 0xFFFF) + (highLow & 0xFFFF);
        uint high = highHigh + (lowHigh >> 16) + (highLow >> 16) + (carry >> 16);
        low = (low & 0xFFFF) | ((carry & 0xFFFF) << 16);

        return (low, high);
    }

    // Собираем байты из CRT-чанков, нормализуем переносы и упаковываем в цифры
    private static BetterBigInteger ConvertToBigInteger(uint[] chunks, int resultLength, bool isNegative)
    {
        List<uint> bytes = [];
        uint carry = 0;

        for (int i = 0; i < resultLength; i++)
        {
            uint low = chunks[3 * i];
            uint mid = chunks[3 * i + 1];
            uint high = chunks[3 * i + 2];

            // Разворачиваем 96-битный чанк в поток байт с учётом переноса
            low += carry;
            if (low < carry)
            {
                mid++;
                if (mid == 0)
                {
                    high++;
                }
            }

            bytes.Add(low & 0xFF);

            carry = (low >> 8) | (mid << 24);
            mid = (mid >> 8) | (high << 24);
            high >>= 8;

            while (mid != 0 || high != 0)
            {
                low = carry;
                bytes.Add(low & 0xFF);

                carry = (low >> 8) | (mid << 24);
                mid = (mid >> 8) | (high << 24);
                high >>= 8;
            }
        }

        while (carry != 0)
        {
            bytes.Add(carry & 0xFF);
            carry >>= 8;
        }

        // Упаковываем байты обратно в 32-битные цифры
        uint[] digits = new uint[(bytes.Count + 3) / 4];
        for (int i = 0; i < bytes.Count; i++)
        {
            int index = i / 4;
            int shift = (i % 4) * 8;
            digits[index] |= bytes[i] << shift;
        }

        // Убираем ведущие нули
        int length = digits.Length;
        while (length > 1 && digits[length - 1] == 0)
        {
            length--;
        }

        uint[] trimmed = new uint[length];
        Array.Copy(digits, trimmed, length);

        return new BetterBigInteger(trimmed, isNegative);
    }
}

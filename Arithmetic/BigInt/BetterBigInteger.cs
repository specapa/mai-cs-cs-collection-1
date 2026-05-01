using System.Globalization;
using System.Numerics;
using System.Text;
using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    #region Поля и константы

    // Пороги для выбора алгоритма умножения
    private const int KaratsubaThreshold = 32;
    private const int FftThreshold = 512;

    // 0 — положительное, 1 — отрицательное
    private int _signBit;
    // Если число маленькое, храним его прямо в этом поле, а _data == null
    private uint _smallValue;
    // Массив разрядов в системе 2^32 (little-endian), null если число маленькое
    private uint[]? _data;

    public bool IsNegative => _signBit == 1;

    #endregion

    #region Конструкторы

    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        ArgumentNullException.ThrowIfNull(digits);
        InitializeFromDigits(NormalizeLittleEndian(digits), isNegative);
    }

    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
    {
        ArgumentNullException.ThrowIfNull(digits);
        InitializeFromDigits(NormalizeLittleEndian([.. digits]), isNegative);
    }

    public BetterBigInteger(string value, int radix)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (radix is < 2 or > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36.");
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new FormatException("Value cannot be empty.");
        }

        bool isNegative = false;
        int index = 0;
        if (trimmed[0] is '+' or '-')
        {
            isNegative = trimmed[0] == '-';
            index = 1;
        }

        if (index >= trimmed.Length)
        {
            throw new FormatException("Value must contain digits.");
        }

        // Собираем число поразрядно: result = result * radix + digit
        List<uint> digits = [0];
        for (int i = index; i < trimmed.Length; i++)
        {
            uint digit = (uint)GetDigitValue(trimmed[i], radix);
            MultiplyAndAdd(digits, (uint)radix, digit);
        }

        InitializeFromDigits(NormalizeLittleEndian([.. digits]), isNegative);
    }

    private BetterBigInteger(int signBit, uint smallValue, uint[]? data)
    {
        _signBit = signBit;
        _smallValue = smallValue;
        _data = data;
    }

    #endregion

    #region Сравнение и равенство

    // Возвращает модуль числа (без знака), с учётом оптимизации для значений в в слово
    public ReadOnlySpan<uint> GetDigits() => _data ?? [_smallValue];

    public int CompareTo(IBigInteger? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (other is BetterBigInteger otherInt)
        {
            return Compare(this, otherInt);
        }

        return CompareViaInterface(this, other);
    }

    public bool Equals(IBigInteger? other)
    {
        if (other is null)
        {
            return false;
        }

        if (other is not BetterBigInteger otherInt)
        {
            return false;
        }

        return _signBit == otherInt._signBit && MagnitudesEqual(GetDigits(), otherInt.GetDigits());
    }

    public override bool Equals(object? obj) => obj is IBigInteger other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(_signBit);
        foreach (uint digit in GetDigits())
        {
            hash.Add(digit);
        }

        return hash.ToHashCode();
    }

    #endregion

    #region Арифметические операторы

    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (IsFastZero(a))
        {
            return b;
        }

        if (IsFastZero(b))
        {
            return a;
        }

        // Одинаковый знак — складываем модули
        if (a.IsNegative == b.IsNegative)
        {
            return FromMagnitude(AddMagnitudes(a.GetDigits(), b.GetDigits()), a.IsNegative);
        }

        // Разный знак — это вычитание модулей
        int cmp = CompareMagnitude(a.GetDigits(), b.GetDigits());
        if (cmp == 0)
        {
            return Zero;
        }

        if (cmp > 0)
        {
            return FromMagnitude(SubtractMagnitudes(a.GetDigits(), b.GetDigits()), a.IsNegative);
        }

        return FromMagnitude(SubtractMagnitudes(b.GetDigits(), a.GetDigits()), b.IsNegative);
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b) => a + (-b);

    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (IsZeroMagnitude(a.GetDigits()))
        {
            return Zero;
        }

        // Меняем знак, сами цифры не трогаем
        return new BetterBigInteger(a._signBit ^ 1, a._smallValue, a._data);
    }

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        (uint[] quotient, _) = DivModMagnitudes(a.GetDigits(), b.GetDigits());
        // Знак частного: минус, если знаки операндов разные
        bool negative = a.IsNegative != b.IsNegative;
        return FromMagnitude(quotient, negative && !IsZeroMagnitude(quotient));
    }

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        (_, uint[] remainder) = DivModMagnitudes(a.GetDigits(), b.GetDigits());
        // Остаток сохраняет знак делимого
        return FromMagnitude(remainder, a.IsNegative && !IsZeroMagnitude(remainder));
    }

    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (IsFastZero(a) || IsFastZero(b))
        {
            return Zero;
        }

        int maxLength = Math.Max(a.GetDigits().Length, b.GetDigits().Length);
        IMultiplier multiplier = maxLength switch
        {
            < KaratsubaThreshold => new SimpleMultiplier(),
            < FftThreshold => new KaratsubaMultiplier(),
            _ => new FftMultiplier()
        };

        return multiplier.Multiply(a, b);
    }

    #endregion

    #region Побитовые операторы и сдвиги

    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        ArgumentNullException.ThrowIfNull(a);
        // Побитовое NOT: ~a = -(a + 1)
        return -(a + One);
    }

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return ApplyBitwise(a, b, static (x, y) => x & y);
    }

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return ApplyBitwise(a, b, static (x, y) => x | y);
    }

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return ApplyBitwise(a, b, static (x, y) => x ^ y);
    }

    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentOutOfRangeException.ThrowIfNegative(shift);

        if (shift == 0 || IsFastZero(a))
        {
            return a;
        }

        int digitShift = shift / 32;
        int bitShift = shift % 32;
        ReadOnlySpan<uint> data = a.GetDigits();
        uint[] newData = new uint[data.Length + digitShift + 1];

        uint carry = 0;
        for (int i = 0; i < data.Length; i++)
        {
            uint tmpCarry = data[i] >> (32 - bitShift);
            uint newValue = (data[i] << bitShift) | carry;
            carry = tmpCarry == data[i] ? 0u : tmpCarry;
            newData[i + digitShift] = newValue;
        }

        if (carry != 0)
        {
            newData[^1] = carry;
        }

        return FromMagnitude(newData, a.IsNegative);
    }

    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentOutOfRangeException.ThrowIfNegative(shift);

        if (shift == 0 || IsFastZero(a))
        {
            return a;
        }

        int digitShift = shift / 32;
        int bitShift = shift % 32;
        ReadOnlySpan<uint> data = a.GetDigits();

        if (digitShift >= data.Length)
        {
            return a.IsNegative ? -One : Zero;
        }

        // сдвиг отрицательного -((|a| - 1) >> k) - 1
        if (a.IsNegative)
        {
            BetterBigInteger absMinusOne = (-a) - One;
            return -((absMinusOne >> shift)) - One;
        }

        uint[] newData = new uint[data.Length - digitShift];

        if (bitShift == 0)
        {
            for (int i = 0; i < newData.Length; i++)
            {
                newData[i] = data[i + digitShift];
            }
        }
        else
        {
            uint carry = 0;
            int invShift = 32 - bitShift;

            for (int i = data.Length - 1; i >= 0; i--)
            {
                uint current = (data[i] >> bitShift) | carry;
                carry = data[i] << invShift;

                if (i >= digitShift)
                {
                    newData[i - digitShift] = current;
                }
            }
        }

        return FromMagnitude(newData, false);
    }

    #endregion

    #region Операторы отношения

    public static bool operator ==(BetterBigInteger? a, BetterBigInteger? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.Equals(b);
    }

    public static bool operator !=(BetterBigInteger? a, BetterBigInteger? b) => !(a == b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;

    #endregion

    #region ToString

    public override string ToString() => ToString(10);

    public string ToString(int radix)
    {
        if (radix is < 2 or > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36.");
        }

        if (IsZeroMagnitude(GetDigits()))
        {
            return "0";
        }

        StringBuilder builder = new();
        if (IsNegative)
        {
            builder.Append('-');
        }

        // Делим число на radix, остатки — цифры строки (от младшей к старшей)
        uint[] magnitude = GetDigits().ToArray();
        int insertIndex = IsNegative ? 1 : 0;
        while (!IsZeroMagnitude(magnitude))
        {
            (uint[] quotient, uint[] remainder) = DivModMagnitudes(magnitude, [(uint)radix]);
            // Вставляем в начало, чтобы цифры шли в правильном порядке
            builder.Insert(insertIndex, DigitToChar(remainder[0]));
            magnitude = quotient;
        }

        if (builder.Length == 0 || (IsNegative && builder.Length == 1))
        {
            return IsNegative ? "-0" : "0";
        }

        return builder.ToString();
    }

    #endregion

    #region Операции над модулями

    internal static BetterBigInteger FromMagnitude(uint[] magnitude, bool isNegative)
    {
        magnitude = NormalizeLittleEndian(magnitude);
        bool zero = IsZeroMagnitude(magnitude);
        int signBit = zero ? 0 : isNegative ? 1 : 0;

        // Одно слово - кладём в _smallValue, массив не нужен
        if (magnitude.Length == 1)
        {
            return new BetterBigInteger(signBit, magnitude[0], null);
        }

        return new BetterBigInteger(signBit, 0, magnitude);
    }

    internal static uint[] NormalizeLittleEndian(uint[] digits)
    {
        // Убираем лишние нулевые старшие разряды
        int length = digits.Length;
        while (length > 1 && digits[length - 1] == 0)
        {
            length--;
        }

        if (length == digits.Length)
        {
            return digits;
        }

        uint[] trimmed = new uint[length];
        Array.Copy(digits, trimmed, length);
        return trimmed;
    }

    internal static int CompareMagnitude(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        if (a.Length != b.Length)
        {
            return a.Length.CompareTo(b.Length);
        }

        // Сравниваем с старших разрядов (little-endian: с конца массива)
        for (int i = a.Length - 1; i >= 0; i--)
        {
            if (a[i] != b[i])
            {
                return a[i].CompareTo(b[i]);
            }
        }

        return 0;
    }

    internal static uint[] AddMagnitudes(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int length = Math.Max(a.Length, b.Length);
        uint[] result = new uint[length + 1];
        ulong carry = 0;

        for (int i = 0; i < length; i++)
        {
            ulong sum = carry;
            if (i < a.Length)
            {
                sum += a[i];
            }

            if (i < b.Length)
            {
                sum += b[i];
            }

            result[i] = (uint)sum;
            carry = sum >> 32; // перенос в следующий разряд
        }

        if (carry != 0)
        {
            result[length] = (uint)carry;
        }

        return NormalizeLittleEndian(result);
    }

    // Вычитание модулей: a >= b (проверяется вызывающим кодом)
    internal static uint[] SubtractMagnitudes(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        uint[] result = new uint[a.Length];
        ulong borrow = 0;

        for (int i = 0; i < a.Length; i++)
        {
            ulong subtrahend = i < b.Length ? b[i] : 0;
            ulong diff = a[i] - subtrahend - borrow;
            result[i] = (uint)diff;
            borrow = (diff >> 32) & 1;
        }

        return NormalizeLittleEndian(result);
    }

    // Вычитание с флагом заёма (для алгоритма Кнута, когда qHat может быть завышен)
    internal static (uint[] Result, bool Borrow) SubtractMagnitudesChecked(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        uint[] result = new uint[a.Length];
        ulong borrow = 0;

        for (int i = 0; i < a.Length; i++)
        {
            ulong subtrahend = i < b.Length ? b[i] : 0;
            ulong diff = a[i] - subtrahend - borrow;
            result[i] = (uint)diff;
            borrow = (diff >> 32) & 1;
        }

        return (result, borrow != 0);
    }

    // Умножение модуля на одну цифру
    internal static uint[] MultiplyMagnitudeByDigit(ReadOnlySpan<uint> magnitude, uint digit)
    {
        if (digit == 0)
        {
            return [0];
        }

        uint[] result = new uint[magnitude.Length + 1];
        ulong carry = 0;

        for (int i = 0; i < magnitude.Length; i++)
        {
            ulong product = (ulong)magnitude[i] * digit + carry;
            result[i] = (uint)product;
            carry = product >> 32;
        }

        if (carry != 0)
        {
            result[magnitude.Length] = (uint)carry;
        }

        return NormalizeLittleEndian(result);
    }

    private static uint[] PadMagnitude(ReadOnlySpan<uint> digits, int length)
    {
        uint[] padded = new uint[length];
        digits.Slice(0, Math.Min(digits.Length, length)).CopyTo(padded);
        return padded;
    }

    private static void AddMagnitudesInPlace(uint[] buffer, int offset, ReadOnlySpan<uint> addend)
    {
        uint[] sum = AddMagnitudes(buffer.AsSpan(offset, addend.Length), addend);

        for (int i = 0; i < addend.Length; i++)
        {
            buffer[offset + i] = i < sum.Length ? sum[i] : 0;
        }

        if (sum.Length > addend.Length)
        {
            buffer[offset + addend.Length] += sum[addend.Length];
        }
    }

    // Деление модулей, возвращает частное и остаток
    internal static (uint[] quotient, uint[] remainder) DivModMagnitudes(
    ReadOnlySpan<uint> dividend,
    ReadOnlySpan<uint> divisor)
    {
        // Деление на 0 запрещено
        if (IsZeroMagnitude(divisor))
        {
            throw new DivideByZeroException();
        }

        // Если делитель больше делимого
        // меньшее / большее = 0 остаток x
        if (CompareMagnitude(dividend, divisor) < 0)
        {
            return ([0], NormalizeLittleEndian(dividend.ToArray()));
        }

        // Деление на одно слово (на uint)
        if (divisor.Length == 1)
        {
            return DivModBySingleDigit(dividend, divisor[0]);
        }

        // Алгоритм Кнута, основание 2^32

        // D1 нормализация — старшая цифра делителя >= b/2 (2^31)
        uint divisorHigh = divisor[divisor.Length - 1];
        uint d = 1;
        ReadOnlySpan<uint> normalizedDivisor = divisor;

        if (divisorHigh < (1U << 31))
        {
            d = (uint)((1UL << 32) / (divisorHigh + 1UL));
            normalizedDivisor = MultiplyMagnitudeByDigit(divisor, d);
        }

        int normalizedDivisorLength = normalizedDivisor.Length;

        uint[] scaledDividend = d == 1
            ? dividend.ToArray()
            : MultiplyMagnitudeByDigit(dividend, d);

        // Копия нормализованного делимого + слово для переносов (u_m = 0)
        uint[] remainder = new uint[scaledDividend.Length + 1];
        scaledDividend.CopyTo(remainder);

        int quotientLength = scaledDividend.Length - normalizedDivisorLength + 1;
        uint[] quotient = new uint[quotientLength];

        divisorHigh = normalizedDivisor[normalizedDivisorLength - 1];
        ulong divisorSecond = normalizedDivisorLength > 1 ? normalizedDivisor[normalizedDivisorLength - 2] : 0;

        // D2–D7 вычисляем цифры частного начиная со старшего
        for (int j = quotientLength - 1; j >= 0; j--)
        {
            ulong numeratorHigh = remainder[j + normalizedDivisorLength];
            ulong numeratorMid = remainder[j + normalizedDivisorLength - 1];
            ulong numeratorLow = j + normalizedDivisorLength >= 2 ? remainder[j + normalizedDivisorLength - 2] : 0;

            ulong qHat = numeratorHigh == divisorHigh
                ? uint.MaxValue
                : ((numeratorHigh << 32) + numeratorMid) / divisorHigh;

            ulong rHat = ((numeratorHigh << 32) + numeratorMid) % divisorHigh;

            while (
                qHat == uint.MaxValue + 1UL || qHat * divisorSecond > (rHat << 32) + numeratorLow)
            {
                qHat--;
                rHat += divisorHigh;
                if (rHat >= 1UL << 32)
                {
                    break;
                }
            }

            int windowLength = normalizedDivisorLength + 1;
            uint[] window = remainder.AsSpan(j, windowLength).ToArray();
            uint[] product = MultiplyMagnitudeByDigit(normalizedDivisor, (uint)qHat);
            (uint[] newWindow, bool underflow) = SubtractMagnitudesChecked(
                window,
                PadMagnitude(product, windowLength));

            newWindow.CopyTo(remainder.AsSpan(j, windowLength));

            if (underflow)
            {
                qHat--;
                AddMagnitudesInPlace(remainder, j, normalizedDivisor);
            }

            quotient[j] = (uint)qHat;
        }

        // D8: денормализация остатка (делим на d)
        uint[] finalRemainder = remainder.AsSpan(0, normalizedDivisorLength).ToArray();
        if (d > 1)
        {
            (_, finalRemainder) = DivModBySingleDigit(finalRemainder, d);
        }

        return (
            NormalizeLittleEndian(quotient),
            NormalizeLittleEndian(finalRemainder));
    }

    #endregion

    #region Инициализация

    private static readonly BetterBigInteger Zero = new([0], false);
    private static readonly BetterBigInteger One = new([1], false);

    // Заполняет поля с учётом оптимизации для одного разряда
    private void InitializeFromDigits(uint[] digits, bool isNegative)
    {
        bool isZero = IsZeroMagnitude(digits);
        _signBit = isZero ? 0 : isNegative ? 1 : 0;

        if (digits.Length == 1)
        {
            _smallValue = digits[0];
            _data = null;
            return;
        }

        _smallValue = 0;
        _data = digits;
    }

    #endregion

    #region Вспомогательные методы

    // Ноль в формате без массива и значение 0
    private static bool IsFastZero(BetterBigInteger value) =>
        value._data is null && value._smallValue == 0;

    private static int Compare(BetterBigInteger a, BetterBigInteger b) =>
        CompareViaInterface(a, b);

    // Сравнение через контракт IBigInteger (знак + модуль)
    private static int CompareViaInterface(IBigInteger a, IBigInteger b)
    {
        if (a.IsNegative != b.IsNegative)
        {
            return a.IsNegative ? -1 : 1;
        }

        int magnitudeCompare = CompareMagnitude(a.GetDigits(), b.GetDigits());
        return a.IsNegative ? -magnitudeCompare : magnitudeCompare;
    }

    private static bool MagnitudesEqual(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsZeroMagnitude(ReadOnlySpan<uint> digits)
    {
        for (int i = 0; i < digits.Length; i++)
        {
            if (digits[i] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetDigitValue(char c, int radix)
    {
        int value = char.IsDigit(c)
            ? c - '0'
            : char.ToUpperInvariant(c) - 'A' + 10;

        if (value < 0 || value >= radix)
        {
            throw new FormatException($"Invalid digit '{c}' for radix {radix}.");
        }

        return value;
    }

    private static char DigitToChar(uint digit) =>
        digit < 10 ? (char)('0' + digit) : (char)('A' + digit - 10);

    // result = result * multiplier + addend, всё за один проход
    private static void MultiplyAndAdd(List<uint> digits, uint multiplier, uint addend)
    {
        ulong carry = addend;

        for (int i = 0; i < digits.Count; i++)
        {
            carry += (ulong)digits[i] * multiplier;
            digits[i] = (uint)carry;
            carry >>= 32;
        }

        while (carry != 0)
        {
            digits.Add((uint)carry);
            carry >>= 32;
        }
    }

    // Быстрое деление, когда делитель — одно 32-битное число
    private static (uint[] quotient, uint[] remainder) DivModBySingleDigit(ReadOnlySpan<uint> dividend, uint divisor)
    {
        uint[] quotient = new uint[dividend.Length];
        ulong remainder = 0;

        // Идём от старших разрядов к младшим
        for (int i = dividend.Length - 1; i >= 0; i--)
        {
            ulong current = (remainder << 32) | dividend[i];
            quotient[i] = (uint)(current / divisor);
            remainder = current % divisor;
        }

        return (NormalizeLittleEndian(quotient), [(uint)remainder]);
    }

    #endregion

    #region Побитовая логика

    /*
     *      sign+magnitude
     *      перевод в two's complement
     *      выравнивание длины представления
     *      выполнение операции над словами
     *      перевод обратно в sign+magnitude
     */

    // Побитовая операция работает в дополнительном коде одинаковой длины
    private static BetterBigInteger ApplyBitwise(
        BetterBigInteger a,
        BetterBigInteger b,
        Func<uint, uint, uint> operation)
    {
        int bitCount = Math.Max(GetSignedBitCount(a), GetSignedBitCount(b));
        if (bitCount == 0)
        {
            return Zero;
        }

        int wordCount = (bitCount + 31) / 32;
        uint[] left = ToTwosComplementWords(a, wordCount);
        uint[] right = ToTwosComplementWords(b, wordCount);
        uint[] result = new uint[wordCount];

        for (int i = 0; i < wordCount; i++)
        {
            result[i] = operation(left[i], right[i]);
        }

        return FromTwosComplement(NormalizeTwosComplement(result));
    }

    // Дополняет представление до нужного числа слов (знак расширяется)
    private static uint[] ToTwosComplementWords(BetterBigInteger value, int wordCount)
    {
        uint[] words = new uint[wordCount];
        uint[] twos = ToTwosComplement(value);
        twos.CopyTo(words, 0);
        // Отрицательные дополняем единицами, положительные — нулями
        uint fill = value.IsNegative ? uint.MaxValue : 0;

        for (int i = twos.Length; i < wordCount; i++)
        {
            words[i] = fill;
        }

        return words;
    }

    // Сколько бит нужно для представления числа со знаком
    private static int GetSignedBitCount(BetterBigInteger value)
    {
        int magnitudeBits = GetMagnitudeBitCount(value.GetDigits());
        if (magnitudeBits == 0)
        {
            return 0;
        }

        // У отрицательного всегда есть знаковый бит
        if (value.IsNegative)
        {
            return magnitudeBits + 1;
        }

        // У положительного — если старший бит модуля уже занят, нужен ещё один нулевой бит сверху
        if ((value.GetDigits()[^1] & 0x80000000) != 0)
        {
            return magnitudeBits + 1;
        }

        return magnitudeBits;
    }

    private static int GetMagnitudeBitCount(ReadOnlySpan<uint> digits)
    {
        if (IsZeroMagnitude(digits))
        {
            return 0;
        }

        return (digits.Length - 1) * 32 + (32 - BitOperations.LeadingZeroCount(digits[^1]));
    }

    // Перевод модуля в дополнительный код: ~magnitude + 1
    private static uint[] ToTwosComplement(BetterBigInteger value)
    {
        if (IsZeroMagnitude(value.GetDigits()))
        {
            return [0];
        }

        if (!value.IsNegative)
        {
            return value.GetDigits().ToArray();
        }

        uint[] magnitude = value.GetDigits().ToArray();
        uint[] inverted = new uint[magnitude.Length];
        for (int i = 0; i < magnitude.Length; i++)
        {
            inverted[i] = ~magnitude[i];
        }

        return AddMagnitudes(inverted, [1]);
    }

    // Обратный перевод: из дополнительного кода обратно в знак + модуль
    private static BetterBigInteger FromTwosComplement(uint[] digits)
    {
        digits = NormalizeTwosComplement(digits);
        if (IsZeroMagnitude(digits))
        {
            return Zero;
        }

        bool negative = (digits[^1] & 0x80000000) != 0;
        if (!negative)
        {
            return FromMagnitude(digits, false);
        }

        uint[] inverted = new uint[digits.Length];
        for (int i = 0; i < digits.Length; i++)
        {
            inverted[i] = ~digits[i];
        }

        uint[] magnitude = AddMagnitudes(inverted, [1]);
        return FromMagnitude(magnitude, true);
    }

    // Убирает лишние старшие разряды, которые только повторяют знак
    private static uint[] NormalizeTwosComplement(uint[] digits)
    {
        if (digits.Length == 0)
        {
            return [0];
        }

        if (digits.Length == 1)
        {
            return digits;
        }

        bool negative = (digits[^1] & 0x80000000) != 0;
        int length = digits.Length;

        while (length > 1)
        {
            uint high = digits[length - 1];
            uint lower = digits[length - 2];

            // Лишние единицы сверху у отрицательного числа
            if (negative)
            {
                if (high == uint.MaxValue && (lower & 0x80000000) != 0)
                {
                    length--;
                    continue;
                }
            }
            // Лишние нули сверху у положительного числа
            else if (high == 0 && (lower & 0x80000000) == 0)
            {
                length--;
                continue;
            }

            break;
        }

        if (length == digits.Length)
        {
            return digits;
        }

        uint[] trimmed = new uint[length];
        Array.Copy(digits, trimmed, length);
        return trimmed;
    }

    #endregion
}

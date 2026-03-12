// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Math
{
    [GoPackage("math")]
    public static class Package
    {
        [GoFunc]
        public static double Abs(double x) => System.Math.Abs(x);
        [GoFunc]
        public static double Max(double x, double y) => System.Math.Max(x, y);
        [GoFunc]
        public static double Min(double x, double y) => System.Math.Min(x, y);
        [GoFunc]
        public static double Sqrt(double x) => System.Math.Sqrt(x);
        [GoFunc]
        public static double Floor(double x) => System.Math.Floor(x);
        [GoFunc]
        public static double Ceil(double x) => System.Math.Ceiling(x);
        [GoFunc]
        public static double Round(double x) => System.Math.Round(x);
        [GoFunc]
        public static double Pow(double x, double y) => System.Math.Pow(x, y);
        [GoFunc]
        public static double Log(double x) => System.Math.Log(x);
        [GoFunc]
        public static double Log2(double x) => System.Math.Log2(x);
        [GoFunc]
        public static double Log10(double x) => System.Math.Log10(x);
        [GoFunc]
        public static double Exp(double x) => System.Math.Exp(x);
        [GoFunc]
        public static double Mod(double x, double y) => x % y;
        [GoFunc]
        public static double Sin(double x) => System.Math.Sin(x);
        [GoFunc]
        public static double Cos(double x) => System.Math.Cos(x);
        [GoFunc]
        public static double Tan(double x) => System.Math.Tan(x);
        [GoFunc]
        public static double Atan(double x) => System.Math.Atan(x);
        [GoFunc]
        public static double Atan2(double y, double x) => System.Math.Atan2(y, x);
        [GoFunc]
        public static double Inf(long sign) => sign >= 0 ? double.PositiveInfinity : double.NegativeInfinity;
        [GoFunc]
        public static bool IsNaN(double x) => double.IsNaN(x);
        [GoFunc]
        public static bool IsInf(double x, long sign) =>
            sign > 0 ? double.IsPositiveInfinity(x) :
            sign < 0 ? double.IsNegativeInfinity(x) :
            double.IsInfinity(x);
        [GoFunc]
        public static double NaN() => double.NaN;
        [GoFunc]
        public static double Remainder(double x, double y) => System.Math.IEEERemainder(x, y);
        [GoFunc]
        public static double Trunc(double x) => System.Math.Truncate(x);
        [GoFunc]
        public static double Pow10(long n) => System.Math.Pow(10, n);
        [GoFunc]
        public static double Asin(double x) => System.Math.Asin(x);
        [GoFunc]
        public static double Acos(double x) => System.Math.Acos(x);
        [GoFunc]
        public static double Sinh(double x) => System.Math.Sinh(x);
        [GoFunc]
        public static double Cosh(double x) => System.Math.Cosh(x);
        [GoFunc]
        public static double Tanh(double x) => System.Math.Tanh(x);
        [GoFunc]
        public static double Cbrt(double x) => System.Math.Cbrt(x);
        [GoFunc]
        public static double Hypot(double p, double q) => System.Math.Sqrt(p * p + q * q);
        [GoFunc]
        public static double Dim(double x, double y) => System.Math.Max(x - y, 0);
        [GoFunc]
        public static double Copysign(double x, double y)
            => System.Math.CopySign(x, y);
        [GoFunc]
        public static double Ldexp(double frac, long exp)
            => frac * System.Math.Pow(2, exp);
        [GoFunc]
        public static double Logb(double x) => System.Math.Log2(System.Math.Abs(x));
        [GoFunc]
        public static bool Signbit(double x) => double.IsNegative(x);
        [GoFunc]
        public static long Ilogb(double x) => (long)System.Math.Floor(System.Math.Log2(System.Math.Abs(x)));

        [GoFunc]
        public static (double, long) Frexp(double f)
        {
            if (f == 0) return (0, 0);
            if (double.IsInfinity(f) || double.IsNaN(f)) return (f, 0);
            long bits = BitConverter.DoubleToInt64Bits(f);
            int exp = (int)((bits >> 52) & 0x7FF) - 1022;
            bits = (bits & unchecked((long)0x800FFFFFFFFFFFFF)) | 0x3FE0000000000000;
            return (BitConverter.Int64BitsToDouble(bits), exp);
        }

        [GoFunc]
        public static double Float64frombits(long bits)
        {
            return BitConverter.Int64BitsToDouble(bits);
        }

        [GoFunc]
        public static long Float64bits(double f)
        {
            return BitConverter.DoubleToInt64Bits(f);
        }

        [GoFunc]
        public static float Float32frombits(long bits)
        {
            return BitConverter.Int32BitsToSingle((int)bits);
        }

        [GoFunc]
        public static long Float32bits(float f)
        {
            return BitConverter.SingleToInt32Bits(f);
        }

        // Constants
        [GoConst]
        public static readonly double Pi = System.Math.PI;
        [GoConst]
        public static readonly double E = System.Math.E;
        [GoConst]
        public static readonly double MaxFloat64 = double.MaxValue;
        [GoConst]
        public static readonly double SmallestNonzeroFloat64 = double.Epsilon;
        [GoConst]
        public static readonly double SmallestNonzeroFloat32 = float.Epsilon;
        [GoConst]
        public static readonly long MaxInt = long.MaxValue;
        [GoConst]
        public static readonly long MinInt = long.MinValue;
        [GoConst]
        public static readonly long MaxInt8 = sbyte.MaxValue;
        [GoConst]
        public static readonly long MinInt8 = sbyte.MinValue;
        [GoConst]
        public static readonly long MaxInt16 = short.MaxValue;
        [GoConst]
        public static readonly long MinInt16 = short.MinValue;
        [GoConst]
        public static readonly long MaxInt32 = int.MaxValue;
        [GoConst]
        public static readonly long MinInt32 = int.MinValue;
        [GoConst]
        public static readonly long MaxInt64 = long.MaxValue;
        [GoConst]
        public static readonly long MinInt64 = long.MinValue;
        [GoConst]
        public static readonly ulong MaxUint8 = byte.MaxValue;
        [GoConst]
        public static readonly ulong MaxUint16 = ushort.MaxValue;
        [GoConst]
        public static readonly ulong MaxUint32 = uint.MaxValue;
        [GoConst]
        public static readonly ulong MaxUint64 = ulong.MaxValue;
        [GoConst]
        public static readonly double MaxFloat32 = float.MaxValue;
        [GoConst]
        public static readonly double Phi = (1 + System.Math.Sqrt(5)) / 2;
        [GoConst]
        public static readonly double Sqrt2 = System.Math.Sqrt(2);
        [GoConst]
        public static readonly double SqrtE = System.Math.Sqrt(System.Math.E);
        [GoConst]
        public static readonly double SqrtPi = System.Math.Sqrt(System.Math.PI);
        [GoConst]
        public static readonly double SqrtPhi = System.Math.Sqrt((1 + System.Math.Sqrt(5)) / 2);
        [GoConst]
        public static readonly double Ln2 = System.Math.Log(2);
        [GoConst]
        public static readonly double Log2E = 1.0 / System.Math.Log(2);
        [GoConst]
        public static readonly double Ln10 = System.Math.Log(10);
        [GoConst]
        public static readonly double Log10E = 1.0 / System.Math.Log(10);

        public static double Asinh(double x) => System.Math.Asinh(x);
        public static double Acosh(double x) => System.Math.Acosh(x);
        public static double Atanh(double x) => System.Math.Atanh(x);
        public static (double, double) Sincos(double x) => (System.Math.Sin(x), System.Math.Cos(x));
        public static double Erf(double x)
        {
            // Approximation of error function
            double a1 =  0.254829592, a2 = -0.284496736, a3 = 1.421413741;
            double a4 = -1.453152027, a5 = 1.061405429, p  = 0.3275911;
            int sign = x < 0 ? -1 : 1;
            x = System.Math.Abs(x);
            double t = 1.0 / (1.0 + p * x);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * System.Math.Exp(-x * x);
            return sign * y;
        }
        public static double Erfc(double x) => 1.0 - Erf(x);
        public static double J0(double x) => 0; // Bessel stub
        public static double J1(double x) => 0; // Bessel stub
        public static double Jn(long n, double x) => 0; // Bessel stub
        public static double Y0(double x) => 0; // Bessel stub
        public static double Y1(double x) => 0; // Bessel stub
        public static double Yn(long n, double x) => 0; // Bessel stub
        public static double Gamma(double x) => System.Math.Exp(LogGamma(x));
        public static double Lgamma(double x) { var (r, _) = LgammaFull(x); return r; }
        private static (double, long) LgammaFull(double x)
        {
            if (double.IsInfinity(x)) return (double.PositiveInfinity, 1);
            return (LogGamma(x), x >= 0 ? 1 : -1);
        }
        private static double LogGamma(double x)
        {
            if (x <= 0) return double.PositiveInfinity;
            // Stirling's approximation
            return 0.5 * System.Math.Log(2 * System.Math.PI / x) + x * (System.Math.Log(x + 1.0 / (12.0 * x - 1.0 / (10.0 * x))) - 1.0);
        }
        public static double Nextafter(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            if (x == y) return x;
            if (x == 0) return y > 0 ? double.Epsilon : -double.Epsilon;
            long bits = BitConverter.DoubleToInt64Bits(x);
            if ((x > 0) == (y > x)) bits++; else bits--;
            return BitConverter.Int64BitsToDouble(bits);
        }
        public static double Nextafter32(double x, double y) => Nextafter(x, y);
        public static double FMA(double x, double y, double z) => System.Math.FusedMultiplyAdd(x, y, z);
        public static double RoundToEven(double x)
        {
            return System.Math.Round(x, MidpointRounding.ToEven);
        }
        public static (double, double) Modf(double f)
        {
            double intPart = System.Math.Truncate(f);
            return (intPart, f - intPart);
        }
    }
}

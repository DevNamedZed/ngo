// -----------------------------------------------------------------------
// <copyright file="GoMath.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class GoMath
    {
        public static double Abs(double x) => Math.Abs(x);
        public static double Max(double x, double y) => Math.Max(x, y);
        public static double Min(double x, double y) => Math.Min(x, y);
        public static double Sqrt(double x) => Math.Sqrt(x);
        public static double Floor(double x) => Math.Floor(x);
        public static double Ceil(double x) => Math.Ceiling(x);
        public static double Round(double x) => Math.Round(x);
        public static double Pow(double x, double y) => Math.Pow(x, y);
        public static double Log(double x) => Math.Log(x);
        public static double Log2(double x) => Math.Log2(x);
        public static double Log10(double x) => Math.Log10(x);
        public static double Exp(double x) => Math.Exp(x);
        public static double Mod(double x, double y) => x % y;
        public static double Sin(double x) => Math.Sin(x);
        public static double Cos(double x) => Math.Cos(x);
        public static double Tan(double x) => Math.Tan(x);
        public static double Atan(double x) => Math.Atan(x);
        public static double Atan2(double y, double x) => Math.Atan2(y, x);
        public static double Inf(long sign) => sign >= 0 ? double.PositiveInfinity : double.NegativeInfinity;
        public static bool IsNaN(double x) => double.IsNaN(x);
        public static bool IsInf(double x, long sign) =>
            sign > 0 ? double.IsPositiveInfinity(x) :
            sign < 0 ? double.IsNegativeInfinity(x) :
            double.IsInfinity(x);
        public static double NaN() => double.NaN;
        public static double Remainder(double x, double y) => Math.IEEERemainder(x, y);
        public static double Trunc(double x) => Math.Truncate(x);
        public static double Pow10(long n) => Math.Pow(10, n);
        public static double Asin(double x) => Math.Asin(x);
        public static double Acos(double x) => Math.Acos(x);
        public static double Sinh(double x) => Math.Sinh(x);
        public static double Cosh(double x) => Math.Cosh(x);
        public static double Tanh(double x) => Math.Tanh(x);
        public static double Cbrt(double x) => Math.Cbrt(x);
        public static double Hypot(double p, double q) => Math.Sqrt(p * p + q * q);
        public static double Dim(double x, double y) => Math.Max(x - y, 0);
        public static double Copysign(double x, double y)
            => Math.CopySign(x, y);
        public static double Ldexp(double frac, long exp)
            => frac * Math.Pow(2, exp);
        public static double Logb(double x) => Math.Log2(Math.Abs(x));
        public static long Ilogb(double x) => (long)Math.Floor(Math.Log2(Math.Abs(x)));

        // Constants
        public static readonly double Pi = Math.PI;
        public static readonly double E = Math.E;
        public static readonly double MaxFloat64 = double.MaxValue;
        public static readonly double SmallestNonzeroFloat64 = double.Epsilon;
        public static readonly long MaxInt = long.MaxValue;
        public static readonly long MinInt = long.MinValue;
        public static readonly long MaxInt8 = sbyte.MaxValue;
        public static readonly long MinInt8 = sbyte.MinValue;
        public static readonly long MaxInt16 = short.MaxValue;
        public static readonly long MinInt16 = short.MinValue;
        public static readonly long MaxInt32 = int.MaxValue;
        public static readonly long MinInt32 = int.MinValue;
        public static readonly long MaxInt64 = long.MaxValue;
        public static readonly long MinInt64 = long.MinValue;
        public static readonly ulong MaxUint8 = byte.MaxValue;
        public static readonly ulong MaxUint16 = ushort.MaxValue;
        public static readonly ulong MaxUint32 = uint.MaxValue;
        public static readonly ulong MaxUint64 = ulong.MaxValue;
        public static readonly double MaxFloat32 = float.MaxValue;
        public static readonly double Phi = (1 + Math.Sqrt(5)) / 2;
        public static readonly double Sqrt2 = Math.Sqrt(2);
        public static readonly double SqrtE = Math.Sqrt(Math.E);
        public static readonly double SqrtPi = Math.Sqrt(Math.PI);
        public static readonly double SqrtPhi = Math.Sqrt((1 + Math.Sqrt(5)) / 2);
        public static readonly double Ln2 = Math.Log(2);
        public static readonly double Log2E = 1.0 / Math.Log(2);
        public static readonly double Ln10 = Math.Log(10);
        public static readonly double Log10E = 1.0 / Math.Log(10);
    }
}

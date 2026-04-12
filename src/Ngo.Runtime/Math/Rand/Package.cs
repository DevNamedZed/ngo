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

namespace Ngo.Runtime.Math.Rand
{
    [GoPackage("math/rand")]
    public static class Package
    {
        private static System.Random _random = new System.Random();

        [GoFunc]
        [return: GoReturn("Source")]
        public static object NewSource(long seed)
        {
            return new GoRandSource(seed);
        }

        [GoFunc]
        [return: GoReturn("*Rand")]
        public static GoRand New([GoParam("Source")] object source)
        {
            IGoRandSource resolvedSource = source as IGoRandSource ?? new GoRandSource(1);
            return new GoRand(resolvedSource);
        }

        [GoFunc]
        public static void Shuffle(long n, [GoParam("func(i, j int)")] Action<long, long> swap)
        {
            if (swap == null || n < 2)
            {
                return;
            }
            for (long i = n - 1; i > 0; i--)
            {
                long j = unchecked((long)(_random.NextDouble() * (i + 1)));
                swap(i, j);
            }
        }

        [GoFunc]
        public static (long, object?) Read(Slice<byte> buffer)
        {
            int length = buffer.Len;
            for (int i = 0; i < length; i++)
            {
                buffer[i] = (byte)_random.Next(256);
            }
            return (length, null);
        }

        [GoFunc]
        public static void Seed(long seed)
        {
            _random = new System.Random((int)seed);
        }

        [GoFunc]
        public static long Intn(long n)
        {
            return _random.Next((int)n);
        }

        [GoFunc]
        public static double Float64()
        {
            return _random.NextDouble();
        }

        [GoFunc]
        public static float Float32()
        {
            return (float)_random.NextDouble();
        }

        [GoFunc]
        public static long Int63()
        {
            return (long)(_random.NextDouble() * long.MaxValue);
        }

        [GoFunc]
        public static long Int()
        {
            return Int63();
        }

        [GoFunc]
        public static long Int31()
        {
            return _random.Next();
        }

        [GoFunc]
        public static long Int63n(long n)
        {
            return (long)(_random.NextDouble() * n);
        }

        [GoFunc]
        public static long Int31n(long n)
        {
            return _random.Next((int)n);
        }

        [GoFunc]
        public static long Uint32()
        {
            return (long)(uint)_random.Next();
        }

        [GoFunc]
        public static long Uint64()
        {
            return (long)((ulong)(_random.NextDouble() * ulong.MaxValue));
        }

        [GoFunc]
        public static Slice<long> Perm(long n)
        {
            var result = new long[(int)n];
            for (int i = 0; i < (int)n; i++)
            {
                result[i] = i;
            }
            for (int i = (int)n - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return new Slice<long>(result);
        }

        [GoFunc]
        public static double NormFloat64()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = _random.NextDouble();
            return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2);
        }

        [GoFunc]
        public static double ExpFloat64()
        {
            return -System.Math.Log(1.0 - _random.NextDouble());
        }
    }
}

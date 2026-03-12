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

namespace Ngo.Runtime.Rand
{
    [GoPackage("math/rand")]
    public static class Package
    {
        [ThreadStatic]
        private static Random? _rng;

        private static Random Rng => _rng ??= new Random();

        // rand.Intn(n int) int
        public static long Intn(long n)
        {
            return (long)Rng.Next((int)n);
        }

        // rand.Float64() float64
        public static double Float64()
        {
            return Rng.NextDouble();
        }

        // rand.Int() int
        public static long Int()
        {
            return (long)Rng.Next();
        }

        // rand.Seed(seed int64)
        public static void Seed(long seed)
        {
            _rng = new Random((int)seed);
        }

        // rand.Int63n(n int64) int64
        public static long Int63n(long n)
        {
            if (n <= 0) throw new GoPanicException("invalid argument to Int63n");
            return (long)(Rng.NextDouble() * n);
        }

        // rand.Int63() int64
        public static long Int63()
        {
            var buf = new byte[8];
            Rng.NextBytes(buf);
            return (long)(BitConverter.ToUInt64(buf, 0) & 0x7FFFFFFFFFFFFFFF);
        }

        // rand.Int31() int32
        public static long Int31()
        {
            return (long)Rng.Next();
        }

        // rand.Int31n(n int32) int32
        public static long Int31n(long n)
        {
            return (long)Rng.Next((int)n);
        }

        // rand.Float32() float32
        public static double Float32()
        {
            return (double)(float)Rng.NextDouble();
        }

        // rand.Uint32() uint32
        public static long Uint32()
        {
            var buf = new byte[4];
            Rng.NextBytes(buf);
            return (long)BitConverter.ToUInt32(buf, 0);
        }

        // rand.Uint64() uint64
        public static long Uint64()
        {
            var buf = new byte[8];
            Rng.NextBytes(buf);
            return BitConverter.ToInt64(buf, 0);
        }

        // rand.Perm(n int) []int
        public static Slice<long> Perm(long n)
        {
            var arr = new long[(int)n];
            for (int i = 0; i < (int)n; i++) arr[i] = i;
            for (int i = (int)n - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return new Slice<long>(arr);
        }

        // rand.Shuffle(n int, swap func(i, j int))
        public static void Shuffle(long n, Action<long, long> swap)
        {
            for (long i = n - 1; i > 0; i--)
            {
                long j = (long)Rng.Next((int)(i + 1));
                swap(i, j);
            }
        }

        // rand.ExpFloat64() float64
        public static double ExpFloat64()
        {
            return -global::System.Math.Log(1.0 - Rng.NextDouble());
        }

        // rand.NormFloat64() float64
        public static double NormFloat64()
        {
            double u1 = 1.0 - Rng.NextDouble();
            double u2 = Rng.NextDouble();
            return global::System.Math.Sqrt(-2.0 * global::System.Math.Log(u1)) * global::System.Math.Cos(2.0 * global::System.Math.PI * u2);
        }

        // rand.Read(p []byte) (n int, err error)
        [return: GoReturn("int", "error")]
        public static (long, object?) Read(Slice<byte> p)
        {
            var buf = new byte[p.Len];
            Rng.NextBytes(buf);
            for (int i = 0; i < buf.Length; i++)
                p[i] = buf[i];
            return (p.Len, null);
        }

        // rand.New(src Source) *Rand
        public static Rand New(object src)
        {
            return new Rand(src);
        }

        // rand.NewSource(seed int64) Source
        public static object NewSource(long seed)
        {
            return new Source(seed);
        }
    }

    // rand.Source64 interface
    [GoType("interface", Name = "Source64", Package = "math/rand")]
    public interface ISource64
    {
        [GoMethod]
        [return: GoReturn("uint64")]
        long Uint64();

        [GoMethod]
        [return: GoReturn("int64")]
        long Int63();

        [GoMethod]
        void Seed(long seed);
    }
}

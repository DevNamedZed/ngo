// -----------------------------------------------------------------------
// <copyright file="GoRand.cs" company="Ziad">
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
    [GoType("struct", Name = "Rand", Package = "math/rand")]
    public sealed class GoRand
    {
        private readonly IGoRandSource _source;

        public GoRand(IGoRandSource source)
        {
            _source = source ?? new GoRandSource(1);
        }

        [GoMethod]
        public void Seed(long seed)
        {
            _source.Seed(seed);
        }

        [GoMethod]
        public long Int63()
        {
            return _source.Int63();
        }

        [GoMethod]
        public long Int()
        {
            long value = _source.Int63();
            return value & long.MaxValue;
        }

        [GoMethod]
        public long Int31()
        {
            return _source.Int63() >> 32;
        }

        [GoMethod]
        public long Uint32()
        {
            return _source.Int63() >> 31;
        }

        [GoMethod]
        public long Uint64()
        {
            if (_source is IGoRandSource64 source64)
            {
                return source64.Uint64();
            }
            return (_source.Int63() >> 31) ^ (_source.Int63() << 1);
        }

        [GoMethod]
        public long Intn(long n)
        {
            if (n <= 0)
            {
                throw new GoPanicException("invalid argument to Intn");
            }
            return Int63n(n);
        }

        [GoMethod]
        public long Int31n(long n)
        {
            if (n <= 0)
            {
                throw new GoPanicException("invalid argument to Int31n");
            }
            return Int31() % n;
        }

        [GoMethod]
        public long Int63n(long n)
        {
            if (n <= 0)
            {
                throw new GoPanicException("invalid argument to Int63n");
            }
            return _source.Int63() % n;
        }

        [GoMethod]
        public double Float64()
        {
            return (double)_source.Int63() / long.MaxValue;
        }

        [GoMethod]
        public double Float32()
        {
            return (float)Float64();
        }

        [GoMethod]
        public Slice<long> Perm(long n)
        {
            var result = new long[(int)n];
            for (int i = 0; i < (int)n; i++)
            {
                result[i] = i;
            }
            for (int i = (int)n - 1; i > 0; i--)
            {
                int j = (int)Int31n(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return new Slice<long>(result);
        }

        [GoMethod]
        public void Shuffle(long n, [GoParam("func(i, j int)")] Action<long, long> swap)
        {
            if (swap == null || n < 2)
            {
                return;
            }
            for (long i = n - 1; i > 0; i--)
            {
                long j = Int63n(i + 1);
                swap(i, j);
            }
        }

        [GoMethod]
        public double NormFloat64()
        {
            double u1 = 1.0 - Float64();
            double u2 = Float64();
            return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2);
        }

        [GoMethod]
        public double ExpFloat64()
        {
            return -System.Math.Log(1.0 - Float64());
        }

        [GoMethod]
        public (long, object?) Read(Slice<byte> buffer)
        {
            int length = buffer.Len;
            for (int i = 0; i < length; i++)
            {
                buffer[i] = (byte)(_source.Int63() & 0xFF);
            }
            return (length, null);
        }
    }
}

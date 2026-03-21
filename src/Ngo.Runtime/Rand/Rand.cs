// -----------------------------------------------------------------------
// <copyright file="Rand.cs" company="Ziad">
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
    [GoType("struct", Name = "Rand", Package = "math/rand")]
    public class Rand
    {
        private readonly Random _rng;

        public Rand(object src)
        {
            if (src is Source gs)
                _rng = new Random((int)(gs.Seed & 0x7FFFFFFF));
            else
                _rng = new Random();
        }

        [GoMethod]
        public long Intn(long n) => (long)_rng.Next((int)n);
        [GoMethod]
        public long Int63n(long n) => (long)(_rng.NextDouble() * n);
        [GoMethod]
        public long Int63()
        {
            var buf = new byte[8];
            _rng.NextBytes(buf);
            return (long)(BitConverter.ToUInt64(buf, 0) & 0x7FFFFFFFFFFFFFFF);
        }
        [GoMethod]
        public long Int31() => (long)_rng.Next();
        [GoMethod]
        public long Int31n(long n) => (long)_rng.Next((int)n);
        [GoMethod]
        public long Int() => (long)_rng.Next();
        [GoMethod]
        public double Float64() => _rng.NextDouble();
        [GoMethod]
        public double Float32() => (double)(float)_rng.NextDouble();
        [GoMethod]
        public long Uint32()
        {
            var buf = new byte[4];
            _rng.NextBytes(buf);
            return (long)BitConverter.ToUInt32(buf, 0);
        }
        [GoMethod]
        public long Uint64()
        {
            var buf = new byte[8];
            _rng.NextBytes(buf);
            return BitConverter.ToInt64(buf, 0);
        }
        [GoMethod]
        public void Seed(long seed) { }
        [GoMethod]
        public Slice<long> Perm(long n)
        {
            var arr = new long[(int)n];
            for (int i = 0; i < (int)n; i++) arr[i] = i;
            for (int i = (int)n - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return new Slice<long>(arr);
        }
        [GoMethod]
        public void Shuffle(long n, Action<long, long> swap)
        {
            for (long i = n - 1; i > 0; i--)
            {
                long j = (long)_rng.Next((int)(i + 1));
                swap(i, j);
            }
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read(Slice<byte> p)
        {
            var buf = new byte[p.Len];
            _rng.NextBytes(buf);
            for (int i = 0; i < p.Len; i++)
                p[i] = buf[i];
            return (p.Len, null);
        }

    }
}

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

namespace Ngo.Runtime
{
    public static class GoRand
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
    }
}

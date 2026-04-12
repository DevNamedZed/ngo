// -----------------------------------------------------------------------
// <copyright file="GoRandSource.cs" company="Ziad">
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

namespace Ngo.Runtime.Math.Rand
{
    /// <summary>
    /// Concrete implementation of math/rand.Source64 backed by System.Random.
    /// Returned by NewSource and used as the default source for the Rand struct.
    /// </summary>
    public sealed class GoRandSource : IGoRandSource64
    {
        private System.Random _random;

        public GoRandSource(long seed)
        {
            _random = new System.Random(unchecked((int)seed));
        }

        public long Int63()
        {
            return (long)(_random.NextDouble() * long.MaxValue);
        }

        public void Seed(long seed)
        {
            _random = new System.Random(unchecked((int)seed));
        }

        public long Uint64()
        {
            return unchecked((long)((ulong)(_random.NextDouble() * ulong.MaxValue)));
        }
    }
}

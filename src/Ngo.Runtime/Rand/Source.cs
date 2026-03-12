// -----------------------------------------------------------------------
// <copyright file="Source.cs" company="Ziad">
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
    [GoType("struct", Name = "Source", Package = "math/rand")]
    public class Source
    {
        public long Seed;
        public Source(long seed) { Seed = seed; }
        public long Int63()
        {
            var rng = new Random((int)(Seed & 0x7FFFFFFF));
            var buf = new byte[8];
            rng.NextBytes(buf);
            return (long)(BitConverter.ToUInt64(buf, 0) & 0x7FFFFFFFFFFFFFFF);
        }
        public long Seed2(long seed) { Seed = seed; return 0; }
    }
}

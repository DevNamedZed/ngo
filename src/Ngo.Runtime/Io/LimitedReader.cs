// -----------------------------------------------------------------------
// <copyright file="LimitedReader.cs" company="Ziad">
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

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Io
{
    /// <summary>A Reader that reads at most N bytes (io.LimitReader / io.LimitedReader).</summary>
    [GoType("struct", Package = "io", Name = "LimitedReader")]
    public sealed class LimitedReader : IGoReader
    {
        private IGoReader _inner;
        private long _remaining;

        public LimitedReader() { _inner = null!; }

        public LimitedReader(IGoReader inner, long n)
        {
            _inner = inner;
            _remaining = n;
        }

        [GoField(Name = "R", Type = "io.Reader")]
        public IGoReader R
        {
            get => _inner;
            set => _inner = value;
        }

        /// <summary>Go field: N int64</summary>
        [GoField(Name = "N")]
        public long N
        {
            get => _remaining;
            set => _remaining = value;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, string) Read(Slice<byte> p)
        {
            if (_remaining <= 0)
                return (0, GoIo.EOF);

            if (p.Len > _remaining)
                p = p.Reslice(0, (int)_remaining);

            var (n, err) = _inner.Read(p);
            _remaining -= n;
            return (n, err);
        }
    }
}

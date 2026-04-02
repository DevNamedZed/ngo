// -----------------------------------------------------------------------
// <copyright file="NopCloserReader.cs" company="Ziad">
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

namespace Ngo.Runtime.Io
{
    /// <summary>A ReadCloser wrapping a Reader with a no-op Close (io.NopCloser).</summary>
    public sealed class NopCloserReader : IGoReader, IGoCloser
    {
        private readonly IGoReader _inner;

        public NopCloserReader(IGoReader inner)
        {
            _inner = inner;
        }

        public (long, string) Read(Slice<byte> p) => _inner.Read(p);
        public string Close() => "";
    }
}

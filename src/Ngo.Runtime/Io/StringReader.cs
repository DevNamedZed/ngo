// -----------------------------------------------------------------------
// <copyright file="StringReader.cs" company="Ziad">
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
using System.Text;

namespace Ngo.Runtime.Io
{
    /// <summary>A Reader that reads from a string (like strings.NewReader).</summary>
    public sealed class StringReader : IGoReader
    {
        private readonly byte[] _data;
        private int _pos;

        public StringReader(string s)
        {
            _data = global::System.Text.Encoding.UTF8.GetBytes(s);
            _pos = 0;
        }

        public (long, string) Read(Slice<byte> p)
        {
            if (_pos >= _data.Length)
                return (0, GoIo.EOF);

            int n = global::System.Math.Min(p.Len, _data.Length - _pos);
            for (int i = 0; i < n; i++)
                p[i] = _data[_pos + i];
            _pos += n;

            string err = _pos >= _data.Length ? GoIo.EOF : "";
            return (n, err);
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="Builder.cs" company="Ziad">
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

using System.Text;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Strings
{
    [GoType("struct", Name = "Builder", Package = "strings")]
    public sealed class Builder
    {
        private readonly StringBuilder _sb = new();

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteString(string s)
        {
            _sb.Append(s);
            return (s.Length, null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? WriteByte([GoParam("uint8")] long b)
        {
            _sb.Append((char)(byte)b);
            return null;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) WriteRune([GoParam("rune")] long r)
        {
            _sb.Append((char)r);
            return (1, null);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write([GoParam("[]byte")] Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
                _sb.Append((char)p[i]);
            return (p.Len, null);
        }

        [GoMethod]
        public void Reset() => _sb.Clear();

        [GoMethod]
        public long Len() => _sb.Length;

        [GoMethod]
        public string String() => _sb.ToString();

        [GoMethod]
        public void Grow(long n) => _sb.EnsureCapacity((int)n);

        [GoMethod]
        public long Cap() => _sb.Capacity;

        public override string ToString() => _sb.ToString();
    }
}

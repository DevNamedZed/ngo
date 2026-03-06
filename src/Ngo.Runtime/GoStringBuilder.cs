// -----------------------------------------------------------------------
// <copyright file="GoStringBuilder.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public sealed class GoStringBuilder
    {
        private readonly StringBuilder _sb = new();

        public void WriteString(string s) => _sb.Append(s);

        public void WriteByte(long b) => _sb.Append((char)(byte)b);

        public void WriteRune(long r) => _sb.Append((char)r);

        public void Write(Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
                _sb.Append((char)p[i]);
        }

        public void Reset() => _sb.Clear();

        public long Len() => _sb.Length;

        public string String() => _sb.ToString();

        public override string ToString() => _sb.ToString();
    }
}

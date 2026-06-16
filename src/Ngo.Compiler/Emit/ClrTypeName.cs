// -----------------------------------------------------------------------
// <copyright file="ClrTypeName.cs" company="Ziad">
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

using System.Globalization;
using System.Text;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Encodes an arbitrary Go type fingerprint as a single .NET-legal type-name segment.
    /// A Go anonymous-struct fingerprint such as <c>struct{table [32]affineLookupTable;initOnce Once}</c>
    /// contains characters that are reserved in the .NET / ECMA-335 type-name grammar
    /// (<c>[ ] , * &amp; +</c>) plus structural punctuation (<c>{ } ; </c> and spaces). Using such a
    /// string as a CLR type name corrupts every consumer that parses the name — array/generic
    /// detection truncates at the first <c>[</c>, and <c>TypeBuilder.DefineType</c> itself cannot be
    /// trusted with it.
    /// <para>
    /// The encoding passes ASCII letters and digits through unchanged and maps every other character
    /// to a fixed-width <c>_XXXX</c> escape (the UTF-16 code unit in hexadecimal). The result is a
    /// legal identifier, the mapping is a pure function of its input (so the definition site and every
    /// reference site across archive boundaries produce the identical name), and it is injective: the
    /// only <c>_</c> in the output begins a four-hex-digit escape, so distinct fingerprints can never
    /// collide.
    /// </para>
    /// </summary>
    internal static class ClrTypeName
    {
        public static string Escape(string name)
        {
            var builder = new StringBuilder(name.Length);
            foreach (char character in name)
            {
                if (IsUnreserved(character))
                {
                    builder.Append(character);
                }
                else
                {
                    builder.Append('_');
                    builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                }
            }
            return builder.ToString();
        }

        public static string Unescape(string encoded)
        {
            var builder = new StringBuilder(encoded.Length);
            int index = 0;
            while (index < encoded.Length)
            {
                char character = encoded[index];
                if (character == '_')
                {
                    var codeUnit = int.Parse(encoded.Substring(index + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    builder.Append((char)codeUnit);
                    index += 5;
                }
                else
                {
                    builder.Append(character);
                    index += 1;
                }
            }
            return builder.ToString();
        }

        private static bool IsUnreserved(char character)
        {
            return (character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9');
        }
    }
}

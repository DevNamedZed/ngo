// -----------------------------------------------------------------------
// <copyright file="GoUnicode.cs" company="Ziad">
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
using System.Globalization;

namespace Ngo.Runtime
{
    /// <summary>
    /// Runtime support for Go's unicode package.
    /// Go's rune is int32, so we accept long and cast to char for BMP.
    /// </summary>
    public static class GoUnicode
    {
        public static bool IsLetter(long r) => char.IsLetter((char)r);
        public static bool IsDigit(long r) => char.IsDigit((char)r);
        public static bool IsSpace(long r) => char.IsWhiteSpace((char)r);
        public static bool IsUpper(long r) => char.IsUpper((char)r);
        public static bool IsLower(long r) => char.IsLower((char)r);
        public static bool IsPunct(long r) => char.IsPunctuation((char)r);
        public static bool IsControl(long r) => char.IsControl((char)r);
        public static long ToUpper(long r) => char.ToUpper((char)r);
        public static long ToLower(long r) => char.ToLower((char)r);
    }

    /// <summary>
    /// Runtime support for Go's unicode/utf8 package.
    /// </summary>
    public static class GoUtf8
    {
        // utf8.RuneCountInString(s string) int
        public static long RuneCountInString(string s)
        {
            if (s == null) return 0;
            var info = new StringInfo(s);
            return info.LengthInTextElements;
        }

        // utf8.ValidString(s string) bool
        public static bool ValidString(string s)
        {
            // .NET strings are always valid UTF-16, but check for unpaired surrogates
            if (s == null) return true;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]))
                {
                    if (i + 1 >= s.Length || !char.IsLowSurrogate(s[i + 1]))
                        return false;
                    i++;
                }
                else if (char.IsLowSurrogate(s[i]))
                {
                    return false;
                }
            }
            return true;
        }

        // utf8.DecodeRuneInString(s string) (rune, size int)
        public static (long, long) DecodeRuneInString(string s)
        {
            if (string.IsNullOrEmpty(s)) return (0xFFFD, 0); // RuneError
            char c = s[0];
            if (char.IsHighSurrogate(c) && s.Length > 1 && char.IsLowSurrogate(s[1]))
            {
                int codePoint = char.ConvertToUtf32(c, s[1]);
                return (codePoint, 4);
            }
            // Single UTF-16 char → 1-3 bytes in UTF-8
            int byteLen = c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
            return (c, byteLen);
        }

        // utf8.RuneLen(r rune) int
        public static long RuneLen(long r)
        {
            if (r < 0) return -1;
            if (r < 0x80) return 1;
            if (r < 0x800) return 2;
            if (r < 0x10000) return 3;
            if (r < 0x110000) return 4;
            return -1;
        }

        // Constants
        public static readonly long RuneError = 0xFFFD;
        public static readonly long MaxRune = 0x10FFFF;
        public static readonly long UTFMax = 4;
    }
}

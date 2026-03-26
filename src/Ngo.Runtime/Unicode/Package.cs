// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
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
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Unicode
{
    /// <summary>
    /// Runtime support for Go's unicode package.
    /// Go's rune is int32, so we accept long and cast to char for BMP.
    /// </summary>
    [GoPackage("unicode")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsLetter([GoParam("rune")] long r) => char.IsLetter((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsDigit([GoParam("rune")] long r) => char.IsDigit((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsSpace([GoParam("rune")] long r) => char.IsWhiteSpace((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsUpper([GoParam("rune")] long r) => char.IsUpper((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsLower([GoParam("rune")] long r) => char.IsLower((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsPunct([GoParam("rune")] long r) => char.IsPunctuation((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsControl([GoParam("rune")] long r) => char.IsControl((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsNumber([GoParam("rune")] long r) => char.IsNumber((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsGraphic([GoParam("rune")] long r) => !char.IsControl((char)r) && !char.IsWhiteSpace((char)r) || char.IsLetterOrDigit((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsPrint([GoParam("rune")] long r) => !char.IsControl((char)r);

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsTitle([GoParam("rune")] long r)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory((char)r);
            return uc == UnicodeCategory.TitlecaseLetter;
        }

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsSymbol([GoParam("rune")] long r) => char.IsSymbol((char)r);

        [GoFunc]
        [return: GoReturn("rune")]
        public static long ToUpper([GoParam("rune")] long r) => char.ToUpper((char)r);

        [GoFunc]
        [return: GoReturn("rune")]
        public static long ToLower([GoParam("rune")] long r) => char.ToLower((char)r);

        [GoFunc]
        [return: GoReturn("rune")]
        public static long ToTitle([GoParam("rune")] long r)
        {
            // In Unicode, titlecase is mostly the same as uppercase for most characters
            return char.ToUpper((char)r);
        }

        // --- Stubs for exports in PackageRegistry but missing from runtime ---

        [GoFunc(IsVariadic = true)]
        [return: GoReturn("bool")]
        public static bool In([GoParam("rune")] long r, params object[] ranges)
        {
            if (ranges == null)
            {
                return false;
            }
            foreach (var range in ranges)
            {
                if (Is(range, r))
                {
                    return true;
                }
            }
            return false;
        }

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool Is(object? rangeTab, [GoParam("rune")] long r)
        {
            if (rangeTab is RangeTable table)
            {
                if (!table.R16.IsNil)
                {
                    for (int i = 0; i < table.R16.Len; i++)
                    {
                        var range = table.R16[i];
                        if (r >= range.Lo && r <= range.Hi)
                        {
                            if (range.Stride == 1 || (r - range.Lo) % range.Stride == 0)
                            {
                                return true;
                            }
                        }
                    }
                }
                if (!table.R32.IsNil)
                {
                    for (int i = 0; i < table.R32.Len; i++)
                    {
                        var range = table.R32[i];
                        if (r >= range.Lo && r <= range.Hi)
                        {
                            if (range.Stride == 1 || (r - range.Lo) % range.Stride == 0)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsOneOf(object? ranges, [GoParam("rune")] long r)
        {
            if (ranges is Slice<object?> rangeSlice)
            {
                for (int i = 0; i < rangeSlice.Len; i++)
                {
                    if (Is(rangeSlice[i], r))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        [GoFunc]
        [return: GoReturn("rune")]
        public static long SimpleFold([GoParam("rune")] long r)
        {
            char c = (char)r;
            if (char.IsUpper(c))
            {
                char lower = char.ToLowerInvariant(c);
                if (lower != c)
                {
                    return lower;
                }
            }
            else if (char.IsLower(c))
            {
                char upper = char.ToUpperInvariant(c);
                if (upper != c)
                {
                    return upper;
                }
            }
            return r;
        }

        // Constants
        [GoConst(Type = "rune")]
        public static readonly long MaxASCII = 0x7F;

        [GoConst(Type = "rune")]
        public static readonly long MaxRune = 0x10FFFF;

        [GoConst(Type = "rune")]
        public static readonly long MaxLatin1 = 0xFF;

        [GoConst(Type = "rune")]
        public static readonly long ReplacementChar = 0xFFFD;

        [GoConst(Type = "int")]
        public static readonly long UpperCase = 0;

        [GoConst(Type = "int")]
        public static readonly long LowerCase = 1;

        [GoConst(Type = "int")]
        public static readonly long TitleCase = 2;

        // RangeTable package-level variables (stubs — populated at runtime if needed)
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Letter = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Upper = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Lower = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Title = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Number = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Digit = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Mark = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Punct = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Symbol = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Space = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Cc = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Cf = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Co = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Cs = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Nd = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Nl = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? No = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Mn = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Me = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Mc = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Ll = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Lu = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Lt = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Lm = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Lo = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Pc = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Pd = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Pe = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Pf = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Pi = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Po = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Ps = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Sc = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Sk = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Sm = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? So = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Zl = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Zp = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Zs = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Latin = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Greek = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Cyrillic = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Han = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Hiragana = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Katakana = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Arabic = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Hebrew = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Thai = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Devanagari = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Common = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Inherited = null;

        // Script/Category maps
        [GoVar(Type = "map[string]*RangeTable")] public static readonly object? Categories = null;
        [GoVar(Type = "map[string]*RangeTable")] public static readonly object? Scripts = null;
        [GoVar(Type = "map[string]*RangeTable")] public static readonly object? Properties = null;
        [GoVar(Type = "map[string]*RangeTable")] public static readonly object? FoldCategory = null;
        [GoVar(Type = "map[string]*RangeTable")] public static readonly object? FoldScript = null;

        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? Soft_Dotted = null;

        // Unicode general category groups
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? N = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? M = null;
        [GoVar(Type = "*unicode.RangeTable")] public static readonly object? L = null;

        // SpecialCase vars
        public static readonly SpecialCase TurkishCase = new SpecialCase();
        public static readonly SpecialCase AzeriCase = new SpecialCase();

        [GoFunc]
        [return: GoReturn("bool")]
        public static bool IsMark([GoParam("rune")] long r)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory((char)r);
            return category == UnicodeCategory.NonSpacingMark
                || category == UnicodeCategory.SpacingCombiningMark
                || category == UnicodeCategory.EnclosingMark;
        }
    }
}

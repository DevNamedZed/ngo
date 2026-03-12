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
using System.Numerics;
using System.Text;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Strconv
{
    [GoPackage("strconv")]
    public static class Package
    {
        // ---- Constants ----

        [GoConst]
        public const long IntSize = 64;

        // ---- Package variables ----

        [GoVar(Type = "error")]
        public static readonly object ErrRange = "value out of range";

        [GoVar(Type = "error")]
        public static readonly object ErrSyntax = "invalid syntax";

        // ---- Functions ----

        [GoFunc]
        public static string Itoa(long i) => i.ToString();

        [GoFunc]
        [return: GoReturn("int", "error")]
        public static (long value, object? error) Atoi(string s)
        {
            if (long.TryParse(s, out long result))
            {
                return (result, null);
            }
            return (0, $"strconv.Atoi: parsing \"{s}\": invalid syntax");
        }

        [GoFunc]
        public static string FormatInt([GoParam("int64")] long i, long @base)
        {
            return (int)@base switch
            {
                2 => Convert.ToString(i, 2),
                8 => Convert.ToString(i, 8),
                16 => Convert.ToString(i, 16),
                _ => i.ToString(),
            };
        }

        [GoFunc]
        public static string FormatBool(bool b) => b ? "true" : "false";

        [GoFunc]
        [return: GoReturn("int64", "error")]
        public static (long value, object? error) ParseInt(string s, long @base, long bitSize)
        {
            try
            {
                long result = Convert.ToInt64(s, (int)(@base == 0 ? 10 : @base));
                return (result, null);
            }
            catch
            {
                return (0, $"strconv.ParseInt: parsing \"{s}\": invalid syntax");
            }
        }

        [GoFunc]
        [return: GoReturn("float64", "error")]
        public static (double value, object? error) ParseFloat(string s, long bitSize)
        {
            if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out double result))
            {
                return (result, null);
            }
            return (0.0, $"strconv.ParseFloat: parsing \"{s}\": invalid syntax");
        }

        [GoFunc]
        public static string FormatFloat(double f, byte fmt, long prec, long bitSize)
        {
            return fmt switch
            {
                (byte)'f' => prec >= 0
                    ? f.ToString("F" + prec, CultureInfo.InvariantCulture)
                    : f.ToString("G", CultureInfo.InvariantCulture),
                (byte)'e' => prec >= 0
                    ? f.ToString("E" + prec, CultureInfo.InvariantCulture).ToLower()
                    : f.ToString("E", CultureInfo.InvariantCulture).ToLower(),
                (byte)'E' => prec >= 0
                    ? f.ToString("E" + prec, CultureInfo.InvariantCulture)
                    : f.ToString("E", CultureInfo.InvariantCulture),
                (byte)'g' => prec >= 0
                    ? f.ToString("G" + prec, CultureInfo.InvariantCulture)
                    : f.ToString("G", CultureInfo.InvariantCulture),
                _ => f.ToString("G", CultureInfo.InvariantCulture),
            };
        }

        [GoFunc]
        [return: GoReturn("bool", "error")]
        public static (bool value, object? error) ParseBool(string s)
        {
            return s switch
            {
                "1" or "t" or "T" or "TRUE" or "true" or "True" => (true, null),
                "0" or "f" or "F" or "FALSE" or "false" or "False" => (false, null),
                _ => (false, Errors.Package.New($"strconv.ParseBool: parsing \"{s}\": invalid syntax")),
            };
        }

        [GoFunc]
        [return: GoReturn("uint64", "error")]
        public static (long value, object? error) ParseUint(string s, long @base, long bitSize)
        {
            try
            {
                ulong result = Convert.ToUInt64(s, (int)(@base == 0 ? 10 : @base));
                return ((long)result, null);
            }
            catch
            {
                return (0, $"strconv.ParseUint: parsing \"{s}\": invalid syntax");
            }
        }

        [GoFunc]
        public static string FormatUint([GoParam("uint64")] long i, long @base)
        {
            ulong u = (ulong)i;
            return (int)@base switch
            {
                2 => Convert.ToString((long)u, 2),
                8 => Convert.ToString((long)u, 8),
                16 => u.ToString("x"),
                _ => u.ToString(),
            };
        }

        [GoFunc]
        public static string Quote(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r") + "\"";
        }

        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string value, object? error) Unquote(string s)
        {
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                var inner = s.Substring(1, s.Length - 2);
                inner = inner.Replace("\\\\", "\\").Replace("\\\"", "\"")
                    .Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r");
                return (inner, null);
            }
            return ("", $"strconv.Unquote: invalid syntax");
        }

        [GoFunc]
        [return: GoReturn("rune", "bool", "string", "error")]
        public static (long value, bool multibyte, string tail, object? err) UnquoteChar(string s, byte quote)
        {
            if (s.Length == 0)
                return (0, false, "", "strconv.UnquoteChar: empty string");

            char c = s[0];
            if (c == '\\' && s.Length >= 2)
            {
                switch (s[1])
                {
                    case 'n': return ('\n', false, s.Substring(2), null);
                    case 't': return ('\t', false, s.Substring(2), null);
                    case 'r': return ('\r', false, s.Substring(2), null);
                    case '\\': return ('\\', false, s.Substring(2), null);
                    case '\'': return ('\'', false, s.Substring(2), null);
                    case '"': return ('"', false, s.Substring(2), null);
                    case '0': return (0, false, s.Substring(2), null);
                    case 'a': return ('\a', false, s.Substring(2), null);
                    case 'b': return ('\b', false, s.Substring(2), null);
                    case 'f': return ('\f', false, s.Substring(2), null);
                    case 'v': return ('\v', false, s.Substring(2), null);
                }
            }

            // Check for multi-byte UTF-8
            if (c >= 0x80 || char.IsHighSurrogate(c))
            {
                int runeLen = 1;
                if (char.IsHighSurrogate(c) && s.Length >= 2 && char.IsLowSurrogate(s[1]))
                {
                    long rune = char.ConvertToUtf32(c, s[1]);
                    return (rune, true, s.Substring(2), null);
                }
                return (c, c >= 0x80, s.Substring(runeLen), null);
            }

            return (c, false, s.Substring(1), null);
        }

        [GoFunc]
        public static Slice<byte> AppendInt(Slice<byte> dst, [GoParam("int64")] long i, long @base)
        {
            string s = FormatInt(i, @base);
            return AppendString(dst, s);
        }

        [GoFunc]
        public static Slice<byte> AppendBool(Slice<byte> dst, bool b)
        {
            string s = FormatBool(b);
            return AppendString(dst, s);
        }

        [GoFunc]
        public static Slice<byte> AppendUint(Slice<byte> dst, [GoParam("uint64")] long i, long @base)
        {
            string s = FormatUint(i, @base);
            return AppendString(dst, s);
        }

        [GoFunc]
        public static Slice<byte> AppendQuote(Slice<byte> dst, string s)
        {
            string q = Quote(s);
            return AppendString(dst, q);
        }

        [GoFunc]
        public static bool CanBackquote(string s)
        {
            foreach (char c in s)
            {
                if (c == '`' || c == '\uFEFF')
                    return false;
                if ((c < ' ' && c != '\t') || c == '\u007F')
                    return false;
            }
            return true;
        }

        [GoFunc]
        public static bool IsPrint([GoParam("rune")] long r)
        {
            if (r <= 0xFF)
            {
                if (0x20 <= r && r <= 0x7E) return true;
                if (0xA1 <= r && r <= 0xFF && r != 0xAD) return true;
                return false;
            }
            // For higher code points, use .NET's categorization
            if (r > 0xFFFF) return true; // Supplementary planes — approximate
            var cat = char.GetUnicodeCategory((char)r);
            return cat != System.Globalization.UnicodeCategory.Control
                && cat != System.Globalization.UnicodeCategory.OtherNotAssigned
                && cat != System.Globalization.UnicodeCategory.Surrogate;
        }

        [GoFunc]
        public static string QuoteRune([GoParam("rune")] long r)
        {
            return "'" + EscapeRune(r) + "'";
        }

        [GoFunc]
        public static string QuoteToASCII(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c >= 0x20 && c < 0x7F && c != '"' && c != '\\')
                {
                    sb.Append(c);
                }
                else
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\a': sb.Append("\\a"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\v': sb.Append("\\v"); break;
                        default:
                            if (c < 0x100)
                                sb.Append($"\\x{(int)c:x2}");
                            else
                                sb.Append($"\\u{(int)c:x4}");
                            break;
                    }
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        [GoFunc]
        public static string QuoteRuneToASCII([GoParam("rune")] long r)
        {
            return "'" + EscapeRuneASCII(r) + "'";
        }

        [GoFunc]
        public static Slice<byte> AppendFloat(Slice<byte> dst, double f, byte fmt, long prec, long bitSize)
        {
            string s = FormatFloat(f, fmt, prec, bitSize);
            return AppendString(dst, s);
        }

        [GoFunc]
        public static Slice<byte> AppendQuoteToASCII(Slice<byte> dst, string s)
        {
            string q = QuoteToASCII(s);
            return AppendString(dst, q);
        }

        [GoFunc]
        [return: GoReturn("complex128", "error")]
        public static (Complex value, object? error) ParseComplex(string s, long bitSize)
        {
            // Minimal implementation: parse "a+bi" or just real numbers
            try
            {
                if (s.EndsWith("i"))
                {
                    // Try to find + or - separating real and imag
                    int sepIdx = s.LastIndexOfAny(new[] { '+', '-' }, s.Length - 2);
                    if (sepIdx > 0)
                    {
                        string realPart = s.Substring(0, sepIdx);
                        string imagPart = s.Substring(sepIdx, s.Length - sepIdx - 1);
                        double re = double.Parse(realPart, CultureInfo.InvariantCulture);
                        double im = double.Parse(imagPart, CultureInfo.InvariantCulture);
                        return (new Complex(re, im), null);
                    }
                    // Pure imaginary
                    string imStr = s.Substring(0, s.Length - 1);
                    double imVal = double.Parse(imStr, CultureInfo.InvariantCulture);
                    return (new Complex(0, imVal), null);
                }
                // Pure real
                double reVal = double.Parse(s, CultureInfo.InvariantCulture);
                return (new Complex(reVal, 0), null);
            }
            catch
            {
                return (default, $"strconv.ParseComplex: parsing \"{s}\": invalid syntax");
            }
        }

        [GoFunc]
        public static string FormatComplex(Complex c, byte fmt, long prec, long bitSize)
        {
            string re = FormatFloat(c.Real, fmt, prec, 64);
            string im = FormatFloat(c.Imaginary, fmt, prec, 64);
            if (c.Imaginary >= 0)
                return "(" + re + "+" + im + "i)";
            return "(" + re + im + "i)";
        }

        [GoFunc]
        public static Slice<byte> AppendQuoteRune(Slice<byte> dst, [GoParam("rune")] long r)
        {
            string s = QuoteRune(r);
            return AppendString(dst, s);
        }

        [GoFunc]
        public static Slice<byte> AppendQuoteRuneToASCII(Slice<byte> dst, [GoParam("rune")] long r)
        {
            string s = QuoteRuneToASCII(r);
            return AppendString(dst, s);
        }

        [GoFunc]
        public static bool IsGraphic([GoParam("rune")] long r)
        {
            if (IsPrint(r)) return true;
            // Check for graphic Unicode categories beyond IsPrint
            if (r < 0 || r > 0x10FFFF) return false;
            if (r <= 0xFFFF)
            {
                var cat = char.GetUnicodeCategory((char)r);
                return cat == System.Globalization.UnicodeCategory.SpaceSeparator
                    || cat == System.Globalization.UnicodeCategory.LineSeparator
                    || cat == System.Globalization.UnicodeCategory.ParagraphSeparator;
            }
            return false;
        }

        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string value, object? error) QuotedPrefix(string s)
        {
            // Find the end of a quoted string at the start of s
            if (s.Length == 0)
                return ("", "strconv.QuotedPrefix: empty string");

            char quote = s[0];
            if (quote != '"' && quote != '\'' && quote != '`')
                return ("", $"strconv.QuotedPrefix: invalid syntax");

            if (quote == '`')
            {
                int end = s.IndexOf('`', 1);
                if (end < 0)
                    return ("", "strconv.QuotedPrefix: invalid syntax");
                return (s.Substring(0, end + 1), null);
            }

            // Handle " and ' quoted strings
            int i = 1;
            while (i < s.Length)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i += 2; // skip escaped char
                    continue;
                }
                if (s[i] == quote)
                {
                    return (s.Substring(0, i + 1), null);
                }
                i++;
            }
            return ("", "strconv.QuotedPrefix: invalid syntax");
        }

        // ---- Helper methods (not exported to Go) ----

        private static Slice<byte> AppendString(Slice<byte> dst, string s)
        {
            byte[] bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            foreach (byte b in bytes)
            {
                dst = Slice<byte>.Append(dst, b);
            }
            return dst;
        }

        private static string EscapeRune(long r)
        {
            switch (r)
            {
                case '\n': return "\\n";
                case '\t': return "\\t";
                case '\r': return "\\r";
                case '\\': return "\\\\";
                case '\'': return "\\'";
                case '\a': return "\\a";
                case '\b': return "\\b";
                case '\f': return "\\f";
                case '\v': return "\\v";
            }
            if (r >= 0x20 && r < 0x7F)
                return ((char)r).ToString();
            if (r <= 0xFFFF)
                return $"\\u{r:x4}";
            return $"\\U{r:x8}";
        }

        private static string EscapeRuneASCII(long r)
        {
            if (r >= 0x20 && r < 0x7F)
            {
                switch (r)
                {
                    case '\\': return "\\\\";
                    case '\'': return "\\'";
                    default: return ((char)r).ToString();
                }
            }
            switch (r)
            {
                case '\n': return "\\n";
                case '\t': return "\\t";
                case '\r': return "\\r";
                case '\a': return "\\a";
                case '\b': return "\\b";
                case '\f': return "\\f";
                case '\v': return "\\v";
            }
            if (r <= 0xFFFF)
                return $"\\u{r:x4}";
            return $"\\U{r:x8}";
        }
    }
}

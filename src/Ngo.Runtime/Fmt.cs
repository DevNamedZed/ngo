// -----------------------------------------------------------------------
// <copyright file="Fmt.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class Fmt
    {
        public static void Println(params object?[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) Console.Write(" ");
                Console.Write(FormatValue(args[i]));
            }
            Console.WriteLine();
        }

        public static void Print(params object?[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                // Print uses spaces between non-string operands
                if (i > 0 && !(args[i - 1] is string) && !(args[i] is string))
                {
                    Console.Write(" ");
                }
                Console.Write(FormatValue(args[i]));
            }
        }

        public static void Printf(string format, params object?[] args)
        {
            Console.Write(Sprintf(format, args));
        }

        public static string Sprintf(string format, params object?[] args)
        {
            var sb = new StringBuilder();
            int argIndex = 0;

            for (int i = 0; i < format.Length; i++)
            {
                if (format[i] != '%')
                {
                    sb.Append(format[i]);
                    continue;
                }

                i++; // skip %
                if (i >= format.Length) break;

                // %%
                if (format[i] == '%')
                {
                    sb.Append('%');
                    continue;
                }

                // Parse flags
                bool flagMinus = false, flagPlus = false, flagZero = false;
                bool flagHash = false, flagSpace = false;
                while (i < format.Length)
                {
                    switch (format[i])
                    {
                        case '-': flagMinus = true; i++; continue;
                        case '+': flagPlus = true; i++; continue;
                        case '0': flagZero = true; i++; continue;
                        case '#': flagHash = true; i++; continue;
                        case ' ': flagSpace = true; i++; continue;
                    }
                    break;
                }

                // Parse width
                int width = -1;
                if (i < format.Length && format[i] >= '1' && format[i] <= '9')
                {
                    width = 0;
                    while (i < format.Length && format[i] >= '0' && format[i] <= '9')
                    {
                        width = width * 10 + (format[i] - '0');
                        i++;
                    }
                }

                // Parse precision
                int prec = -1;
                if (i < format.Length && format[i] == '.')
                {
                    i++;
                    prec = 0;
                    while (i < format.Length && format[i] >= '0' && format[i] <= '9')
                    {
                        prec = prec * 10 + (format[i] - '0');
                        i++;
                    }
                }

                if (i >= format.Length) break;

                char verb = format[i];
                object? arg = argIndex < args.Length ? args[argIndex] : null;
                argIndex++;

                sb.Append(FormatVerb(verb, arg, flagMinus, flagPlus, flagZero, flagHash, flagSpace, width, prec));
            }

            return sb.ToString();
        }

        public static string Sprint(params object?[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0 && !(args[i - 1] is string) && !(args[i] is string))
                {
                    sb.Append(" ");
                }
                sb.Append(FormatValue(args[i]));
            }
            return sb.ToString();
        }

        public static string Errorf(string format, params object?[] args)
        {
            return Sprintf(format, args);
        }

        public static string Sprintln(params object?[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(" ");
                sb.Append(FormatValue(args[i]));
            }
            sb.Append('\n');
            return sb.ToString();
        }

        private static string FormatVerb(char verb, object? arg,
            bool flagMinus, bool flagPlus, bool flagZero, bool flagHash, bool flagSpace,
            int width, int prec)
        {
            if (arg == null)
                return ApplyWidth("<nil>", width, flagMinus, false, false);

            string result;
            bool isNumeric = false;

            switch (verb)
            {
                case 'v':
                    result = FormatValue(arg);
                    break;
                case 'd':
                    result = FormatInt(arg);
                    isNumeric = true;
                    if (prec >= 0)
                        result = ApplyIntPrecision(result, prec);
                    result = ApplySign(result, flagPlus, flagSpace);
                    break;
                case 's':
                    result = arg.ToString() ?? "";
                    if (prec >= 0 && result.Length > prec)
                        result = result.Substring(0, prec);
                    break;
                case 'f':
                    result = FormatFloat(arg, prec);
                    isNumeric = true;
                    result = ApplySign(result, flagPlus, flagSpace);
                    break;
                case 'e':
                    result = FormatFloatSci(arg, 'e', prec);
                    isNumeric = true;
                    result = ApplySign(result, flagPlus, flagSpace);
                    break;
                case 'E':
                    result = FormatFloatSci(arg, 'E', prec);
                    isNumeric = true;
                    result = ApplySign(result, flagPlus, flagSpace);
                    break;
                case 'g':
                    result = FormatFloatG(arg, prec);
                    isNumeric = true;
                    result = ApplySign(result, flagPlus, flagSpace);
                    break;
                case 't':
                    result = arg is bool bv ? (bv ? "true" : "false") : $"%!t({FormatValue(arg)})";
                    break;
                case 'x':
                    result = FormatHex(arg, false);
                    isNumeric = true;
                    if (prec >= 0 && !(arg is string))
                        result = ApplyIntPrecision(result, prec);
                    if (flagHash && !(arg is string))
                        result = "0x" + result;
                    result = ApplySign(result, flagPlus, flagSpace);
                    break;
                case 'X':
                    result = FormatHex(arg, true);
                    isNumeric = true;
                    if (prec >= 0 && !(arg is string))
                        result = ApplyIntPrecision(result, prec);
                    if (flagHash && !(arg is string))
                        result = "0X" + result;
                    result = ApplySign(result, flagPlus, flagSpace);
                    break;
                case 'o':
                    result = FormatOctal(arg);
                    isNumeric = true;
                    if (prec >= 0)
                        result = ApplyIntPrecision(result, prec);
                    if (flagHash && result.Length > 0 && result[0] != '0')
                        result = "0" + result;
                    break;
                case 'b':
                    result = FormatBinary(arg);
                    isNumeric = true;
                    if (prec >= 0)
                        result = ApplyIntPrecision(result, prec);
                    if (flagHash)
                        result = "0b" + result;
                    break;
                case 'c':
                    result = FormatChar(arg);
                    break;
                case 'q':
                    result = FormatQuoted(arg);
                    break;
                case 'p':
                    result = FormatPointer(arg);
                    break;
                case 'T':
                    result = FormatType(arg);
                    break;
                default:
                    result = $"%!{verb}({FormatValue(arg)})";
                    break;
            }

            return ApplyWidth(result, width, flagMinus, flagZero, isNumeric);
        }

        private static string ApplySign(string formatted, bool flagPlus, bool flagSpace)
        {
            if (formatted.Length > 0 && formatted[0] != '-')
            {
                if (flagPlus)
                    return "+" + formatted;
                if (flagSpace)
                    return " " + formatted;
            }
            return formatted;
        }

        private static string ApplyIntPrecision(string formatted, int prec)
        {
            bool negative = formatted.Length > 0 && formatted[0] == '-';
            string digits = negative ? formatted.Substring(1) : formatted;
            if (digits.Length < prec)
                digits = new string('0', prec - digits.Length) + digits;
            return negative ? "-" + digits : digits;
        }

        private static string ApplyWidth(string formatted, int width, bool flagMinus, bool flagZero, bool isNumeric)
        {
            if (width < 0 || formatted.Length >= width)
                return formatted;

            int padding = width - formatted.Length;

            if (flagMinus)
                return formatted + new string(' ', padding);

            if (flagZero && isNumeric)
            {
                if (formatted.Length > 0 && (formatted[0] == '-' || formatted[0] == '+' || formatted[0] == ' '))
                    return formatted[0] + new string('0', padding) + formatted.Substring(1);
                return new string('0', padding) + formatted;
            }

            return new string(' ', padding) + formatted;
        }

        public static string FormatValue(object? arg)
        {
            if (arg == null) return "<nil>";
            if (arg is bool b) return b ? "true" : "false";
            return arg.ToString() ?? "";
        }

        private static string FormatInt(object? arg)
        {
            if (arg is int i) return i.ToString();
            if (arg is long l) return l.ToString();
            if (arg is byte by) return by.ToString();
            if (arg is short sh) return sh.ToString();
            if (arg is uint ui) return ui.ToString();
            if (arg is ulong ul) return ul.ToString();
            if (arg is double d) return ((long)d).ToString();
            if (arg is float f) return ((long)f).ToString();
            return arg?.ToString() ?? "0";
        }

        private static string FormatFloat(object? arg, int prec)
        {
            int p = prec >= 0 ? prec : 6;
            string fmt = "F" + p;
            if (arg is double d) return d.ToString(fmt);
            if (arg is float f) return f.ToString(fmt);
            if (arg is int i) return ((double)i).ToString(fmt);
            if (arg is long l) return ((double)l).ToString(fmt);
            return arg?.ToString() ?? 0.0.ToString(fmt);
        }

        private static string FormatFloatSci(object? arg, char e, int prec)
        {
            int p = prec >= 0 ? prec : 6;
            double val = Convert.ToDouble(arg);
            return val.ToString((e == 'e' ? "e" : "E") + p);
        }

        private static string FormatFloatG(object? arg, int prec)
        {
            double val = Convert.ToDouble(arg);
            if (prec >= 0)
                return val.ToString("G" + prec);
            return val.ToString("G");
        }

        private static string FormatHex(object? arg, bool upper)
        {
            if (arg is int i) return i.ToString(upper ? "X" : "x");
            if (arg is long l) return l.ToString(upper ? "X" : "x");
            if (arg is string s)
            {
                var sb = new StringBuilder();
                foreach (char c in s)
                    sb.Append(((int)c).ToString(upper ? "X2" : "x2"));
                return sb.ToString();
            }
            return arg?.ToString() ?? "0";
        }

        private static string FormatOctal(object? arg)
        {
            if (arg is int i) return Convert.ToString(i, 8);
            if (arg is long l) return Convert.ToString(l, 8);
            return arg?.ToString() ?? "0";
        }

        private static string FormatBinary(object? arg)
        {
            if (arg is int i) return Convert.ToString(i, 2);
            if (arg is long l) return Convert.ToString(l, 2);
            return arg?.ToString() ?? "0";
        }

        private static string FormatChar(object? arg)
        {
            if (arg is int i) return ((char)i).ToString();
            if (arg is long l) return ((char)l).ToString();
            return arg?.ToString() ?? "";
        }

        private static string FormatQuoted(object? arg)
        {
            if (arg is string s) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            if (arg is int i) return "'" + (char)i + "'";
            return "\"" + (arg?.ToString() ?? "") + "\"";
        }

        private static string FormatPointer(object? arg)
        {
            return "0x0"; // Simplified — real pointers not tracked
        }

        private static string FormatType(object? arg)
        {
            if (arg == null) return "<nil>";
            if (arg is int) return "int";
            if (arg is long) return "int64";
            if (arg is string) return "string";
            if (arg is bool) return "bool";
            if (arg is double) return "float64";
            if (arg is float) return "float32";
            return arg.GetType().Name;
        }

        public static (long, string) Fprintf(IGoWriter w, string format, params object?[] args)
        {
            var s = Sprintf(format, args);
            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            var slice = new Slice<byte>(bytes);
            var (n, err) = w.Write(slice);
            return ((long)n, err);
        }

        public static (long, string) Fprintln(IGoWriter w, params object?[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(FormatValue(args[i]));
            }
            sb.Append('\n');
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var slice = new Slice<byte>(bytes);
            var (n, err) = w.Write(slice);
            return ((long)n, err);
        }

        public static (long, string) Fprint(IGoWriter w, params object?[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0 && !(args[i] is string) && (i == 0 || !(args[i - 1] is string)))
                    sb.Append(' ');
                sb.Append(FormatValue(args[i]));
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var slice = new Slice<byte>(bytes);
            var (n, err) = w.Write(slice);
            return ((long)n, err);
        }
    }
}

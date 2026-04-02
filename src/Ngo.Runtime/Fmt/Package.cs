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
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Fmt
{
    [GoPackage("fmt")]
    public static class Package
    {
        public static (long, string) Println(params object?[] args)
        {
            long n = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) { Console.Write(" "); n++; }
                var s = FormatValue(args[i]);
                Console.Write(s);
                n += s.Length;
            }
            Console.WriteLine();
            n++;
            return (n, "");
        }

        public static (long, string) Print(params object?[] args)
        {
            long n = 0;
            for (int i = 0; i < args.Length; i++)
            {
                // Print uses spaces between non-string operands
                if (i > 0 && !(args[i - 1] is string) && !(args[i] is string))
                {
                    Console.Write(" ");
                    n++;
                }
                var s = FormatValue(args[i]);
                Console.Write(s);
                n += s.Length;
            }
            return (n, "");
        }

        public static (long, string) Printf(string format, params object?[] args)
        {
            var s = Sprintf(format, args);
            Console.Write(s);
            return (s.Length, "");
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

        [GoFunc(IsVariadic = true)]
        [return: GoReturn("error")]
        public static object Errorf(string format, params object?[] args)
        {
            // Check for %w verb (error wrapping)
            int wIdx = format.IndexOf("%w");
            if (wIdx >= 0)
            {
                // Find which arg corresponds to %w
                int argIndex = 0;
                for (int i = 0; i < wIdx; i++)
                {
                    if (format[i] == '%' && i + 1 < format.Length && format[i + 1] != '%')
                        argIndex++;
                }

                object? wrappedErr = argIndex < args.Length ? args[argIndex] : null;

                // Replace %w with %v for display
                var displayFormat = format.Substring(0, wIdx) + "%v" + format.Substring(wIdx + 2);
                string message = Sprintf(displayFormat, args);

                return new WrappedError(message, wrappedErr);
            }

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

            var type = arg.GetType();

            // Check for interface wrapper: unwrap _value and dispatch on inner value
            var valueField = type.GetField("_value");
            if (valueField != null)
            {
                var innerValue = valueField.GetValue(arg);
                if (innerValue != null)
                {
                    // Use wrapper's ToString if it overrides (e.g., error wrappers)
                    var wrapperStr = arg.ToString();
                    if (wrapperStr != null && wrapperStr != type.FullName && wrapperStr != type.Name)
                    {
                        return wrapperStr;
                    }
                    return FormatValue(innerValue);
                }
            }

            // Error() method (Go's error interface — checked before Stringer)
            var errorMethod = type.GetMethod("Error", Type.EmptyTypes);
            if (errorMethod != null && errorMethod.ReturnType == typeof(string))
            {
                var result = errorMethod.Invoke(arg, null);
                if (result is string errorStr) return errorStr;
            }

            // String() method (Go's fmt.Stringer interface)
            var stringMethod = type.GetMethod("String", Type.EmptyTypes);
            if (stringMethod != null && stringMethod.ReturnType == typeof(string)
                && stringMethod.DeclaringType != typeof(object))
            {
                var result = stringMethod.Invoke(arg, null);
                if (result is string s) return s;
            }

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
            if (arg == null)
            {
                return "0x0";
            }
            int hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(arg);
            return $"0x{hash:x}";
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
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            var slice = new Slice<byte>(bytes);
            var (n, err) = w.Write(slice);
            return (n, err);
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
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var slice = new Slice<byte>(bytes);
            var (n, err) = w.Write(slice);
            return (n, err);
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
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var slice = new Slice<byte>(bytes);
            var (n, err) = w.Write(slice);
            return (n, err);
        }

        /// <summary>
        /// fmt.Sscan(str string, a ...interface{}) (n int, err error)
        /// Scans space-separated values from a string into the provided pointers.
        /// </summary>
        public static (long, object?) Sscan(string str, params object?[] args)
        {
            var parts = str.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            int n = 0;
            for (int i = 0; i < args.Length && i < parts.Length; i++)
            {
                if (ScanValue(parts[i], args[i]))
                    n++;
                else
                    return ((long)n, "scan error");
            }
            return ((long)n, null);
        }

        /// <summary>
        /// fmt.Sscanf(str string, format string, a ...interface{}) (n int, err error)
        /// Scans values from str according to format into the provided pointers.
        /// Simplified: extracts whitespace-separated tokens and assigns by type.
        /// </summary>
        public static (long, object?) Sscanf(string str, string format, params object?[] args)
        {
            // Simplified: ignore format verbs and just scan whitespace-separated tokens
            return Sscan(str, args);
        }

        /// <summary>
        /// fmt.Sscanln(str string, a ...interface{}) (n int, err error)
        /// Like Sscan but stops at a newline.
        /// </summary>
        public static (long, object?) Sscanln(string str, params object?[] args)
        {
            var line = str.Split('\n')[0];
            return Sscan(line, args);
        }

        /// <summary>
        /// fmt.Scan(a ...interface{}) (n int, err error)
        /// Scans space-separated values from stdin into the provided pointers.
        /// </summary>
        public static (long, object?) Scan(params object?[] args)
        {
            var line = Console.ReadLine();
            if (line == null)
                return (0L, "EOF");
            return Sscan(line, args);
        }

        /// <summary>
        /// fmt.Scanf(format string, a ...interface{}) (n int, err error)
        /// Scans values from stdin according to format into the provided pointers.
        /// </summary>
        public static (long, object?) Scanf(string format, params object?[] args)
        {
            var line = Console.ReadLine();
            if (line == null)
                return (0L, "EOF");
            return Sscanf(line, format, args);
        }

        /// <summary>
        /// fmt.Scanln(a ...interface{}) (n int, err error)
        /// Scans a single line from stdin into the provided pointers.
        /// </summary>
        public static (long, object?) Scanln(params object?[] args)
        {
            var line = Console.ReadLine();
            if (line == null)
                return (0L, "EOF");
            return Sscanln(line, args);
        }

        public static (long, object?) Fscan(object? r, params object?[] args)
        {
            // Read from an io.Reader (simplified: read line then scan)
            if (r is Io.IGoReader reader)
            {
                var buf = new byte[4096];
                var slice = new Slice<byte>(buf);
                var (n, _) = reader.Read(slice);
                var line = global::System.Text.Encoding.UTF8.GetString(buf, 0, (int)n).TrimEnd('\n', '\r');
                return Sscan(line, args);
            }
            // Fallback for stdin-like objects
            var input = Console.ReadLine();
            if (input == null)
                return (0L, "EOF");
            return Sscan(input, args);
        }

        public static (long, object?) Fscanf(object? r, string format, params object?[] args)
        {
            return Fscan(r, args);
        }

        public static (long, object?) Fscanln(object? r, params object?[] args)
        {
            return Fscan(r, args);
        }

        private static bool ScanValue(string token, object? target)
        {
            if (target is Ptr<long> pi)
            {
                if (long.TryParse(token, out var v)) { pi.Value = v; return true; }
                return false;
            }
            if (target is Ptr<double> pf)
            {
                if (double.TryParse(token, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
                    { pf.Value = v; return true; }
                return false;
            }
            if (target is Ptr<bool> pb)
            {
                if (bool.TryParse(token, out var v)) { pb.Value = v; return true; }
                return false;
            }
            if (target is Ptr<int> p32)
            {
                if (int.TryParse(token, out var v)) { p32.Value = v; return true; }
                return false;
            }
            return false;
        }

        // fmt.Append (Go 1.19+)
        [GoFunc(IsVariadic = true)]
        public static Slice<byte> Append(Slice<byte> b, params object?[] a)
        {
            var s = Sprint(a);
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            return Slice<byte>.Append(b, bytes);
        }

        // fmt.Appendf (Go 1.19+)
        [GoFunc(IsVariadic = true)]
        public static Slice<byte> Appendf(Slice<byte> b, string format, params object?[] a)
        {
            var s = Sprintf(format, a);
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            return Slice<byte>.Append(b, bytes);
        }

        // fmt.Appendln (Go 1.19+)
        [GoFunc(IsVariadic = true)]
        public static Slice<byte> Appendln(Slice<byte> b, params object?[] a)
        {
            var s = Sprintln(a);
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(s);
            return Slice<byte>.Append(b, bytes);
        }
    }
}

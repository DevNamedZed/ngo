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
using System.IO;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log
{
    [GoPackage("log")]
    public static class Package
    {
        private static Logger _defaultLogger = new Logger(Console.Error, default, LstdFlags);

        private static TextWriter? _output;
        private static long _flags = LstdFlags;
        private static GoString _prefix = default;

        public const long Ldate = 1;
        public const long Ltime = 2;
        public const long Lmicroseconds = 4;
        public const long Llongfile = 8;
        public const long Lshortfile = 16;
        public const long LUTC = 32;
        public const long Lmsgprefix = 64;
        public const long LstdFlags = Ldate | Ltime;

        [GoFunc(IsVariadic = true)]
        public static void Print(params object?[] args)
        {
            WriteOutput(FormatArgs(args));
        }

        [GoFunc(IsVariadic = true)]
        public static void Printf(GoString format, params object?[] args)
        {
            var formatted = Fmt.Package.Sprintf(format.ToNetString(), args).ToNetString();
            WriteOutput(formatted);
        }

        [GoFunc(IsVariadic = true)]
        public static void Println(params object?[] args)
        {
            WriteOutput(FormatArgsLine(args));
        }

        [GoFunc(IsVariadic = true)]
        public static void Fatal(params object?[] args)
        {
            WriteOutput(FormatArgs(args));
            Environment.Exit(1);
        }

        [GoFunc(IsVariadic = true)]
        public static void Fatalf(GoString format, params object?[] args)
        {
            var formatted = Fmt.Package.Sprintf(format.ToNetString(), args).ToNetString();
            WriteOutput(formatted);
            Environment.Exit(1);
        }

        [GoFunc(IsVariadic = true)]
        public static void Fatalln(params object?[] args)
        {
            WriteOutput(FormatArgsLine(args));
            Environment.Exit(1);
        }

        [GoFunc(IsVariadic = true)]
        public static void Panic(params object?[] args)
        {
            var message = FormatArgs(args);
            WriteOutput(message);
            throw new GoPanicException(message);
        }

        [GoFunc(IsVariadic = true)]
        public static void Panicf(GoString format, params object?[] args)
        {
            var formatted = Fmt.Package.Sprintf(format.ToNetString(), args).ToNetString();
            WriteOutput(formatted);
            throw new GoPanicException(formatted);
        }

        [GoFunc(IsVariadic = true)]
        public static void Panicln(params object?[] args)
        {
            var message = FormatArgsLine(args);
            WriteOutput(message);
            throw new GoPanicException(message);
        }

        [GoFunc]
        public static void SetOutput([GoParam("io.Writer")] object writer)
        {
            if (writer is TextWriter textWriter)
            {
                _output = textWriter;
            }
        }

        [GoFunc]
        public static void SetFlags(long flag)
        {
            _flags = flag;
        }

        [GoFunc]
        public static long Flags()
        {
            return _flags;
        }

        [GoFunc]
        public static void SetPrefix(GoString prefix)
        {
            _prefix = prefix;
        }

        [GoFunc]
        public static GoString Prefix()
        {
            return _prefix;
        }

        [GoFunc]
        [return: GoReturn("*log.Logger")]
        public static Logger Default()
        {
            return _defaultLogger;
        }

        [GoFunc]
        [return: GoReturn("*log.Logger")]
        public static Logger New([GoParam("io.Writer")] object output, GoString prefix, long flag)
        {
            TextWriter writer = Console.Error;
            if (output is TextWriter textWriter)
            {
                writer = textWriter;
            }
            return new Logger(writer, prefix, flag);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Output(long calldepth, GoString s)
        {
            WriteOutput(s.ToNetString());
            return null;
        }

        private static void WriteOutput(string message)
        {
            var now = DateTime.Now;
            var sb = new System.Text.StringBuilder();

            if ((_flags & Lmsgprefix) == 0)
            {
                sb.Append(_prefix.ToNetString());
            }

            if ((_flags & Ldate) != 0)
            {
                sb.Append(now.ToString("yyyy/MM/dd "));
            }

            if ((_flags & Ltime) != 0)
            {
                sb.Append(now.ToString("HH:mm:ss "));
            }

            if ((_flags & Lmsgprefix) != 0)
            {
                sb.Append(_prefix.ToNetString());
            }

            sb.Append(message);

            if (message.Length == 0 || message[message.Length - 1] != '\n')
            {
                sb.Append('\n');
            }

            var writer = _output ?? Console.Error;
            writer.Write(sb.ToString());
            writer.Flush();
        }

        private static string FormatArgs(object?[] args)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(Fmt.Package.FormatValue(args[i]));
            }
            return sb.ToString();
        }

        private static string FormatArgsLine(object?[] args)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(Fmt.Package.FormatValue(args[i]));
            }
            sb.Append('\n');
            return sb.ToString();
        }
    }
}

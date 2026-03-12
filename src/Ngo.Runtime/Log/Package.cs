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
using System.Text;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log
{
    [GoPackage("log")]
    public static partial class Package
    {
        // log flag constants
        [GoConst(Type = "int")]
        public const long Ldate = 1;

        [GoConst(Type = "int")]
        public const long Ltime = 2;

        [GoConst(Type = "int")]
        public const long Lmicroseconds = 4;

        [GoConst(Type = "int")]
        public const long Llongfile = 8;

        [GoConst(Type = "int")]
        public const long Lshortfile = 16;

        [GoConst(Type = "int")]
        public const long LUTC = 32;

        [GoConst(Type = "int")]
        public const long Lmsgprefix = 64;

        [GoConst(Type = "int")]
        public const long LstdFlags = Ldate | Ltime;

        // Default logger
        private static Logger _defaultLogger = new Logger(Console.Error, "", LstdFlags);

        // log.Println(v ...interface{})
        [GoFunc(IsVariadic = true)]
        public static void Println([GoParam("interface{}")] params object[] args)
        {
            _defaultLogger.Println(args);
        }

        // log.Print(v ...interface{})
        [GoFunc(IsVariadic = true)]
        public static void Print([GoParam("interface{}")] params object[] args)
        {
            _defaultLogger.Print(args);
        }

        // log.Printf(format string, v ...interface{})
        [GoFunc(IsVariadic = true)]
        public static void Printf(string format, [GoParam("interface{}")] params object[] args)
        {
            var result = Fmt.Package.Sprintf(format, args);
            Console.Error.Write(result);
        }

        // log.Fatal(v ...interface{})
        [GoFunc(IsVariadic = true)]
        public static void Fatal([GoParam("interface{}")] params object[] args)
        {
            _defaultLogger.Println(args);
            Environment.Exit(1);
        }

        // log.Fatalf(format string, v ...interface{})
        [GoFunc(IsVariadic = true)]
        public static void Fatalf(string format, [GoParam("interface{}")] params object[] args)
        {
            _defaultLogger.Printf(format, args);
            Console.Error.WriteLine();
            Environment.Exit(1);
        }

        // log.Fatalln(v ...interface{})
        [GoFunc(IsVariadic = true)]
        public static void Fatalln([GoParam("interface{}")] params object[] args)
        {
            _defaultLogger.Println(args);
            Environment.Exit(1);
        }

        // log.Panic(v ...interface{})
        [GoFunc(IsVariadic = true)]
        public static void Panic([GoParam("interface{}")] params object[] args)
        {
            var s = FormatArgs(args);
            Console.Error.Write(s);
            throw new GoPanicException(s);
        }

        // log.Panicf(format string, v ...interface{})
        [GoFunc(IsVariadic = true)]
        public static void Panicf(string format, [GoParam("interface{}")] params object[] args)
        {
            var s = Fmt.Package.Sprintf(format, args);
            Console.Error.Write(s);
            throw new GoPanicException(s);
        }

        // log.Panicln(v ...interface{})
        [GoFunc(IsVariadic = true)]
        public static void Panicln([GoParam("interface{}")] params object[] args)
        {
            var s = FormatArgs(args);
            Console.Error.WriteLine(s);
            throw new GoPanicException(s);
        }

        // log.SetFlags(flag int)
        [GoFunc]
        public static void SetFlags([GoParam("int")] long flag)
        {
            _defaultLogger.SetFlags(flag);
        }

        // log.Flags() int
        [GoFunc]
        [return: GoReturn("int")]
        public static long Flags()
        {
            return _defaultLogger.Flags();
        }

        // log.SetOutput(w io.Writer)
        [GoFunc]
        public static void SetOutput([GoParam("io.Writer")] object w)
        {
            _defaultLogger.SetOutput(w);
        }

        // log.SetPrefix(prefix string)
        [GoFunc]
        public static void SetPrefix(string prefix)
        {
            _defaultLogger.SetPrefix(prefix);
        }

        // log.Prefix() string
        [GoFunc]
        public static string Prefix()
        {
            return _defaultLogger.Prefix();
        }

        // log.Output(calldepth int, s string) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Output([GoParam("int")] long calldepth, string s)
        {
            return _defaultLogger.Output(calldepth, s);
        }

        // log.New(out io.Writer, prefix string, flag int) *Logger
        [GoFunc]
        [return: GoReturn("*Logger")]
        public static Logger New([GoParam("io.Writer")] object @out, string prefix, [GoParam("int")] long flag)
        {
            return new Logger(@out, prefix, flag);
        }

        // log.Default() *Logger
        [GoFunc]
        [return: GoReturn("*Logger")]
        public static Logger Default()
        {
            return _defaultLogger;
        }

        // log.Writer() io.Writer
        [GoFunc]
        [return: GoReturn("io.Writer")]
        public static object Writer()
        {
            return _defaultLogger.WriterValue();
        }

        internal static string FormatArgs(object[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(BuiltIn.FormatArg(args[i]));
            }
            return sb.ToString();
        }
    }
}

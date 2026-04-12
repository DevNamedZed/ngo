// -----------------------------------------------------------------------
// <copyright file="Logger.cs" company="Ziad">
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
    [GoType("struct", Name = "Logger", Package = "log")]
    public sealed class Logger
    {
        private TextWriter _output;
        private GoString _prefix;
        private long _flag;

        public Logger(TextWriter output, GoString prefix, long flag)
        {
            _output = output;
            _prefix = prefix;
            _flag = flag;
        }

        [GoMethod]
        public void SetOutput([GoParam("io.Writer")] object writer)
        {
            if (writer is TextWriter textWriter)
            {
                _output = textWriter;
            }
        }

        [GoMethod]
        public void SetFlags(long flag)
        {
            _flag = flag;
        }

        [GoMethod]
        public long Flags()
        {
            return _flag;
        }

        [GoMethod]
        public void SetPrefix(GoString prefix)
        {
            _prefix = prefix;
        }

        [GoMethod]
        public GoString Prefix()
        {
            return _prefix;
        }

        [GoMethod(IsVariadic = true)]
        public void Println(params object?[] args)
        {
            WriteOutput(FormatArgsLine(args));
        }

        [GoMethod(IsVariadic = true)]
        public void Printf(GoString format, params object?[] args)
        {
            var formatted = Fmt.Package.Sprintf(format.ToNetString(), args).ToNetString();
            WriteOutput(formatted);
        }

        [GoMethod(IsVariadic = true)]
        public void Print(params object?[] args)
        {
            WriteOutput(FormatArgs(args));
        }

        [GoMethod(IsVariadic = true)]
        public void Fatal(params object?[] args)
        {
            WriteOutput(FormatArgs(args));
            Environment.Exit(1);
        }

        [GoMethod(IsVariadic = true)]
        public void Fatalf(GoString format, params object?[] args)
        {
            var formatted = Fmt.Package.Sprintf(format.ToNetString(), args).ToNetString();
            WriteOutput(formatted);
            Environment.Exit(1);
        }

        [GoMethod(IsVariadic = true)]
        public void Fatalln(params object?[] args)
        {
            WriteOutput(FormatArgsLine(args));
            Environment.Exit(1);
        }

        [GoMethod(IsVariadic = true)]
        public void Panic(params object?[] args)
        {
            var message = FormatArgs(args);
            WriteOutput(message);
            throw new GoPanicException(message);
        }

        [GoMethod(IsVariadic = true)]
        public void Panicf(GoString format, params object?[] args)
        {
            var formatted = Fmt.Package.Sprintf(format.ToNetString(), args).ToNetString();
            WriteOutput(formatted);
            throw new GoPanicException(formatted);
        }

        [GoMethod(IsVariadic = true)]
        public void Panicln(params object?[] args)
        {
            var message = FormatArgsLine(args);
            WriteOutput(message);
            throw new GoPanicException(message);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Output(long calldepth, GoString message)
        {
            WriteOutput(message.ToNetString());
            return null;
        }

        [GoMethod]
        [return: GoReturn("io.Writer")]
        public object Writer()
        {
            return _output;
        }

        private void WriteOutput(string message)
        {
            var now = DateTime.Now;
            var sb = new System.Text.StringBuilder();

            if ((_flag & Package.Lmsgprefix) == 0)
            {
                sb.Append(_prefix.ToNetString());
            }

            if ((_flag & Package.Ldate) != 0)
            {
                sb.Append(now.ToString("yyyy/MM/dd "));
            }

            if ((_flag & Package.Ltime) != 0)
            {
                sb.Append(now.ToString("HH:mm:ss "));
            }

            if ((_flag & Package.Lmsgprefix) != 0)
            {
                sb.Append(_prefix.ToNetString());
            }

            sb.Append(message);

            if (message.Length == 0 || message[message.Length - 1] != '\n')
            {
                sb.Append('\n');
            }

            _output.Write(sb.ToString());
            _output.Flush();
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

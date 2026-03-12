// -----------------------------------------------------------------------
// <copyright file="ConsoleLog.cs" company="Ziad">
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
using Ngo.Compiler;

namespace Ngo.Cli
{
    /// <summary>
    /// Writes compiler diagnostics to stderr for CLI use.
    /// Verbose mode controls whether Info/Debug messages are shown.
    /// </summary>
    public sealed class ConsoleLog : ICompilerLog
    {
        private readonly bool _verbose;

        public ConsoleLog(bool verbose)
        {
            _verbose = verbose;
        }

        public void Error(string message)
        {
            Console.Error.WriteLine($"ngo: error: {message}");
        }

        public void Warn(string message)
        {
            Console.Error.WriteLine($"ngo: warning: {message}");
        }

        public void Info(string message)
        {
            if (_verbose)
            {
                Console.Error.WriteLine($"ngo: {message}");
            }
        }

        public void Debug(string message)
        {
            if (_verbose)
            {
                Console.Error.WriteLine($"ngo: debug: {message}");
            }
        }
    }
}

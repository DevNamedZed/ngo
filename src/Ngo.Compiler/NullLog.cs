// -----------------------------------------------------------------------
// <copyright file="NullLog.cs" company="Ziad">
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

namespace Ngo.Compiler
{
    /// <summary>
    /// Default log implementation used when no explicit log is configured.
    /// Error and Warn go to stderr so that compilation problems are never hidden.
    /// Info and Debug are discarded — those are verbose-only output channels.
    /// </summary>
    public sealed class NullLog : ICompilerLog
    {
        public static readonly NullLog Instance = new();

        public void Error(string message)
        {
            Console.Error.WriteLine($"error: {message}");
        }

        public void Warn(string message)
        {
            Console.Error.WriteLine($"warning: {message}");
        }

        public void Info(string message) { }
        public void Debug(string message) { }
    }
}

// -----------------------------------------------------------------------
// <copyright file="GoPanicException.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    /// <summary>
    /// Represents a Go panic. Thrown by panic() and caught by recover() in deferred functions.
    /// </summary>
    public class GoPanicException : Exception
    {
        public GoPanicException(object? value)
            : base(value?.ToString() ?? "panic: nil")
        {
            Value = value;
        }

        /// <summary>The value passed to panic().</summary>
        public object? Value { get; }
    }
}

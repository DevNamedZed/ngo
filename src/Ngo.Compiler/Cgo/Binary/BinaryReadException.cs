// -----------------------------------------------------------------------
// <copyright file="BinaryReadException.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Binary
{
    /// <summary>
    /// Thrown when a read from a raw byte buffer cannot complete —
    /// fixed-width integer truncation, a UTF-8 string that runs off
    /// the end of the buffer without a null terminator, or any other
    /// binary-level failure detected by the readers in this layer.
    /// Subclassed by <see cref="Leb128ParseException"/> so callers
    /// can catch all binary-layer failures with a single handler or
    /// narrow to the LEB128-specific cases when that is useful.
    /// Every instance carries the byte offset at which the failing
    /// read began so higher-level diagnostics can name a location
    /// inside a DWARF section or an ELF header.
    /// </summary>
    public class BinaryReadException : Exception
    {
        public BinaryReadException(string message, int offset)
            : base(message)
        {
            Offset = offset;
        }

        /// <summary>
        /// Byte offset in the source buffer at which the failing read
        /// began. Reported back so higher-level error messages can
        /// anchor diagnostics to a section offset.
        /// </summary>
        public int Offset { get; }
    }
}

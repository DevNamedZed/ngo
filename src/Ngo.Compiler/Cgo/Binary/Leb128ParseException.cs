// -----------------------------------------------------------------------
// <copyright file="Leb128ParseException.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Binary
{
    /// <summary>
    /// Thrown when a LEB128-encoded integer cannot be decoded — either
    /// because the input byte sequence is truncated (the continuation
    /// bit is set on the last available byte) or because the value
    /// exceeds the 64-bit range the decoder returns. Both conditions
    /// indicate malformed DWARF input; the exception carries the byte
    /// offset at which decoding began so the caller can surface a
    /// diagnostic anchored to a section offset.
    /// </summary>
    public sealed class Leb128ParseException : BinaryReadException
    {
        public Leb128ParseException(string message, int startOffset)
            : base(message, startOffset)
        {
        }

        /// <summary>
        /// Byte offset in the input buffer at which LEB128 decoding
        /// began. Alias for the inherited <see cref="BinaryReadException.Offset"/>
        /// retained so LEB128-specific diagnostics read naturally at
        /// their call sites.
        /// </summary>
        public int StartOffset
        {
            get { return Offset; }
        }
    }
}

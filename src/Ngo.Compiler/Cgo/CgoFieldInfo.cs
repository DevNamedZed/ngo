// -----------------------------------------------------------------------
// <copyright file="CgoFieldInfo.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Immutable description of a C struct or union field as it
    /// appears in the symbol catalog. Carries the textual C type
    /// alongside numeric layout data — byte offset, byte size, and
    /// bitfield coordinates — so downstream consumers can emit
    /// <c>[FieldOffset]</c> attributes or marshalling code without
    /// re-deriving offsets from debug info.
    ///
    /// A non-bitfield is represented by <see cref="BitSize"/> equal
    /// to zero; <see cref="BitOffset"/> is then irrelevant and also
    /// zero. Bitfield members set both values to the bit coordinates
    /// within the enclosing storage word as resolved from
    /// <c>DW_AT_data_bit_offset</c>.
    /// </summary>
    public sealed class CgoFieldInfo
    {
        public CgoFieldInfo(
            string name,
            string cType,
            long offsetBytes,
            long sizeBytes,
            int bitOffset,
            int bitSize)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (cType == null)
            {
                throw new ArgumentNullException(nameof(cType));
            }
            if (offsetBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offsetBytes), offsetBytes, "Field offset must be non-negative.");
            }
            if (sizeBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sizeBytes), sizeBytes, "Field size must be non-negative.");
            }
            if (bitOffset < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bitOffset), bitOffset, "Bit offset must be non-negative.");
            }
            if (bitSize < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bitSize), bitSize, "Bit size must be non-negative.");
            }

            Name = name;
            CType = cType;
            OffsetBytes = offsetBytes;
            SizeBytes = sizeBytes;
            BitOffset = bitOffset;
            BitSize = bitSize;
        }

        public string Name { get; }

        public string CType { get; }

        public long OffsetBytes { get; }

        public long SizeBytes { get; }

        public int BitOffset { get; }

        public int BitSize { get; }

        public bool IsBitfield
        {
            get { return BitSize > 0; }
        }
    }
}

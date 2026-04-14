// -----------------------------------------------------------------------
// <copyright file="DwarfResolvedField.cs" company="Ziad">
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
using Ngo.Compiler.Cgo.Dwarf;

namespace Ngo.Compiler.Cgo.Symbols
{
    /// <summary>
    /// One field within a <see cref="DwarfResolvedStructLayout"/>.
    /// Offset and size are in bytes; <see cref="BitOffset"/> and
    /// <see cref="BitSize"/> are populated only for bitfields and
    /// zero otherwise. Bitfields use DWARF 4+ semantics — the legacy
    /// <c>DW_AT_bit_offset</c> is rejected at parse time per
    /// hardening item #4 — so <see cref="BitOffset"/> is always the
    /// least-significant-bit-first offset relative to the start of
    /// the byte at <see cref="OffsetBytes"/>.
    /// </summary>
    public sealed class DwarfResolvedField
    {
        public DwarfResolvedField(
            string name,
            DwarfDie typeDie,
            long offsetBytes,
            long sizeBytes,
            int bitOffset,
            int bitSize)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (typeDie == null)
            {
                throw new ArgumentNullException(nameof(typeDie));
            }
            if (offsetBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offsetBytes), "Field offset cannot be negative.");
            }
            if (sizeBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sizeBytes), "Field size cannot be negative.");
            }
            if (bitOffset < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bitOffset), "Field bit offset cannot be negative.");
            }
            if (bitSize < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bitSize), "Field bit size cannot be negative.");
            }

            Name = name;
            TypeDie = typeDie;
            OffsetBytes = offsetBytes;
            SizeBytes = sizeBytes;
            BitOffset = bitOffset;
            BitSize = bitSize;
        }

        public string Name { get; }

        /// <summary>
        /// The field's underlying type after unwrapping
        /// typedef/const/volatile/restrict/atomic. Never a type-alias
        /// DIE.
        /// </summary>
        public DwarfDie TypeDie { get; }

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

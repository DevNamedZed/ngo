// -----------------------------------------------------------------------
// <copyright file="DebugSection.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.ObjectFile
{
    /// <summary>
    /// One named debug section extracted from an object file. Carries
    /// the section's canonical name (<c>.debug_info</c>,
    /// <c>.debug_abbrev</c>, ...) and its raw bytes. The DWARF reader
    /// matches sections by exact name and feeds the bytes to
    /// <see cref="Binary.BinaryReaderLittleEndian"/>.
    /// </summary>
    public sealed class DebugSection
    {
        public DebugSection(string name, byte[] data)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            Name = name;
            Data = data;
        }

        /// <summary>
        /// Canonical section name including the leading dot
        /// (e.g. <c>.debug_info</c>).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Raw section bytes copied out of the object file. The buffer
        /// is owned by the section and must not be mutated by callers.
        /// </summary>
        public byte[] Data { get; }
    }
}

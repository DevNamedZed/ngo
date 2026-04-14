// -----------------------------------------------------------------------
// <copyright file="ObjectFileContents.cs" company="Ziad">
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
using System.Collections.Generic;

namespace Ngo.Compiler.Cgo.ObjectFile
{
    /// <summary>
    /// Everything an <see cref="IObjectFileReader"/> knows about an
    /// object file after a single <see cref="IObjectFileReader.Read"/>
    /// call: the target pointer width (needed by the DWARF reader to
    /// size <c>DW_FORM_addr</c>) and the debug sections by name. The
    /// reader returns this record as an indivisible unit so callers
    /// never observe a half-populated state.
    /// </summary>
    public sealed class ObjectFileContents
    {
        public ObjectFileContents(int pointerSize, IReadOnlyList<DebugSection> debugSections)
        {
            if (pointerSize != 4 && pointerSize != 8)
            {
                throw new ArgumentException(
                    "Pointer size must be 4 or 8; got " + pointerSize + ".",
                    nameof(pointerSize));
            }
            if (debugSections == null)
            {
                throw new ArgumentNullException(nameof(debugSections));
            }
            PointerSize = pointerSize;
            DebugSections = debugSections;
        }

        /// <summary>
        /// Target pointer width in bytes. Four for ELF32 targets,
        /// eight for ELF64 targets. The DWARF reader uses this for
        /// the <c>DW_FORM_addr</c> fixed width; per-CU address size
        /// overrides it when a compilation unit header sets one
        /// explicitly.
        /// </summary>
        public int PointerSize { get; }

        /// <summary>
        /// All debug sections found in the object file, keyed by the
        /// section's canonical name. The list is stable but its order
        /// is an implementation detail of the container reader and
        /// callers must not rely on it.
        /// </summary>
        public IReadOnlyList<DebugSection> DebugSections { get; }
    }
}

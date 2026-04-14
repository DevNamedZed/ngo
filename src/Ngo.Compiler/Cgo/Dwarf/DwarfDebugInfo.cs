// -----------------------------------------------------------------------
// <copyright file="DwarfDebugInfo.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Dwarf
{
    /// <summary>
    /// Top-level result of parsing a <c>.debug_info</c> section:
    /// the list of parsed compilation units plus the dominant DWARF
    /// standard version observed across them. <see cref="Format"/>
    /// reflects the largest version seen; a mixed-version section
    /// is malformed and is rejected by the reader rather than
    /// summarised here.
    /// </summary>
    public sealed class DwarfDebugInfo
    {
        public DwarfDebugInfo(
            IReadOnlyList<DwarfCompilationUnit> compilationUnits, DwarfFormat format)
        {
            if (compilationUnits == null)
            {
                throw new ArgumentNullException(nameof(compilationUnits));
            }

            CompilationUnits = compilationUnits;
            Format = format;
        }

        public IReadOnlyList<DwarfCompilationUnit> CompilationUnits { get; }

        public DwarfFormat Format { get; }
    }
}

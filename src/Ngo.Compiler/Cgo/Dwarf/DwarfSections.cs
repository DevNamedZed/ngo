// -----------------------------------------------------------------------
// <copyright file="DwarfSections.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Dwarf
{
    /// <summary>
    /// Bundles the DWARF sections the parser needs. Lifting the
    /// sections into a typed record lets the reader declare its
    /// inputs precisely instead of accepting a bag of byte arrays
    /// keyed by string — a typo in a section name becomes a compile
    /// error rather than a silent null-section truncation at parse
    /// time.
    ///
    /// <see cref="DebugInfo"/> and <see cref="DebugAbbrev"/> are
    /// always required: without them the parser cannot make any
    /// progress. <see cref="DebugStr"/> and <see cref="DebugLineStr"/>
    /// are optional — if a DIE references them but they are absent,
    /// the parser throws a <see cref="DwarfParseException"/> at the
    /// site of the reference rather than bailing out at construction
    /// time, so callers with only the required sections can still
    /// parse DIE trees that do not use the string sections.
    /// </summary>
    public sealed class DwarfSections
    {
        public DwarfSections(
            byte[] debugInfo,
            byte[] debugAbbrev,
            byte[]? debugStr,
            byte[]? debugLineStr)
        {
            if (debugInfo == null)
            {
                throw new ArgumentNullException(nameof(debugInfo));
            }
            if (debugAbbrev == null)
            {
                throw new ArgumentNullException(nameof(debugAbbrev));
            }

            DebugInfo = debugInfo;
            DebugAbbrev = debugAbbrev;
            DebugStr = debugStr;
            DebugLineStr = debugLineStr;
        }

        public byte[] DebugInfo { get; }

        public byte[] DebugAbbrev { get; }

        /// <summary>
        /// Backing bytes for <see cref="DwarfForm.Strp"/>. Null when
        /// the object file has no <c>.debug_str</c> section.
        /// </summary>
        public byte[]? DebugStr { get; }

        /// <summary>
        /// Backing bytes for <see cref="DwarfForm.LineStrp"/> (DWARF
        /// 5). Null when the object file has no
        /// <c>.debug_line_str</c> section.
        /// </summary>
        public byte[]? DebugLineStr { get; }
    }
}

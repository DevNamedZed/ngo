// -----------------------------------------------------------------------
// <copyright file="DwarfParseException.cs" company="Ziad">
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
    /// Thrown when the DWARF reader cannot make forward progress on a
    /// section — an unknown abbreviation code, an unknown form, a
    /// truncated location expression, a missing CU header field, or
    /// any other content-level malformation. The exception carries
    /// the DWARF section name and the byte offset within that
    /// section so a diagnostic can point at a specific place inside
    /// an object file, which is the only way these bugs stay
    /// tractable when real-world debug info is involved.
    /// </summary>
    public sealed class DwarfParseException : Exception
    {
        public DwarfParseException(string message, string sectionName, int offsetInSection)
            : base(BuildMessage(message, sectionName, offsetInSection))
        {
            SectionName = sectionName;
            OffsetInSection = offsetInSection;
        }

        public DwarfParseException(
            string message, string sectionName, int offsetInSection, Exception innerException)
            : base(BuildMessage(message, sectionName, offsetInSection), innerException)
        {
            SectionName = sectionName;
            OffsetInSection = offsetInSection;
        }

        /// <summary>
        /// Canonical DWARF section name this error came from
        /// (<c>.debug_info</c>, <c>.debug_abbrev</c>, <c>.debug_str</c>,
        /// or <c>.debug_line_str</c>).
        /// </summary>
        public string SectionName { get; }

        /// <summary>
        /// Byte offset inside <see cref="SectionName"/> at which the
        /// failing parse began. Zero-based from the start of the
        /// section's bytes.
        /// </summary>
        public int OffsetInSection { get; }

        private static string BuildMessage(string message, string sectionName, int offsetInSection)
        {
            return sectionName + "@" + offsetInSection + ": " + message;
        }
    }
}

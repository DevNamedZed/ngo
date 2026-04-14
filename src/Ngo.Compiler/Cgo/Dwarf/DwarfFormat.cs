// -----------------------------------------------------------------------
// <copyright file="DwarfFormat.cs" company="Ziad">
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

namespace Ngo.Compiler.Cgo.Dwarf
{
    /// <summary>
    /// Top-level DWARF standard version represented in the parsed
    /// <c>.debug_info</c>. Our anchor probes compile with
    /// <c>-gdwarf-4</c>, so DWARF 4 is the common path; DWARF 5 is
    /// accepted for forward compatibility and because some toolchains
    /// default to 5. DWARF 2 and 3 are out of scope per the spec.
    /// </summary>
    public enum DwarfFormat
    {
        Dwarf4 = 4,
        Dwarf5 = 5,
    }
}

// -----------------------------------------------------------------------
// <copyright file="DwarfUnitFormat.cs" company="Ziad">
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
    /// Per-CU width of section offset and cross-unit reference fields.
    /// Determined at CU-header time: a leading four-byte unit-length
    /// of <c>0xFFFFFFFF</c> signals <see cref="Dwarf64"/> and an
    /// eight-byte length follows; anything else is
    /// <see cref="Dwarf32"/>. The distinction controls
    /// <see cref="DwarfForm.RefAddr"/> and
    /// <see cref="DwarfForm.SecOffset"/> sizing — getting it wrong
    /// mis-aligns every reference in the CU.
    /// </summary>
    public enum DwarfUnitFormat
    {
        Dwarf32,
        Dwarf64,
    }
}

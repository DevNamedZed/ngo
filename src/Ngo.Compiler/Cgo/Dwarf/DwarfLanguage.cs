// -----------------------------------------------------------------------
// <copyright file="DwarfLanguage.cs" company="Ziad">
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
    /// Values for <c>DW_AT_language</c> on a compile-unit DIE. The
    /// cgo path only needs the C dialects — any other source language
    /// in an anchor-probe object file indicates a toolchain surprise
    /// worth surfacing, so the parser casts unknown codes through and
    /// lets the semantic layer decide whether to reject. Named entries
    /// cover every standard DWARF 5 language up through C23 so the
    /// enum is still meaningful when non-C probes appear.
    /// </summary>
    public enum DwarfLanguage
    {
        Unknown = 0x00,
        C89 = 0x01,
        C = 0x02,
        Ada83 = 0x03,
        CPlusPlus = 0x04,
        Cobol74 = 0x05,
        Cobol85 = 0x06,
        Fortran77 = 0x07,
        Fortran90 = 0x08,
        Pascal83 = 0x09,
        Modula2 = 0x0A,
        Java = 0x0B,
        C99 = 0x0C,
        Ada95 = 0x0D,
        Fortran95 = 0x0E,
        Pli = 0x0F,
        ObjC = 0x10,
        ObjCPlusPlus = 0x11,
        Upc = 0x12,
        D = 0x13,
        Python = 0x14,
        OpenCL = 0x15,
        Go = 0x16,
        Modula3 = 0x17,
        Haskell = 0x18,
        CPlusPlus03 = 0x19,
        CPlusPlus11 = 0x1A,
        OCaml = 0x1B,
        Rust = 0x1C,
        C11 = 0x1D,
        Swift = 0x1E,
        Julia = 0x1F,
        Dylan = 0x20,
        CPlusPlus14 = 0x21,
        Fortran03 = 0x22,
        Fortran08 = 0x23,
        RenderScript = 0x24,
        Bliss = 0x25,
        Kotlin = 0x26,
        Zig = 0x27,
        Crystal = 0x28,
        CPlusPlus17 = 0x2A,
        CPlusPlus20 = 0x2B,
        C17 = 0x2C,
        Fortran18 = 0x2D,
        Ada2005 = 0x2E,
        Ada2012 = 0x2F,
    }
}

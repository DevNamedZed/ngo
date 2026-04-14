// -----------------------------------------------------------------------
// <copyright file="SyntheticAbbreviationAttribute.cs" company="Ziad">
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

using Ngo.Compiler.Cgo.Dwarf;

namespace Ngo.Compiler.Tests.Cgo.Dwarf;

/// <summary>
/// Test-only DTO describing one attribute spec the builder will
/// encode into a synthetic abbreviation. Mirrors
/// <see cref="DwarfAbbreviationAttribute"/> but stays in the test
/// project because it is only ever written, never decoded.
/// </summary>
internal sealed class SyntheticAbbreviationAttribute
{
    public SyntheticAbbreviationAttribute(
        DwarfAttribute attribute, DwarfForm form, long implicitConstValue = 0)
    {
        Attribute = attribute;
        Form = form;
        ImplicitConstValue = implicitConstValue;
    }

    public DwarfAttribute Attribute { get; }

    public DwarfForm Form { get; }

    public long ImplicitConstValue { get; }
}

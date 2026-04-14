// -----------------------------------------------------------------------
// <copyright file="IDwarfAttributeValueVisitor.cs" company="Ziad">
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
    /// Visitor over the closed <see cref="DwarfAttributeValue"/>
    /// hierarchy. The closed hierarchy + dispatching visitor replaces
    /// a chain of <c>is</c>/<c>as</c> checks: adding a new concrete
    /// value type forces every visitor to implement the new method,
    /// which is exactly the compile-time safety the Layer-4 type
    /// resolver needs when it grows.
    /// </summary>
    public interface IDwarfAttributeValueVisitor<out T>
    {
        T VisitInteger(DwarfIntegerAttributeValue value);

        T VisitString(DwarfStringAttributeValue value);

        T VisitBlock(DwarfBlockAttributeValue value);

        T VisitFlag(DwarfFlagAttributeValue value);

        T VisitReference(DwarfReferenceAttributeValue value);
    }
}

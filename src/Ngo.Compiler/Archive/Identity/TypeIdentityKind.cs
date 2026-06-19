// -----------------------------------------------------------------------
// <copyright file="TypeIdentityKind.cs" company="Ziad">
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

namespace Ngo.Compiler.Archive.Identity
{
    /// <summary>
    /// The shape discriminator for a <see cref="TypeIdentity"/>. Mirrors <see cref="TypeTokenKind"/>,
    /// except a serialized <c>TypeDef</c> and <c>PackageTypeRef</c> for the same logical type both
    /// canonicalize to <see cref="Named"/> (a <c>(packagePath, name)</c> identity).
    /// </summary>
    internal enum TypeIdentityKind : byte
    {
        Named = 0,
        Primitive = 1,
        Array = 2,
        Pointer = 3,
        ByRef = 4,
        GenericInstance = 5,
        GenericTypeParam = 6,
        GenericMethodParam = 7,
    }
}

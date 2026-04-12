// -----------------------------------------------------------------------
// <copyright file="IFieldBuilder.cs" company="Ziad">
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
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Emit.Refs;

namespace Ngo.Compiler.Emit.Builder
{
    internal interface IFieldBuilder
    {
        void SetCustomAttribute(CustomAttributeBuilder attr);

        /// <summary>
        /// Returns a FieldRef pointing at this field. Used for all emit call sites.
        /// </summary>
        FieldRef AsFieldRef();

        /// <summary>
        /// The field name. Used by NgoWriter to build FieldDef tokens without going through a
        /// reflection proxy.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The field's type. Exposed so NgoWriter can build tokens and so emitters can reason
        /// about the field's shape without materialising a reflection proxy.
        /// </summary>
        Type FieldType { get; }

        /// <summary>
        /// The type that declares this field. For the live path this is the underlying
        /// <see cref="FieldBuilder.DeclaringType"/>; for the archive path it is the lightweight
        /// declaring type tracked by the builder. Exposed so emitters can compare declaring types
        /// without reflecting through a proxy <see cref="FieldInfo"/>.
        /// </summary>
        Type? DeclaringType { get; }
    }
}

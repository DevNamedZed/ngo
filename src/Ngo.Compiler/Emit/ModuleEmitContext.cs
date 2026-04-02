// -----------------------------------------------------------------------
// <copyright file="ModuleEmitContext.cs" company="Ziad">
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
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Module-level emit context. Created once per package compilation.
    /// Holds module-wide state and creates child type/method contexts.
    /// </summary>
    internal sealed class ModuleEmitContext
    {
        public EmitContext Legacy { get; }

        public ModuleEmitContext(EmitContext legacyContext)
        {
            Legacy = legacyContext;
        }

        public TypeMapper Mapper => Legacy.Mapper;
        public IModuleBuilder Module => Legacy.Module;
        public ITypeBuilder PackageType => Legacy.PackageType;

        public Type Resolve(TypeSymbol symbol)
        {
            return Mapper.Map(symbol);
        }

        public TypeEmitContext CreateTypeContext(ITypeBuilder typeBuilder,
            IReadOnlyList<TypeParameterSymbol>? typeParameters = null,
            Type[]? genericParams = null)
        {
            return new TypeEmitContext(this, typeBuilder, typeParameters, genericParams);
        }
    }
}

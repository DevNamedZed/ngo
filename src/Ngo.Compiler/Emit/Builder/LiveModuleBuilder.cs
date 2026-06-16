// -----------------------------------------------------------------------
// <copyright file="LiveModuleBuilder.cs" company="Ziad">
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
using System.Reflection;
using System.Reflection.Emit;

namespace Ngo.Compiler.Emit.Builder
{
    /// <summary>
    /// IModuleBuilder backed by a real System.Reflection.Emit.ModuleBuilder.
    /// Used at final build/run time — wraps the host assembly's module.
    /// </summary>
    internal sealed class LiveModuleBuilder : IModuleBuilder
    {
        private readonly ModuleBuilder _module;

        // Every TypeBuilder defined through this module, by any path (the IModuleBuilder wrapper,
        // ILLinker's linked-type defines, and inline-array defines). .NET requires every defined
        // TypeBuilder to be created before the assembly is serialized; this is the single registry
        // that lets the emitter assert that and name any builder left uncreated. (spec/A4 §A4.1)
        private readonly List<TypeBuilder> _definedTypes = new();

        public LiveModuleBuilder(ModuleBuilder module) => _module = module;

        /// <summary>
        /// Access to the underlying ModuleBuilder for AssemblyEmitter (entry point, PE generation).
        /// </summary>
        public ModuleBuilder Inner => _module;

        public ITypeBuilder DefineType(string name, TypeAttributes attrs)
            => new LiveTypeBuilder(RegisterDefinedType(_module.DefineType(name, attrs)));

        public ITypeBuilder DefineType(string name, TypeAttributes attrs, Type baseType)
            => new LiveTypeBuilder(RegisterDefinedType(_module.DefineType(name, attrs, baseType)));

        public ITypeBuilder DefineType(string name, TypeAttributes attrs, Type? baseType, Type[]? interfaces)
            => new LiveTypeBuilder(RegisterDefinedType(_module.DefineType(name, attrs, baseType, interfaces)));

        /// <summary>
        /// Defines a type on the underlying module and returns the raw <see cref="TypeBuilder"/>,
        /// tracking it for finalization. For callers (ILLinker, inline arrays) that need the raw
        /// builder rather than the <see cref="ITypeBuilder"/> wrapper.
        /// </summary>
        public TypeBuilder DefineTypeTracked(string name, TypeAttributes attrs)
            => RegisterDefinedType(_module.DefineType(name, attrs));

        public TypeBuilder DefineTypeTracked(string name, TypeAttributes attrs, Type? parent)
            => RegisterDefinedType(_module.DefineType(name, attrs, parent));

        public TypeBuilder DefineTypeTracked(string name, TypeAttributes attrs, Type? parent, Type[]? interfaces)
            => RegisterDefinedType(_module.DefineType(name, attrs, parent, interfaces));

        /// <summary>
        /// Records a TypeBuilder defined directly on <see cref="Inner"/> so it is included in the
        /// finalization assert. Returns the same builder for call-site convenience.
        /// </summary>
        public TypeBuilder RegisterDefinedType(TypeBuilder typeBuilder)
        {
            _definedTypes.Add(typeBuilder);
            return typeBuilder;
        }

        /// <summary>
        /// The full names of every defined type builder that has not yet been created. Empty when the
        /// module is ready to serialize.
        /// </summary>
        public IReadOnlyList<string> GetUncreatedTypeNames()
        {
            var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var typeBuilder in _definedTypes)
            {
                var name = typeBuilder.FullName ?? typeBuilder.Name;
                nameCounts[name] = nameCounts.TryGetValue(name, out var count) ? count + 1 : 1;
            }

            var uncreated = new List<string>();
            foreach (var typeBuilder in _definedTypes)
            {
                if (!typeBuilder.IsCreated())
                {
                    var name = typeBuilder.FullName ?? typeBuilder.Name;
                    uncreated.Add($"{name} (defined {nameCounts[name]}×)");
                }
            }
            return uncreated;
        }
    }
}

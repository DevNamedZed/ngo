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

        public LiveModuleBuilder(ModuleBuilder module) => _module = module;

        /// <summary>
        /// Access to the underlying ModuleBuilder for AssemblyEmitter (entry point, PE generation).
        /// </summary>
        public ModuleBuilder Inner => _module;

        public ITypeBuilder DefineType(string name, TypeAttributes attrs)
            => new LiveTypeBuilder(_module.DefineType(name, attrs));

        public ITypeBuilder DefineType(string name, TypeAttributes attrs, Type baseType)
            => new LiveTypeBuilder(_module.DefineType(name, attrs, baseType));

        public ITypeBuilder DefineType(string name, TypeAttributes attrs, Type? baseType, Type[]? interfaces)
            => new LiveTypeBuilder(_module.DefineType(name, attrs, baseType, interfaces));
    }
}

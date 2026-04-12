// -----------------------------------------------------------------------
// <copyright file="IMethodBuilder.cs" company="Ziad">
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
    internal interface IMethodBuilder
    {
        Type[] DefineGenericParameters(string[] names);
        void DefineParameter(int position, ParameterAttributes attrs, string? name);
        void SetReturnType(Type type);
        void SetParameters(Type[] types);
        CilWriter GetILWriter();
        void SetCustomAttribute(CustomAttributeBuilder attr);

        /// <summary>
        /// Returns a MethodRef pointing at this method. Used for all emit call sites.
        /// </summary>
        MethodRef AsMethodRef();

        string Name { get; }
        MethodAttributes Attributes { get; }

        /// <summary>
        /// The method's return type. Used by NgoWriter to build MethodDef tokens without
        /// going through a reflection proxy. Tracks whatever SetReturnType was last given, or
        /// the returnType passed to the constructor (defaulting to typeof(void)).
        /// </summary>
        Type ReturnType { get; }

        /// <summary>
        /// The method's parameter types in declaration order. Used by NgoWriter to build
        /// MethodDef tokens without going through a reflection proxy. Tracks whatever
        /// SetParameters was last given, or the paramTypes passed to the constructor.
        /// </summary>
        Type[] ParameterTypes { get; }

        /// <summary>
        /// The method's declaring type, if known. Live path delegates to the underlying
        /// <see cref="MethodBuilder.DeclaringType"/>; archive path tracks the lightweight
        /// declaring type passed in at construction. Exposed so emitters can reason about
        /// cross-type method membership without going through a reflection proxy.
        /// </summary>
        Type? DeclaringType { get; }

        /// <summary>
        /// Generic type parameters declared on this method (not its declaring type).
        /// Empty for non-generic methods. Used by emitters that need to enumerate
        /// method-level type parameters without materialising a proxy MethodInfo.
        /// </summary>
        Type[] GenericArguments { get; }
    }
}

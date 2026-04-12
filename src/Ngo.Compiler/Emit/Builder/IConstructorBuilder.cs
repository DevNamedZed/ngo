// -----------------------------------------------------------------------
// <copyright file="IConstructorBuilder.cs" company="Ziad">
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
using Ngo.Compiler.Emit.Refs;

namespace Ngo.Compiler.Emit.Builder
{
    internal interface IConstructorBuilder
    {
        CilWriter GetILWriter();

        /// <summary>
        /// Returns a CtorRef pointing at this constructor. Preferred over constructing reflection proxies at call sites.
        /// </summary>
        CtorRef AsCtorRef();

        /// <summary>
        /// The constructor's parameter types in declaration order. Used by NgoWriter to build
        /// MethodDef tokens for .ctor references without going through a reflection proxy.
        /// </summary>
        Type[] ParameterTypes { get; }

        /// <summary>
        /// The constructor's method attributes. Needed by NgoWriter for stack tracking
        /// (static vs. instance, although ctors are instance in practice).
        /// </summary>
        MethodAttributes Attributes { get; }
    }
}

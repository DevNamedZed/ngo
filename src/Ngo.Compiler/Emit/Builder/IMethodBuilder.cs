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

namespace Ngo.Compiler.Emit.Builder
{
    internal interface IMethodBuilder
    {
        Type[] DefineGenericParameters(string[] names);
        void DefineParameter(int position, ParameterAttributes attrs, string? name);
        void SetReturnType(Type type);
        void SetParameters(Type[] types);
        CilWriter GetILWriter();

        /// <summary>
        /// Returns this method as a MethodInfo for use in CilWriter.Emit(OpCodes.Call, method).
        /// Live: returns the wrapped MethodBuilder. Ngo: returns a proxy MethodInfo.
        /// </summary>
        MethodInfo AsMethodInfo();

        string Name { get; }
        MethodAttributes Attributes { get; }
    }
}

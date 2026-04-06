// -----------------------------------------------------------------------
// <copyright file="ITypeBuilder.cs" company="Ziad">
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
    internal interface ITypeBuilder
    {
        IFieldBuilder DefineField(string name, Type type, FieldAttributes attrs);
        IMethodBuilder DefineMethod(string name, MethodAttributes attrs, Type returnType, Type[] paramTypes);
        IMethodBuilder DefineMethod(string name, MethodAttributes attrs);
        IConstructorBuilder DefineConstructor(MethodAttributes attrs, CallingConventions callingConvention, Type[] paramTypes);
        Type[] DefineGenericParameters(string[] names);
        void DefineMethodOverride(IMethodBuilder body, MethodInfo declaration);
        bool IsCreated { get; }
        Type CreateType();

        /// <summary>
        /// Define a P/Invoke method (extern function imported from a native library).
        /// The CLR handles the native transition — no IL body needed.
        /// </summary>
        MethodInfo DefinePInvokeMethod(string name, string dllName, string entryPoint,
            MethodAttributes attrs, CallingConventions callingConvention,
            Type returnType, Type[] paramTypes,
            System.Runtime.InteropServices.CallingConvention nativeCallConv,
            System.Runtime.InteropServices.CharSet charset);

        /// <summary>
        /// Returns this type as a System.Type for use in TypeMapper, CilWriter.Emit, etc.
        /// Live: returns the wrapped TypeBuilder. Ngo: returns a named proxy type.
        /// </summary>
        Type AsType();

        string? FullName { get; }
    }
}

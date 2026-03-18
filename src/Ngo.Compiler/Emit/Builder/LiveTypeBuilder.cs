// -----------------------------------------------------------------------
// <copyright file="LiveTypeBuilder.cs" company="Ziad">
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
    internal sealed class LiveTypeBuilder : ITypeBuilder
    {
        private readonly TypeBuilder _tb;

        public LiveTypeBuilder(TypeBuilder tb) => _tb = tb;

        public string? FullName => _tb.FullName;
        public Type AsType() => _tb;

        public IFieldBuilder DefineField(string name, Type type, FieldAttributes attrs)
            => new LiveFieldBuilder(_tb.DefineField(name, type, attrs));

        public IMethodBuilder DefineMethod(string name, MethodAttributes attrs, Type returnType, Type[] paramTypes)
            => new LiveMethodBuilder(_tb.DefineMethod(name, attrs, returnType, paramTypes));

        public IMethodBuilder DefineMethod(string name, MethodAttributes attrs)
            => new LiveMethodBuilder(_tb.DefineMethod(name, attrs));

        public IConstructorBuilder DefineConstructor(MethodAttributes attrs, CallingConventions callingConvention, Type[] paramTypes)
            => new LiveConstructorBuilder(_tb.DefineConstructor(attrs, callingConvention, paramTypes));

        public Type[] DefineGenericParameters(string[] names)
            => _tb.DefineGenericParameters(names);

        public void DefineMethodOverride(IMethodBuilder body, MethodInfo declaration)
            => _tb.DefineMethodOverride(((LiveMethodBuilder)body).Inner, declaration);

        public Type CreateType() => _tb.CreateType()!;

        public MethodInfo DefinePInvokeMethod(string name, string dllName, string entryPoint,
            MethodAttributes attrs, CallingConventions callingConvention,
            Type returnType, Type[] paramTypes,
            System.Runtime.InteropServices.CallingConvention nativeCallConv,
            System.Runtime.InteropServices.CharSet charset)
        {
            var method = _tb.DefinePInvokeMethod(
                name, dllName, entryPoint,
                attrs, callingConvention,
                returnType, paramTypes,
                nativeCallConv, charset);
            method.SetImplementationFlags(MethodImplAttributes.PreserveSig);
            return method;
        }
    }
}

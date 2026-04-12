// -----------------------------------------------------------------------
// <copyright file="LiveMethodBuilder.cs" company="Ziad">
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
    internal sealed class LiveMethodBuilder : IMethodBuilder
    {
        private readonly MethodBuilder _mb;
        private Type _returnType;
        private Type[] _paramTypes;

        public LiveMethodBuilder(MethodBuilder mb)
            : this(mb, typeof(void), Type.EmptyTypes)
        {
        }

        public LiveMethodBuilder(MethodBuilder mb, Type returnType, Type[] paramTypes)
        {
            _mb = mb;
            _returnType = returnType ?? typeof(void);
            _paramTypes = paramTypes ?? Type.EmptyTypes;
        }

        public MethodBuilder Inner => _mb;
        public string Name => _mb.Name;
        public MethodAttributes Attributes => _mb.Attributes;
        public Type ReturnType => _returnType;
        public Type[] ParameterTypes => _paramTypes;
        public Type? DeclaringType => _mb.DeclaringType;
        public Type[] GenericArguments => _mb.IsGenericMethodDefinition
            ? _mb.GetGenericArguments()
            : Type.EmptyTypes;

        public MethodRef AsMethodRef()
        {
            var declaringType = _mb.DeclaringType
                ?? throw new InvalidOperationException(
                    "LiveMethodBuilder.AsMethodRef: underlying MethodBuilder has no declaring type");
            return MethodRef.FromBuilder(this, TypeRef.FromRuntime(declaringType));
        }

        public Type[] DefineGenericParameters(string[] names)
            => _mb.DefineGenericParameters(names);

        public void DefineParameter(int position, ParameterAttributes attrs, string? name)
            => _mb.DefineParameter(position, attrs, name);

        public void SetReturnType(Type type)
        {
            _returnType = type;
            _mb.SetReturnType(type);
        }

        public void SetParameters(Type[] types)
        {
            _paramTypes = types ?? Type.EmptyTypes;
            _mb.SetParameters(types);
        }

        public void SetCustomAttribute(System.Reflection.Emit.CustomAttributeBuilder attr) => _mb.SetCustomAttribute(attr);

        public CilWriter GetILWriter() => new ILGeneratorWriter(_mb.GetILGenerator());
    }
}

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

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class LiveMethodBuilder : IMethodBuilder
    {
        private readonly MethodBuilder _mb;

        public LiveMethodBuilder(MethodBuilder mb) => _mb = mb;

        public MethodBuilder Inner => _mb;
        public string Name => _mb.Name;
        public MethodAttributes Attributes => _mb.Attributes;
        public MethodInfo AsMethodInfo() => _mb;

        public Type[] DefineGenericParameters(string[] names)
            => _mb.DefineGenericParameters(names);

        public void DefineParameter(int position, ParameterAttributes attrs, string? name)
            => _mb.DefineParameter(position, attrs, name);

        public void SetReturnType(Type type) => _mb.SetReturnType(type);
        public void SetParameters(Type[] types) => _mb.SetParameters(types);

        public CilWriter GetILWriter() => new ILGeneratorWriter(_mb.GetILGenerator());
    }
}

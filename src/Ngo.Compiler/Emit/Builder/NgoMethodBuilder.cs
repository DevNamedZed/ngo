// -----------------------------------------------------------------------
// <copyright file="NgoMethodBuilder.cs" company="Ziad">
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

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoMethodBuilder : IMethodBuilder
    {
        private readonly Type _declaringType;
        private readonly string _name;
        private readonly MethodAttributes _attrs;
        private string _returnTypeName;
        private readonly List<string> _paramTypeNames;
        private NgoWriter? _writer;
        private readonly NgoProxyMethodInfo _proxy;

        public NgoMethodBuilder(Type declaringType, string name, MethodAttributes attrs, Type? returnType, Type[]? paramTypes)
        {
            _declaringType = declaringType;
            _name = name;
            _attrs = attrs;
            _returnTypeName = returnType != null ? NgoWriter.GetTypeNameStatic(returnType) : "System.Void";
            _paramTypeNames = new List<string>();
            if (paramTypes != null)
            {
                foreach (var pt in paramTypes)
                    _paramTypeNames.Add(NgoWriter.GetTypeNameStatic(pt));
            }
            _proxy = new NgoProxyMethodInfo(declaringType, name);
        }

        public string Name => _name;
        public MethodAttributes Attributes => _attrs;
        public string ReturnTypeName => _returnTypeName;
        public IReadOnlyList<string> ParamTypeNames => _paramTypeNames;
        public NgoWriter? Writer => _writer;

        public Type[] DefineGenericParameters(string[] names)
        {
            var result = new Type[names.Length];
            for (int i = 0; i < names.Length; i++)
                result[i] = new NgoProxyType(names[i]);
            return result;
        }

        public void DefineParameter(int position, ParameterAttributes attrs, string? name) { }

        public void SetReturnType(Type type)
        {
            _returnTypeName = NgoWriter.GetTypeNameStatic(type);
        }

        public void SetParameters(Type[] types)
        {
            _paramTypeNames.Clear();
            foreach (var t in types)
                _paramTypeNames.Add(NgoWriter.GetTypeNameStatic(t));
        }

        public CilWriter GetILWriter()
        {
            _writer ??= new NgoWriter();
            return _writer;
        }

        public MethodInfo AsMethodInfo() => _proxy;
    }
}

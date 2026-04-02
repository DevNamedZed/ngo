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
using Ngo.Compiler.Archive;
using System.Collections.Generic;
using System.Reflection;

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoMethodBuilder : IMethodBuilder
    {
        private readonly Type _declaringType;
        private readonly NgoTypeBuilder? _declaringTypeBuilder;
        private readonly string _name;
        private readonly MethodAttributes _attrs;
        private string _returnTypeName;
        private readonly List<string> _paramTypeNames;
        private NgoWriter? _writer;
        private readonly NgoProxyMethodInfo _proxy;
        private string[] _genericParamNames = Array.Empty<string>();
        private Type[] _genericParamTypes = Type.EmptyTypes;

        public NgoMethodBuilder(Type declaringType, string name, MethodAttributes attrs, Type? returnType, Type[]? paramTypes,
            NgoTypeBuilder? declaringTypeBuilder = null)
        {
            _declaringType = declaringType;
            _declaringTypeBuilder = declaringTypeBuilder;
            _name = name;
            _attrs = attrs;
            _returnTypeName = returnType != null ? NgoWriter.GetTypeNameStatic(returnType) : "System.Void";
            _paramTypeNames = new List<string>();
            if (paramTypes != null)
            {
                foreach (var pt in paramTypes)
                    _paramTypeNames.Add(NgoWriter.GetTypeNameStatic(pt));
            }
            _proxy = new NgoProxyMethodInfo(declaringType, name, paramTypes ?? Type.EmptyTypes, returnType ?? typeof(void));
        }

        public string Name => _name;
        public MethodAttributes Attributes => _attrs;
        public string ReturnTypeName => _returnTypeName;
        public IReadOnlyList<string> ParamTypeNames => _paramTypeNames;
        public NgoWriter? Writer => _writer;
        public IReadOnlyList<string> GenericParamNames => _genericParamNames;

        public Type[] DefineGenericParameters(string[] names)
        {
            _genericParamNames = names;
            var result = new Type[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = new NgoProxyType(names[i], i, isMethodGenericParam: true);
            }
            _genericParamTypes = result;
            return result;
        }

        public void DefineParameter(int position, ParameterAttributes attrs, string? name) { }

        public void SetReturnType(Type type)
        {
            _returnTypeName = NgoWriter.GetTypeNameStatic(type);
            _proxy.UpdateReturnType(type);
        }

        public void SetParameters(Type[] types)
        {
            _paramTypeNames.Clear();
            foreach (var t in types)
                _paramTypeNames.Add(NgoWriter.GetTypeNameStatic(t));
            _proxy.UpdateParameterTypes(types);
        }

        public CilWriter GetILWriter()
        {
            if (_writer == null)
            {
                var typeGenericParams = _declaringTypeBuilder?.GenericParamTypes ?? Type.EmptyTypes;
                var context = new Archive.SerializationContext(_genericParamTypes, typeGenericParams);
                _writer = new NgoWriter(context);
            }
            return _writer;
        }

        public MethodInfo AsMethodInfo() => _proxy;
        public void SetCustomAttribute(System.Reflection.Emit.CustomAttributeBuilder attr) { }
    }
}

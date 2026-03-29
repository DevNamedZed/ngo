// -----------------------------------------------------------------------
// <copyright file="NgoTypeBuilder.cs" company="Ziad">
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
using System.Reflection.Emit;

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoTypeBuilder : ITypeBuilder
    {
        private readonly string _fullName;
        private readonly TypeAttributes _attrs;
        private readonly string _baseTypeName;
        private readonly NgoProxyType _proxyType;
        private readonly List<NgoFieldBuilder> _fields = new();
        private readonly List<NgoMethodBuilder> _methods = new();
        private readonly List<NgoMethodOverride> _overrides = new();
        private NgoConstructorBuilder? _constructor;
        private string[] _genericParamNames = Array.Empty<string>();

        public NgoTypeBuilder(string fullName, TypeAttributes attrs, Type? baseType)
        {
            _fullName = fullName;
            _attrs = attrs;

            bool isStatic = (attrs & TypeAttributes.Abstract) != 0 && (attrs & TypeAttributes.Sealed) != 0;
            bool isInterface = (attrs & TypeAttributes.Interface) != 0;

            if (baseType != null)
            {
                _baseTypeName = NgoWriter.GetTypeNameStatic(baseType);
            }
            else if (isStatic || isInterface)
            {
                _baseTypeName = "";
            }
            else
            {
                _baseTypeName = "System.Object";
            }

            bool isValueType = !isStatic && !isInterface && baseType == typeof(ValueType);
            _proxyType = new NgoProxyType(fullName, isValueType);
        }

        public string? FullName => _fullName;
        public TypeAttributes TypeAttrs => _attrs;
        public string BaseTypeName => _baseTypeName;
        public IReadOnlyList<NgoFieldBuilder> Fields => _fields;
        public IReadOnlyList<NgoMethodBuilder> Methods => _methods;
        public IReadOnlyList<NgoMethodOverride> Overrides => _overrides;
        public NgoConstructorBuilder? Constructor => _constructor;
        public IReadOnlyList<string> GenericParamNames => _genericParamNames;

        public Type AsType() => _proxyType;

        public Type CreateType() => _proxyType;

        public IFieldBuilder DefineField(string name, Type type, FieldAttributes attrs)
        {
            var fb = new NgoFieldBuilder(_proxyType, name, type, attrs);
            _fields.Add(fb);
            _proxyType.AddField(name, type);
            return fb;
        }

        public IMethodBuilder DefineMethod(string name, MethodAttributes attrs, Type returnType, Type[] paramTypes)
        {
            var mb = new NgoMethodBuilder(_proxyType, name, attrs, returnType, paramTypes);
            _methods.Add(mb);
            _proxyType.AddMethod(name, returnType, paramTypes);
            return mb;
        }

        public IMethodBuilder DefineMethod(string name, MethodAttributes attrs)
        {
            var mb = new NgoMethodBuilder(_proxyType, name, attrs, null, null);
            _methods.Add(mb);
            _proxyType.AddMethod(name, null!, null!);
            return mb;
        }

        public IConstructorBuilder DefineConstructor(MethodAttributes attrs, CallingConventions callingConvention, Type[] paramTypes)
        {
            _constructor = new NgoConstructorBuilder(attrs, callingConvention, paramTypes);
            return _constructor;
        }

        public Type[] DefineGenericParameters(string[] names)
        {
            _genericParamNames = names;
            var result = new Type[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = new NgoProxyType(names[i]);
            }
            return result;
        }

        public void DefineMethodOverride(IMethodBuilder body, MethodInfo declaration)
        {
            var bodyName = ((NgoMethodBuilder)body).Name;
            var declType = NgoWriter.GetTypeNameStatic(declaration.DeclaringType!);
            var declName = declaration.Name;
            _overrides.Add(new NgoMethodOverride(bodyName, declType, declName));
        }

        public MethodInfo DefinePInvokeMethod(string name, string dllName, string entryPoint,
            MethodAttributes attrs, CallingConventions callingConvention,
            Type returnType, Type[] paramTypes,
            System.Runtime.InteropServices.CallingConvention nativeCallConv,
            System.Runtime.InteropServices.CharSet charset)
        {
            // NgoTypeBuilder is for archive serialization — P/Invoke is not serialized
            // Return a proxy MethodInfo
            return new NgoProxyMethodInfo(_proxyType, name);
        }
    }
}

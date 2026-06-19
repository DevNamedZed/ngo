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
using Ngo.Compiler.Archive;
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
        private readonly Type? _baseType;
        private readonly NgoBuilderType _builderType;
        private readonly List<NgoFieldBuilder> _fields = new();
        private readonly List<NgoMethodBuilder> _methods = new();
        private readonly List<NgoMethodOverride> _overrides = new();
        private NgoConstructorBuilder? _constructor;
        private string[] _genericParamNames = Array.Empty<string>();
        private Type[] _genericParamTypes = Type.EmptyTypes;
        private readonly string[] _interfaceNames;
        private readonly Type[] _interfaceTypes;

        public NgoTypeBuilder(string fullName, TypeAttributes attrs, Type? baseType, Type[]? interfaces = null)
        {
            _fullName = fullName;
            _attrs = attrs;

            bool isStatic = (attrs & TypeAttributes.Abstract) != 0 && (attrs & TypeAttributes.Sealed) != 0;
            bool isInterface = (attrs & TypeAttributes.Interface) != 0;

            if (baseType != null)
            {
                _baseTypeName = NgoWriter.GetTypeNameStatic(baseType);
                _baseType = baseType;
            }
            else if (isStatic || isInterface)
            {
                _baseTypeName = "";
                _baseType = null;
            }
            else
            {
                _baseTypeName = "System.Object";
                _baseType = typeof(object);
            }

            if (interfaces != null && interfaces.Length > 0)
            {
                var validInterfaceNames = new List<string>();
                var validInterfaceTypes = new List<Type>();
                for (int i = 0; i < interfaces.Length; i++)
                {
                    // Skip non-interface types like typeof(object) which is used
                    // as a placeholder for Go's error interface
                    if (interfaces[i] == typeof(object))
                    {
                        continue;
                    }
                    validInterfaceNames.Add(NgoWriter.GetTypeNameStatic(interfaces[i]));
                    validInterfaceTypes.Add(interfaces[i]);
                }
                _interfaceNames = validInterfaceNames.ToArray();
                _interfaceTypes = validInterfaceTypes.ToArray();
            }
            else
            {
                _interfaceNames = Array.Empty<string>();
                _interfaceTypes = Type.EmptyTypes;
            }

            bool isValueType = !isStatic && !isInterface && baseType == typeof(ValueType);
            _builderType = new NgoBuilderType(fullName, isValueType);
        }

        public string? FullName => _fullName;
        public TypeAttributes TypeAttrs => _attrs;
        public string BaseTypeName => _baseTypeName;
        public IReadOnlyList<string> InterfaceNames => _interfaceNames;

        // Structured base/interface type tokens, built from the type's generic context (available by
        // archive-write time, after DefineGenericParameters). Base/interface types are concrete, so
        // these resolve without needing the generic context at link time. Base token is null when the
        // type has no base (interfaces and package static classes).
        public Archive.TypeToken? BaseTypeToken =>
            _baseType != null ? BuildSignatureWriter().BuildTypeToken(_baseType) : null;

        public Archive.TypeToken[] InterfaceTokens
        {
            get
            {
                var writer = BuildSignatureWriter();
                var tokens = new Archive.TypeToken[_interfaceTypes.Length];
                for (int i = 0; i < _interfaceTypes.Length; i++)
                {
                    tokens[i] = writer.BuildTypeToken(_interfaceTypes[i]);
                }
                return tokens;
            }
        }

        private NgoWriter BuildSignatureWriter() =>
            new NgoWriter(new Archive.SerializationContext(Type.EmptyTypes, _genericParamTypes));
        public IReadOnlyList<NgoFieldBuilder> Fields => _fields;
        public IReadOnlyList<NgoMethodBuilder> Methods => _methods;
        public IReadOnlyList<NgoMethodOverride> Overrides => _overrides;
        public NgoConstructorBuilder? Constructor => _constructor;
        public IReadOnlyList<string> GenericParamNames => _genericParamNames;

        public Type AsType() => _builderType;

        public void StampPackagePath(string importPath) => _builderType.StampPackagePath(importPath);

        public bool IsCreated => true;
        public Type CreateType() => _builderType;

        private int _blankFieldIndex;

        public IFieldBuilder DefineField(string name, Type type, FieldAttributes attrs)
        {
            var actualName = name;
            if (name == "_")
            {
                actualName = $"_pad{_blankFieldIndex++}";
            }
            var fb = new NgoFieldBuilder(_builderType, actualName, type, attrs, declaringTypeBuilder: this);
            _fields.Add(fb);
            return fb;
        }

        public IMethodBuilder DefineMethod(string name, MethodAttributes attrs, Type returnType, Type[] paramTypes)
        {
            var mb = new NgoMethodBuilder(_builderType, name, attrs, returnType, paramTypes, declaringTypeBuilder: this);
            _methods.Add(mb);
            return mb;
        }

        public IMethodBuilder DefineMethod(string name, MethodAttributes attrs)
        {
            var mb = new NgoMethodBuilder(_builderType, name, attrs, null, null, declaringTypeBuilder: this);
            _methods.Add(mb);
            return mb;
        }

        public IConstructorBuilder DefineConstructor(MethodAttributes attrs, CallingConventions callingConvention, Type[] paramTypes)
        {
            _constructor = new NgoConstructorBuilder(attrs, callingConvention, paramTypes, declaringTypeBuilder: this);
            return _constructor;
        }

        public Type[] DefineGenericParameters(string[] names)
        {
            _genericParamNames = names;
            _builderType.SetGenericParamCount(names.Length);
            var result = new Type[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = new NgoGenericParameterType(names[i], i, isMethodGenericParam: false);
            }
            _genericParamTypes = result;
            return result;
        }

        public Type[] GenericParamTypes => _genericParamTypes;

        public void DefineMethodOverride(IMethodBuilder body, MethodInfo declaration)
        {
            var bodyName = ((NgoMethodBuilder)body).Name;
            var declaringType = declaration.DeclaringType!;
            var declType = NgoWriter.GetTypeNameStatic(declaringType);
            var declName = declaration.Name;
            _overrides.Add(new NgoMethodOverride(bodyName, declType, declName, declaringType));
        }

        public MethodInfo DefinePInvokeMethod(string name, string dllName, string entryPoint,
            MethodAttributes attrs, CallingConventions callingConvention,
            Type returnType, Type[] paramTypes,
            System.Runtime.InteropServices.CallingConvention nativeCallConv,
            System.Runtime.InteropServices.CharSet charset)
        {
            throw new NotSupportedException(
                "NgoTypeBuilder.DefinePInvokeMethod is not valid: P/Invoke stubs are only emitted on live modules.");
        }
    }
}

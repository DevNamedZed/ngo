// -----------------------------------------------------------------------
// <copyright file="TypeRef.cs" company="Ziad">
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
using Ngo.Compiler.Emit.Builder;

namespace Ngo.Compiler.Emit.Refs
{
    /// <summary>
    /// A structured reference to a type used during emission.
    /// Replaces System.Type proxies in the emit layer. Plain data — never extends a framework type.
    /// </summary>
    internal sealed class TypeRef
    {
        public TypeRefKind Kind { get; }
        public Type? RuntimeType { get; }
        public string? DefinedFullName { get; }
        public ITypeBuilder? Builder { get; }
        public TypeRef? ElementType { get; }
        public TypeRef? GenericDefinition { get; }
        public TypeRef[] GenericArguments { get; }
        public int GenericParameterIndex { get; }
        public string? PackagePath { get; }
        public string? ExternalTypeName { get; }

        private TypeRef(TypeRefKind kind, Type? runtimeType = null, string? definedFullName = null,
            ITypeBuilder? builder = null,
            TypeRef? elementType = null, TypeRef? genericDefinition = null,
            TypeRef[]? genericArguments = null, int genericParameterIndex = 0,
            string? packagePath = null, string? externalTypeName = null)
        {
            Kind = kind;
            RuntimeType = runtimeType;
            DefinedFullName = definedFullName;
            Builder = builder;
            ElementType = elementType;
            GenericDefinition = genericDefinition;
            GenericArguments = genericArguments ?? System.Array.Empty<TypeRef>();
            GenericParameterIndex = genericParameterIndex;
            PackagePath = packagePath;
            ExternalTypeName = externalTypeName;
        }

        public static TypeRef FromRuntime(Type runtimeType)
        {
            if (runtimeType == null)
            {
                throw new ArgumentNullException(nameof(runtimeType));
            }
            return new TypeRef(TypeRefKind.Runtime, runtimeType: runtimeType);
        }

        public static TypeRef FromDefined(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                throw new ArgumentException("fullName cannot be null or empty", nameof(fullName));
            }
            return new TypeRef(TypeRefKind.Defined, definedFullName: fullName);
        }

        public static TypeRef FromBuilder(ITypeBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            return new TypeRef(TypeRefKind.Builder, builder: builder);
        }

        public static TypeRef Array(TypeRef elementType)
        {
            if (elementType == null)
            {
                throw new ArgumentNullException(nameof(elementType));
            }
            return new TypeRef(TypeRefKind.Array, elementType: elementType);
        }

        public static TypeRef Pointer(TypeRef elementType)
        {
            if (elementType == null)
            {
                throw new ArgumentNullException(nameof(elementType));
            }
            return new TypeRef(TypeRefKind.Pointer, elementType: elementType);
        }

        public static TypeRef ByRef(TypeRef elementType)
        {
            if (elementType == null)
            {
                throw new ArgumentNullException(nameof(elementType));
            }
            return new TypeRef(TypeRefKind.ByRef, elementType: elementType);
        }

        public static TypeRef GenericInstantiation(TypeRef definition, TypeRef[] arguments)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }
            return new TypeRef(TypeRefKind.GenericInstantiation,
                genericDefinition: definition, genericArguments: arguments);
        }

        public static TypeRef GenericTypeParameter(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return new TypeRef(TypeRefKind.GenericTypeParameter, genericParameterIndex: index);
        }

        public static TypeRef GenericMethodParameter(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return new TypeRef(TypeRefKind.GenericMethodParameter, genericParameterIndex: index);
        }

        public static TypeRef ExternalPackage(string packagePath, string typeName)
        {
            if (packagePath == null)
            {
                throw new ArgumentNullException(nameof(packagePath));
            }
            if (string.IsNullOrEmpty(typeName))
            {
                throw new ArgumentException("typeName cannot be null or empty", nameof(typeName));
            }
            return new TypeRef(TypeRefKind.ExternalPackage,
                packagePath: packagePath, externalTypeName: typeName);
        }

        public string DisplayName
        {
            get
            {
                switch (Kind)
                {
                    case TypeRefKind.Runtime:
                    {
                        return RuntimeType!.FullName ?? RuntimeType.Name;
                    }
                    case TypeRefKind.Defined:
                    {
                        return DefinedFullName!;
                    }
                    case TypeRefKind.Builder:
                    {
                        return Builder!.FullName ?? "";
                    }
                    case TypeRefKind.Array:
                    {
                        return ElementType!.DisplayName + "[]";
                    }
                    case TypeRefKind.Pointer:
                    {
                        return ElementType!.DisplayName + "*";
                    }
                    case TypeRefKind.ByRef:
                    {
                        return ElementType!.DisplayName + "&";
                    }
                    case TypeRefKind.GenericInstantiation:
                    {
                        var argNames = new string[GenericArguments.Length];
                        for (int index = 0; index < GenericArguments.Length; index++)
                        {
                            argNames[index] = GenericArguments[index].DisplayName;
                        }
                        return GenericDefinition!.DisplayName + "[" + string.Join(",", argNames) + "]";
                    }
                    case TypeRefKind.GenericTypeParameter:
                    {
                        return "!T" + GenericParameterIndex;
                    }
                    case TypeRefKind.GenericMethodParameter:
                    {
                        return "!!M" + GenericParameterIndex;
                    }
                    case TypeRefKind.ExternalPackage:
                    {
                        return PackagePath + "::" + ExternalTypeName;
                    }
                    default:
                    {
                        return Kind.ToString();
                    }
                }
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}

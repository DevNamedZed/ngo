// -----------------------------------------------------------------------
// <copyright file="TypeToken.cs" company="Ziad">
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
using System.IO;

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// A structured type reference mirroring ECMA-335's TypeDef/TypeRef/TypeSpec distinction.
    /// Replaces flat type name strings in the .ngo archive token table.
    /// </summary>
    internal sealed class TypeToken
    {
        public TypeTokenKind Kind { get; }
        public string TypeName { get; }
        public string PackageImportPath { get; }
        public PrimitiveTypeKind PrimitiveKind { get; }
        public TypeToken? GenericDefinition { get; }
        public TypeToken[] GenericArguments { get; }
        public TypeToken? ElementType { get; }
        public int GenericParamIndex { get; }

        private TypeToken(TypeTokenKind kind, string typeName = "", string packageImportPath = "",
            PrimitiveTypeKind primitiveKind = default, TypeToken? genericDefinition = null,
            TypeToken[]? genericArguments = null, TypeToken? elementType = null,
            int genericParamIndex = 0)
        {
            Kind = kind;
            TypeName = typeName;
            PackageImportPath = packageImportPath;
            PrimitiveKind = primitiveKind;
            GenericDefinition = genericDefinition;
            GenericArguments = genericArguments ?? Array.Empty<TypeToken>();
            ElementType = elementType;
            GenericParamIndex = genericParamIndex;
        }

        public static TypeToken CreateTypeDef(string fullTypeName)
        {
            return new TypeToken(TypeTokenKind.TypeDef, typeName: fullTypeName);
        }

        public static TypeToken CreatePackageTypeRef(string packageImportPath, string typeName)
        {
            return new TypeToken(TypeTokenKind.PackageTypeRef,
                typeName: typeName, packageImportPath: packageImportPath);
        }

        public static TypeToken CreatePrimitive(PrimitiveTypeKind primitiveKind)
        {
            return new TypeToken(TypeTokenKind.Primitive, primitiveKind: primitiveKind);
        }

        public static TypeToken CreateGenericInst(TypeToken genericDefinition, TypeToken[] arguments)
        {
            return new TypeToken(TypeTokenKind.GenericInst,
                genericDefinition: genericDefinition, genericArguments: arguments);
        }

        public static TypeToken CreateArray(TypeToken elementType)
        {
            return new TypeToken(TypeTokenKind.Array, elementType: elementType);
        }

        public static TypeToken CreatePointer(TypeToken elementType)
        {
            return new TypeToken(TypeTokenKind.Pointer, elementType: elementType);
        }

        public static TypeToken CreateByRef(TypeToken elementType)
        {
            return new TypeToken(TypeTokenKind.ByRef, elementType: elementType);
        }

        public static TypeToken CreateGenericMethodParam(int index)
        {
            return new TypeToken(TypeTokenKind.GenericMethodParam, genericParamIndex: index);
        }

        public static TypeToken CreateGenericTypeParam(int index)
        {
            return new TypeToken(TypeTokenKind.GenericTypeParam, genericParamIndex: index);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
            switch (Kind)
            {
                case TypeTokenKind.TypeDef:
                    writer.Write(TypeName);
                    break;
                case TypeTokenKind.PackageTypeRef:
                    writer.Write(PackageImportPath);
                    writer.Write(TypeName);
                    break;
                case TypeTokenKind.Primitive:
                    writer.Write((byte)PrimitiveKind);
                    break;
                case TypeTokenKind.GenericInst:
                    GenericDefinition!.Write(writer);
                    writer.Write(GenericArguments.Length);
                    foreach (var argument in GenericArguments)
                    {
                        argument.Write(writer);
                    }
                    break;
                case TypeTokenKind.Array:
                case TypeTokenKind.Pointer:
                case TypeTokenKind.ByRef:
                    ElementType!.Write(writer);
                    break;
                case TypeTokenKind.GenericMethodParam:
                case TypeTokenKind.GenericTypeParam:
                    writer.Write(GenericParamIndex);
                    break;
            }
        }

        public static TypeToken Read(BinaryReader reader)
        {
            var kind = (TypeTokenKind)reader.ReadByte();
            switch (kind)
            {
                case TypeTokenKind.TypeDef:
                    return new TypeToken(kind, typeName: reader.ReadString());
                case TypeTokenKind.PackageTypeRef:
                {
                    var packagePath = reader.ReadString();
                    var typeName = reader.ReadString();
                    return new TypeToken(kind, typeName: typeName, packageImportPath: packagePath);
                }
                case TypeTokenKind.Primitive:
                    return new TypeToken(kind, primitiveKind: (PrimitiveTypeKind)reader.ReadByte());
                case TypeTokenKind.GenericInst:
                {
                    var genericDef = Read(reader);
                    var argCount = reader.ReadInt32();
                    var arguments = new TypeToken[argCount];
                    for (int i = 0; i < argCount; i++)
                    {
                        arguments[i] = Read(reader);
                    }
                    return new TypeToken(kind, genericDefinition: genericDef, genericArguments: arguments);
                }
                case TypeTokenKind.Array:
                    return new TypeToken(kind, elementType: Read(reader));
                case TypeTokenKind.Pointer:
                    return new TypeToken(kind, elementType: Read(reader));
                case TypeTokenKind.ByRef:
                    return new TypeToken(kind, elementType: Read(reader));
                case TypeTokenKind.GenericMethodParam:
                    return new TypeToken(kind, genericParamIndex: reader.ReadInt32());
                case TypeTokenKind.GenericTypeParam:
                    return new TypeToken(kind, genericParamIndex: reader.ReadInt32());
                default:
                    throw new InvalidOperationException($"Unknown TypeToken kind: {kind}");
            }
        }
    }
}

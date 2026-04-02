// -----------------------------------------------------------------------
// <copyright file="MethodToken.cs" company="Ziad">
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
    /// A structured method reference mirroring ECMA-335's MethodDef/MemberRef/MethodSpec distinction.
    /// The declaring type is a full TypeToken, not a flat string — so constructed generic
    /// declaring types like Ptr&lt;Counter&gt; are represented structurally.
    /// </summary>
    internal sealed class MethodToken
    {
        public MethodTokenKind Kind { get; }
        public TypeToken? DeclaringType { get; }
        public string MethodName { get; }
        public TypeToken[] ParameterTypes { get; }
        public TypeToken? ReturnType { get; }
        public MethodToken? GenericDefinition { get; }
        public TypeToken[] GenericTypeArguments { get; }

        private MethodToken(MethodTokenKind kind, TypeToken? declaringType = null,
            string methodName = "", TypeToken[]? parameterTypes = null, TypeToken? returnType = null,
            MethodToken? genericDefinition = null, TypeToken[]? genericTypeArguments = null)
        {
            Kind = kind;
            DeclaringType = declaringType;
            MethodName = methodName;
            ParameterTypes = parameterTypes ?? Array.Empty<TypeToken>();
            ReturnType = returnType;
            GenericDefinition = genericDefinition;
            GenericTypeArguments = genericTypeArguments ?? Array.Empty<TypeToken>();
        }

        public static MethodToken CreateMethodDef(TypeToken declaringType, string methodName,
            TypeToken[] parameterTypes, TypeToken returnType)
        {
            return new MethodToken(MethodTokenKind.MethodDef,
                declaringType: declaringType, methodName: methodName,
                parameterTypes: parameterTypes, returnType: returnType);
        }

        public static MethodToken CreateMemberRef(TypeToken declaringType,
            string methodName, TypeToken[] parameterTypes, TypeToken returnType)
        {
            return new MethodToken(MethodTokenKind.MemberRef,
                declaringType: declaringType, methodName: methodName,
                parameterTypes: parameterTypes, returnType: returnType);
        }

        public static MethodToken CreateMethodSpec(MethodToken genericDefinition, TypeToken[] typeArguments)
        {
            return new MethodToken(MethodTokenKind.MethodSpec,
                genericDefinition: genericDefinition, genericTypeArguments: typeArguments);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
            switch (Kind)
            {
                case MethodTokenKind.MethodDef:
                    DeclaringType!.Write(writer);
                    writer.Write(MethodName);
                    writer.Write(ParameterTypes.Length);
                    foreach (var paramType in ParameterTypes)
                    {
                        paramType.Write(writer);
                    }
                    ReturnType!.Write(writer);
                    break;
                case MethodTokenKind.MemberRef:
                    DeclaringType!.Write(writer);
                    writer.Write(MethodName);
                    writer.Write(ParameterTypes.Length);
                    foreach (var paramType in ParameterTypes)
                    {
                        paramType.Write(writer);
                    }
                    ReturnType!.Write(writer);
                    break;
                case MethodTokenKind.MethodSpec:
                    GenericDefinition!.Write(writer);
                    writer.Write(GenericTypeArguments.Length);
                    foreach (var typeArg in GenericTypeArguments)
                    {
                        typeArg.Write(writer);
                    }
                    break;
            }
        }

        public static MethodToken Read(BinaryReader reader)
        {
            var kind = (MethodTokenKind)reader.ReadByte();
            switch (kind)
            {
                case MethodTokenKind.MethodDef:
                case MethodTokenKind.MemberRef:
                {
                    var declaringType = TypeToken.Read(reader);
                    var methodName = reader.ReadString();
                    var paramCount = reader.ReadInt32();
                    var paramTypes = new TypeToken[paramCount];
                    for (int i = 0; i < paramCount; i++)
                    {
                        paramTypes[i] = TypeToken.Read(reader);
                    }
                    var returnType = TypeToken.Read(reader);
                    return new MethodToken(kind, declaringType: declaringType,
                        methodName: methodName, parameterTypes: paramTypes, returnType: returnType);
                }
                case MethodTokenKind.MethodSpec:
                {
                    var genericDef = Read(reader);
                    var argCount = reader.ReadInt32();
                    var typeArgs = new TypeToken[argCount];
                    for (int i = 0; i < argCount; i++)
                    {
                        typeArgs[i] = TypeToken.Read(reader);
                    }
                    return new MethodToken(kind, genericDefinition: genericDef,
                        genericTypeArguments: typeArgs);
                }
                default:
                    throw new InvalidOperationException($"Unknown MethodToken kind: {kind}");
            }
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="ILSerializerTypes.cs" company="Ziad">
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

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Links a method definition handle to its serialized body index
    /// during IL serialization (write path).
    /// </summary>
    public sealed class MethodBodyReference
    {
        public MethodBodyReference(MethodDefinitionHandle handle, int bodyIndex)
        {
            Handle = handle;
            BodyIndex = bodyIndex;
        }

        public MethodDefinitionHandle Handle { get; }

        public int BodyIndex { get; }
    }

    /// <summary>
    /// Serialized method metadata read from an .ngo archive, before
    /// the MethodBuilder is created (types may not be resolved yet).
    /// </summary>
    public sealed class SerializedMethodInfo
    {
        public SerializedMethodInfo(string methodName, MethodAttributes attributes,
            string returnTypeName, string[] paramTypeNames, int bodyIndex)
        {
            MethodName = methodName;
            Attributes = attributes;
            ReturnTypeName = returnTypeName;
            ParamTypeNames = paramTypeNames;
            BodyIndex = bodyIndex;
        }

        public string MethodName { get; }

        public MethodAttributes Attributes { get; }

        public string ReturnTypeName { get; }

        public string[] ParamTypeNames { get; }

        public int BodyIndex { get; }
    }

    /// <summary>
    /// A deserialized type definition with its builder and deferred method metadata.
    /// Used during two-pass deserialization: types are created first, then methods.
    /// </summary>
    public sealed class DeserializedTypeInfo
    {
        public DeserializedTypeInfo(string fullTypeName, TypeBuilder typeBuilder,
            int methodCount, List<SerializedMethodInfo> methods,
            List<SerializedMethodOverride> overrides)
        {
            FullTypeName = fullTypeName;
            TypeBuilder = typeBuilder;
            MethodCount = methodCount;
            Methods = methods;
            Overrides = overrides;
        }

        public string FullTypeName { get; }

        public TypeBuilder TypeBuilder { get; }

        public int MethodCount { get; }

        public List<SerializedMethodInfo> Methods { get; }

        public List<SerializedMethodOverride> Overrides { get; }
    }

    /// <summary>
    /// A serialized method override mapping: body method → declaration method on a base/interface type.
    /// </summary>
    public sealed class SerializedMethodOverride
    {
        public SerializedMethodOverride(string bodyMethodName, string declarationTypeName, string declarationMethodName)
        {
            BodyMethodName = bodyMethodName;
            DeclarationTypeName = declarationTypeName;
            DeclarationMethodName = declarationMethodName;
        }

        public string BodyMethodName { get; }

        public string DeclarationTypeName { get; }

        public string DeclarationMethodName { get; }
    }

    /// <summary>
    /// A token reference resolved from IL metadata: kind byte + string reference.
    /// Used during IL body remapping to resolve metadata tokens.
    /// </summary>
    public sealed class TokenReference
    {
        public TokenReference(byte kind, string reference)
        {
            Kind = kind;
            Reference = reference;
        }

        public byte Kind { get; }

        public string Reference { get; }
    }

    /// <summary>
    /// A token entry scanned from IL bytecode during serialization:
    /// offset in IL stream, kind byte, and string reference.
    /// </summary>
    public sealed class TokenEntry
    {
        public TokenEntry(int offset, byte kind, string reference)
        {
            Offset = offset;
            Kind = kind;
            Reference = reference;
        }

        public int Offset { get; }

        public byte Kind { get; }

        public string Reference { get; }
    }
}

// -----------------------------------------------------------------------
// <copyright file="DeserializedTypeInfo.cs" company="Ziad">
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
using System.Reflection.Emit;

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// A deserialized type definition with its builder and deferred method metadata.
    /// Used during two-pass deserialization: types are created first, then methods.
    /// </summary>
    internal sealed class DeserializedTypeInfo
    {
        public DeserializedTypeInfo(string fullTypeName, TypeBuilder typeBuilder,
            int methodCount, List<SerializedMethodInfo> methods,
            InterfaceMethodMapping[] interfaceMappings)
        {
            FullTypeName = fullTypeName;
            TypeBuilder = typeBuilder;
            MethodCount = methodCount;
            Methods = methods;
            InterfaceMappings = interfaceMappings;
        }

        public string FullTypeName { get; }

        public TypeBuilder TypeBuilder { get; }

        public int MethodCount { get; }

        public List<SerializedMethodInfo> Methods { get; }

        public InterfaceMethodMapping[] InterfaceMappings { get; }
    }
}

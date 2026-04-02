// -----------------------------------------------------------------------
// <copyright file="InterfaceMethodMapping.cs" company="Ziad">
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
    /// Records which methods on a type satisfy which interface methods.
    /// Serialized per type in the archive, replaces the old override triples.
    /// At link time, the linker uses this to call DefineMethodOverride for each mapping.
    /// </summary>
    internal sealed class InterfaceMethodMapping
    {
        public string InterfaceTypeName { get; }
        public MethodMapping[] Methods { get; }

        public InterfaceMethodMapping(string interfaceTypeName, MethodMapping[] methods)
        {
            InterfaceTypeName = interfaceTypeName;
            Methods = methods;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(InterfaceTypeName);
            writer.Write(Methods.Length);
            foreach (var mapping in Methods)
            {
                writer.Write(mapping.InterfaceMethodName);
                writer.Write(mapping.BodyMethodName);
            }
        }

        public static InterfaceMethodMapping Read(BinaryReader reader)
        {
            var interfaceTypeName = reader.ReadString();
            var methodCount = reader.ReadInt32();
            var methods = new MethodMapping[methodCount];
            for (int i = 0; i < methodCount; i++)
            {
                var interfaceMethodName = reader.ReadString();
                var bodyMethodName = reader.ReadString();
                methods[i] = new MethodMapping(interfaceMethodName, bodyMethodName);
            }
            return new InterfaceMethodMapping(interfaceTypeName, methods);
        }
    }

}

// -----------------------------------------------------------------------
// <copyright file="NgoModuleBuilder.cs" company="Ziad">
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
using System.IO;
using System.Reflection;

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoModuleBuilder : IModuleBuilder
    {
        private readonly List<NgoTypeBuilder> _types = new();

        public IReadOnlyList<NgoTypeBuilder> Types => _types;

        public ITypeBuilder DefineType(string name, TypeAttributes attrs)
            => DefineType(name, attrs, null, null);

        public ITypeBuilder DefineType(string name, TypeAttributes attrs, Type baseType)
            => DefineType(name, attrs, baseType, null);

        public ITypeBuilder DefineType(string name, TypeAttributes attrs, Type? baseType, Type[]? interfaces)
        {
            var tb = new NgoTypeBuilder(name, attrs, baseType);
            _types.Add(tb);
            return tb;
        }

        /// <summary>
        /// Writes Section 2 (IL metadata) and Section 3 (IL bytecode) to the writer.
        /// Format must match what ILSerializer.LinkIL expects to read.
        /// </summary>
        public void WriteILSections(BinaryWriter writer, BinaryWriter codeWriter)
        {
            // Collect all method bodies across all types (for Section 3 body index mapping)
            var allBodies = new List<NgoWriter>();
            var typeMethodBodies = new Dictionary<NgoTypeBuilder, List<(string methodName, MethodAttributes attrs, string returnType, string[] paramTypes, int bodyIndex)>>();

            foreach (var type in _types)
            {
                var methodEntries = new List<(string methodName, MethodAttributes attrs, string returnType, string[] paramTypes, int bodyIndex)>();

                foreach (var method in type.Methods)
                {
                    int bodyIndex = -1;
                    if (method.Writer != null)
                    {
                        bodyIndex = allBodies.Count;
                        allBodies.Add(method.Writer);
                    }
                    var paramNames = new string[method.ParamTypeNames.Count];
                    for (int i = 0; i < paramNames.Length; i++)
                        paramNames[i] = method.ParamTypeNames[i];
                    methodEntries.Add((method.Name, method.Attributes, method.ReturnTypeName, paramNames, bodyIndex));
                }

                // .cctor
                if (type.Constructor?.Writer != null)
                {
                    int bodyIndex = allBodies.Count;
                    allBodies.Add(type.Constructor.Writer);
                    methodEntries.Add((".cctor",
                        MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                        "System.Void", Array.Empty<string>(), bodyIndex));
                }

                typeMethodBodies[type] = methodEntries;
            }

            // Section 2: IL Metadata
            writer.Write(_types.Count);
            foreach (var type in _types)
            {
                writer.Write(type.FullName ?? "");
                writer.Write((int)type.TypeAttrs);
                writer.Write(type.BaseTypeName);

                // Fields
                writer.Write(type.Fields.Count);
                foreach (var field in type.Fields)
                {
                    writer.Write(field.FieldName);
                    writer.Write((int)field.FieldAttributes);
                    writer.Write(NgoWriter.GetTypeNameStatic(field.FieldType));
                }

                // Methods
                var methods = typeMethodBodies[type];
                writer.Write(methods.Count);
                foreach (var m in methods)
                {
                    writer.Write(m.methodName);
                    writer.Write((int)m.attrs);
                    writer.Write(m.returnType);
                    writer.Write(m.paramTypes.Length);
                    foreach (var pt in m.paramTypes)
                        writer.Write(pt);
                    writer.Write(m.bodyIndex);
                }
            }

            // Section 3: IL Bytecode
            codeWriter.Write(allBodies.Count);
            foreach (var ngoWriter in allBodies)
            {
                ngoWriter.WriteMethodBody(codeWriter);
            }
        }
    }
}

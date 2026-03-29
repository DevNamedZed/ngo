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

        public HashSet<string> ExternalTypeNames { get; } = new();

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
            var packageTypes = new List<NgoTypeBuilder>();
            foreach (var type in _types)
            {
                if (BelongsToPackage(type))
                {
                    packageTypes.Add(type);
                }
            }

            var allBodies = new List<NgoWriter>();
            var typeMethodBodies = new Dictionary<NgoTypeBuilder, List<NgoMethodEntry>>();

            foreach (var type in packageTypes)
            {
                var methodEntries = new List<NgoMethodEntry>();

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
                    {
                        paramNames[i] = method.ParamTypeNames[i];
                    }
                    var genericNames = new string[method.GenericParamNames.Count];
                    for (int i = 0; i < genericNames.Length; i++)
                    {
                        genericNames[i] = method.GenericParamNames[i];
                    }
                    methodEntries.Add(new NgoMethodEntry(method.Name, method.Attributes, method.ReturnTypeName, paramNames, bodyIndex, genericNames));
                }

                if (type.Constructor?.Writer != null)
                {
                    int bodyIndex = allBodies.Count;
                    allBodies.Add(type.Constructor.Writer);
                    bool isStatic = (type.Constructor.Attributes & MethodAttributes.Static) != 0;
                    var ctorName = isStatic ? ".cctor" : ".ctor";
                    var ctorAttrs = type.Constructor.Attributes | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                    var ctorParams = new string[type.Constructor.ParamTypeNames.Count];
                    for (int p = 0; p < ctorParams.Length; p++)
                    {
                        ctorParams[p] = type.Constructor.ParamTypeNames[p];
                    }
                    methodEntries.Add(new NgoMethodEntry(ctorName, ctorAttrs,
                        "System.Void", ctorParams, bodyIndex, Array.Empty<string>()));
                }

                typeMethodBodies[type] = methodEntries;
            }

            // Section 2: IL Metadata
            writer.Write(packageTypes.Count);
            foreach (var type in packageTypes)
            {
                writer.Write(type.FullName ?? "");
                writer.Write((int)type.TypeAttrs);
                writer.Write(type.BaseTypeName);

                // Generic type parameters
                writer.Write(type.GenericParamNames.Count);
                foreach (var gpName in type.GenericParamNames)
                {
                    writer.Write(gpName);
                }

                // Fields
                writer.Write(type.Fields.Count);
                int blankFieldIndex = 0;
                foreach (var field in type.Fields)
                {
                    var fieldName = field.FieldName;
                    if (fieldName == "_")
                    {
                        fieldName = $"_pad{blankFieldIndex++}";
                    }
                    writer.Write(fieldName);
                    writer.Write((int)field.FieldAttributes);
                    writer.Write(NgoWriter.GetTypeNameStatic(field.FieldType));
                }

                // Methods
                var methods = typeMethodBodies[type];
                writer.Write(methods.Count);
                foreach (var m in methods)
                {
                    writer.Write(m.MethodName);
                    writer.Write((int)m.Attributes);
                    writer.Write(m.GenericParamNames.Length);
                    foreach (var gpName in m.GenericParamNames)
                    {
                        writer.Write(gpName);
                    }
                    writer.Write(m.ReturnType);
                    writer.Write(m.ParamTypes.Length);
                    foreach (var pt in m.ParamTypes)
                    {
                        writer.Write(pt);
                    }
                    writer.Write(m.BodyIndex);
                }

                // Method overrides
                writer.Write(type.Overrides.Count);
                foreach (var ov in type.Overrides)
                {
                    writer.Write(ov.BodyMethodName);
                    writer.Write(ov.DeclarationTypeName);
                    writer.Write(ov.DeclarationMethodName);
                }
            }

            // Section 3: IL Bytecode
            codeWriter.Write(allBodies.Count);
            foreach (var ngoWriter in allBodies)
            {
                ngoWriter.WriteMethodBody(codeWriter);
            }
        }

        private bool BelongsToPackage(NgoTypeBuilder type)
        {
            return true;
        }
    }
}

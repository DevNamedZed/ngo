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
using Ngo.Compiler.Archive;
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
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Type name must not be null or empty", nameof(name));
            }
            var tb = new NgoTypeBuilder(name, attrs, baseType, interfaces);
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
                    methodEntries.Add(new NgoMethodEntry(method.Name, method.Attributes, method.ReturnTypeName, paramNames, bodyIndex, genericNames,
                        method.ReturnTypeToken, method.ParamTypeTokens));
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
                        "System.Void", ctorParams, bodyIndex, Array.Empty<string>(),
                        TypeToken.CreatePrimitive(PrimitiveTypeKind.Void), type.Constructor.ParamTypeTokens));
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
                if (!string.IsNullOrEmpty(type.BaseTypeName))
                {
                    type.BaseTypeToken!.Write(writer);
                }

                // Interfaces — names (for keys/diagnostics) then structured tokens (resolution).
                writer.Write(type.InterfaceNames.Count);
                foreach (var ifaceName in type.InterfaceNames)
                {
                    writer.Write(ifaceName);
                }
                foreach (var interfaceToken in type.InterfaceTokens)
                {
                    interfaceToken.Write(writer);
                }

                // Generic type parameters
                writer.Write(type.GenericParamNames.Count);
                foreach (var gpName in type.GenericParamNames)
                {
                    writer.Write(gpName);
                }

                // Fields
                writer.Write(type.Fields.Count);
                foreach (var field in type.Fields)
                {
                    writer.Write(field.FieldName);
                    writer.Write((int)field.FieldAttributes);
                    writer.Write(NgoWriter.GetTypeNameStatic(field.FieldType));
                    writer.Write(field.GoArrayLength);
                    writer.Write(field.GoArrayElementTypeName ?? "");
                    field.FieldTypeToken.Write(writer);
                    if (field.GoArrayLength > 0)
                    {
                        field.GoArrayElementTypeToken!.Write(writer);
                    }
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

                    // Structured signature tokens (index-based generic params) — the source of
                    // truth for resolving the signature at link time. The bare-name strings above
                    // are retained only for the method key and diagnostics. One return token plus
                    // one token per parameter (parameter count already written above).
                    m.ReturnTypeToken.Write(writer);
                    foreach (var paramTypeToken in m.ParamTypeTokens)
                    {
                        paramTypeToken.Write(writer);
                    }
                    writer.Write(m.BodyIndex);
                }

                // Interface method implementations — grouped by interface type
                var interfaceMappings = BuildInterfaceMethodMappings(type);
                writer.Write(interfaceMappings.Count);
                foreach (var mapping in interfaceMappings)
                {
                    mapping.Write(writer);
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
            if (string.IsNullOrEmpty(type.FullName))
            {
                return false;
            }
            if (ExternalTypeNames.Contains(type.FullName))
            {
                return false;
            }
            return true;
        }

        private static List<Archive.InterfaceMethodMapping> BuildInterfaceMethodMappings(NgoTypeBuilder type)
        {
            var mappingsByInterface = new Dictionary<string, List<Archive.MethodMapping>>();
            var interfaceTypesByName = new Dictionary<string, Type>();

            foreach (var ov in type.Overrides)
            {
                if (!mappingsByInterface.TryGetValue(ov.DeclarationTypeName, out var methodList))
                {
                    methodList = new List<Archive.MethodMapping>();
                    mappingsByInterface[ov.DeclarationTypeName] = methodList;
                    interfaceTypesByName[ov.DeclarationTypeName] = ov.DeclarationType;
                }
                methodList.Add(new Archive.MethodMapping(ov.DeclarationMethodName, ov.BodyMethodName));
            }

            var signatureWriter = new NgoWriter(new Archive.SerializationContext(Type.EmptyTypes, type.GenericParamTypes));
            var result = new List<Archive.InterfaceMethodMapping>();
            foreach (var (interfaceName, methods) in mappingsByInterface)
            {
                var interfaceToken = signatureWriter.BuildTypeToken(interfaceTypesByName[interfaceName]);
                result.Add(new Archive.InterfaceMethodMapping(interfaceName, interfaceToken, methods.ToArray()));
            }
            return result;
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="NgoArchive.cs" company="Ziad">
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
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Binary archive format for compiled Go packages.
    /// Contains Go metadata (for type checking) and IL (for linking).
    ///
    /// Format:
    ///   Header: magic(4) + version(2) + goMetaOffset(4) + goMetaLen(4)
    ///           + ilMetaOffset(4) + ilMetaLen(4) + ilCodeOffset(4) + ilCodeLen(4)
    ///   Section 1: Go metadata (PackageSymbol serialized with BinaryWriter)
    ///   Section 2: IL metadata (type/method definitions) — reserved
    ///   Section 3: IL bytecode (raw MSIL + token tables) — reserved
    /// </summary>
    public static class NgoArchive
    {
        private static readonly byte[] Magic = { (byte)'N', (byte)'G', (byte)'O', 0 };
        private const int MagicSize = 4;
        private const int VersionSize = 2;
        private const int SectionEntrySize = 4 + 4; // offset(uint32) + length(uint32)
        private const int SectionCount = 4; // Go metadata, IL metadata, IL bytecode, CGo native lib
        private const int HeaderSize = MagicSize + VersionSize + (SectionEntrySize * SectionCount);
        internal const ushort CurrentVersion = 5;

        /// <summary>
        /// Gets the archive path for a package in the cache directory.
        /// </summary>
        public static string GetArchivePath(string cacheDir, string importPath, string? sourceDir = null)
        {
            var baseName = importPath.Replace('/', '.');
            if (sourceDir != null)
            {
                var version = ExtractModuleVersion(sourceDir);
                if (version != null)
                {
                    baseName += "@" + version;
                }
            }
            return Path.Combine(cacheDir, baseName + ".ngo");
        }

        /// <summary>
        /// Extracts a module version string from a source directory path.
        /// e.g., "~/.ngo/mod/cache/google.golang.org/grpc@v1.69.2/internal" → "v1.69.2"
        /// Returns null for stdlib or project-local paths (no @version).
        /// </summary>
        public static string? ExtractModuleVersion(string sourceDir)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                sourceDir, @"@(v[^\\/]+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Gets the global .ngo package cache directory (~/.ngo/cache/pkg/).
        /// Per the design doc, this is a process-wide cache — not per-project.
        /// This ensures cross-module dependencies are only analyzed once.
        /// </summary>
        public static string GetCacheDir(string projectRoot)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".ngo", "cache", "pkg");
        }

        /// <summary>
        /// Writes a .ngo archive containing Go metadata for a package.
        /// IL sections are written by ILSerializer separately.
        /// </summary>
        public static void Write(string path, PackageSymbol pkg, string importPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            // Write placeholder header — we'll seek back to fill offsets
            var headerPos = stream.Position;
            writer.Write(Magic);
            writer.Write(CurrentVersion);
            // Placeholder offsets (8 uint32s = 4 sections * 2)
            for (int i = 0; i < SectionCount * 2; i++)
            {
                writer.Write((uint)0);
            }

            // Section 1: Go metadata
            var goMetaOffset = (uint)stream.Position;
            WriteGoMetadata(writer, pkg, importPath);
            var goMetaLen = (uint)(stream.Position - goMetaOffset);

            // Section 2: IL metadata (reserved — filled by ILSerializer)
            var ilMetaOffset = (uint)stream.Position;
            uint ilMetaLen = 0;

            // Section 3: IL bytecode (reserved — filled by ILSerializer)
            var ilCodeOffset = (uint)stream.Position;
            uint ilCodeLen = 0;

            // Section 4: CGo native library metadata (empty for non-CGo packages)
            var cgoOffset = (uint)stream.Position;
            uint cgoLen = 0;

            // Seek back and write real header
            stream.Seek(headerPos + MagicSize + VersionSize, SeekOrigin.Begin);
            writer.Write(goMetaOffset);
            writer.Write(goMetaLen);
            writer.Write(ilMetaOffset);
            writer.Write(ilMetaLen);
            writer.Write(ilCodeOffset);
            writer.Write(ilCodeLen);
            writer.Write(cgoOffset);
            writer.Write(cgoLen);
        }

        /// <summary>
        /// Reads only the Go metadata section from a .ngo archive.
        /// Returns a PackageSymbol for type checking, without loading IL.
        /// </summary>
        public static PackageSymbol? ReadGoMetadata(string path,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var savedArchivePath = _currentArchivePath;
            _currentArchivePath = path;
            if (!File.Exists(path))
            {
                _currentArchivePath = savedArchivePath;
                return null;
            }

            // Try v2 (ZIP) format first
            if (NgoArchiveV2.IsV2Archive(path))
            {
                var v2result = NgoArchiveV2.ReadMetadata(path, crossPkgResolver);
                _currentArchivePath = savedArchivePath;
                return v2result;
            }

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(stream);

                // Validate v1 header
                var magic = reader.ReadBytes(4);
                if (magic.Length < 4 || magic[0] != 'N' || magic[1] != 'G' || magic[2] != 'O' || magic[3] != 0)
                    return null;

                var version = reader.ReadUInt16();
                if (version != CurrentVersion)
                {
                    return null;
                }

                var goMetaOffset = reader.ReadUInt32();
                var goMetaLen = reader.ReadUInt32();
                reader.ReadUInt32(); reader.ReadUInt32(); // ilMeta
                reader.ReadUInt32(); reader.ReadUInt32(); // ilCode
                reader.ReadUInt32(); reader.ReadUInt32(); // cgo

                if (goMetaLen == 0)
                    return null;

                stream.Seek(goMetaOffset, SeekOrigin.Begin);
                var result = ReadGoMetadataSection(reader, crossPkgResolver);
                _currentArchivePath = savedArchivePath;
                return result;
            }
            catch
            {
                _currentArchivePath = savedArchivePath;
                return null;
            }
        }

        /// <summary>
        /// Write CGo metadata into an existing .ngo archive's Section 4.
        /// Called after C compilation to store the native library path and probe results.
        /// </summary>
        public static void WriteCgoSection(string archivePath, Cgo.CgoCompilationResult cgoResult)
        {
            if (!File.Exists(archivePath) || cgoResult == null || !cgoResult.Success)
            {
                return;
            }

            // Read current archive, append CGo section, update header
            var data = File.ReadAllBytes(archivePath);
            using var stream = new FileStream(archivePath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            // Copy existing data
            writer.Write(data);

            // Write CGo section at the end
            var cgoOffset = (uint)stream.Position;
            writer.Write(cgoResult.NativeLibraryPath ?? "");
            if (cgoResult.ProbeResult != null)
            {
                writer.Write(cgoResult.ProbeResult.TypeSizes.Count);
                foreach (var kv in cgoResult.ProbeResult.TypeSizes)
                {
                    writer.Write(kv.Key);
                    writer.Write(kv.Value);
                }
            }
            else
            {
                writer.Write(0);
            }
            var cgoLen = (uint)(stream.Position - cgoOffset);

            // Update Section 4 offset/length in header
            stream.Seek(MagicSize + VersionSize + (3 * SectionEntrySize), SeekOrigin.Begin);
            writer.Write(cgoOffset);
            writer.Write(cgoLen);
        }

        /// <summary>
        /// Read CGo native library path from a .ngo archive's Section 4.
        /// </summary>
        public static string? ReadCgoNativeLibraryPath(string archivePath)
        {
            if (!File.Exists(archivePath))
            {
                return null;
            }

            try
            {
                using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(stream);

                var magic = reader.ReadBytes(4);
                if (magic.Length < 4 || magic[0] != 'N' || magic[1] != 'G' || magic[2] != 'O')
                {
                    return null;
                }

                var version = reader.ReadUInt16();
                if (version < 3)
                {
                    return null; // No CGo section in version 2
                }

                // Skip sections 1-3
                for (int i = 0; i < 3; i++)
                {
                    reader.ReadUInt32(); // offset
                    reader.ReadUInt32(); // length
                }

                var cgoOffset = reader.ReadUInt32();
                var cgoLen = reader.ReadUInt32();

                if (cgoLen == 0)
                {
                    return null;
                }

                stream.Seek(cgoOffset, SeekOrigin.Begin);
                return reader.ReadString();
            }
            catch
            {
                return null;
            }
        }

        // ----- Go Metadata Serialization (Section 1) -----

        internal static void WriteGoMetadataPublic(BinaryWriter w, PackageSymbol pkg, string importPath)
            => WriteGoMetadata(w, pkg, importPath);

        private static void WriteGoMetadata(BinaryWriter w, PackageSymbol pkg, string importPath)
        {
            w.Write(pkg.Name);
            w.Write(importPath);

            // Collect exports by kind
            var functions = new List<FunctionSymbol>();
            var structs = new List<StructTypeSymbol>();
            var interfaces = new List<InterfaceTypeSymbol>();
            var namedTypes = new List<TypeSymbol>();
            var constants = new List<ConstantSymbol>();
            var variables = new List<PackageVarSymbol>();

            foreach (var export in pkg.Exports)
            {
                switch (export.Value)
                {
                    case FunctionSymbol func:
                        functions.Add(func);
                        break;
                    case StructTypeSymbol s:
                        structs.Add(s);
                        break;
                    case InterfaceTypeSymbol i:
                        interfaces.Add(i);
                        break;
                    case TypeSymbol t:
                        namedTypes.Add(t);
                        break;
                    case ConstantSymbol c:
                        constants.Add(c);
                        break;
                    case PackageVarSymbol v:
                        variables.Add(v);
                        break;
                }
            }

            // Collect all unexported types reachable from the exported API.
            // Walk the type graph from every export — any type reachable from
            // any export must be in the archive.
            var visited = new HashSet<string>();
            foreach (var s in structs)
            {
                visited.Add(s.Name);
            }
            foreach (var i in interfaces)
            {
                visited.Add(i.Name);
            }
            foreach (var nt in namedTypes)
            {
                visited.Add(nt.Name);
            }

            var reachableStructs = new List<StructTypeSymbol>();
            var reachableInterfaces = new List<InterfaceTypeSymbol>();
            var reachableNamedTypes = new List<TypeSymbol>();

            void WalkType(TypeSymbol type)
            {
                if (type == null)
                {
                    return;
                }

                switch (type)
                {
                    case PointerTypeSymbol ptr:
                        WalkType(ptr.ElementType);
                        return;
                    case SliceTypeSymbol slice:
                        WalkType(slice.ElementType);
                        return;
                    case ArrayTypeSymbol array:
                        WalkType(array.ElementType);
                        return;
                    case MapTypeSymbol map:
                        WalkType(map.KeyType);
                        WalkType(map.ValueType);
                        return;
                    case ChannelTypeSymbol chan:
                        WalkType(chan.ElementType);
                        return;
                    case FunctionTypeSymbol funcType:
                        foreach (var p in funcType.ParameterTypes)
                        {
                            WalkType(p);
                        }
                        foreach (var r in funcType.ReturnTypes)
                        {
                            WalkType(r);
                        }
                        return;
                }

                // Only collect types from this package (no PackagePath or same as importPath)
                if (!string.IsNullOrEmpty(type.PackagePath) && type.PackagePath != importPath)
                {
                    return;
                }

                if (type.Name.Length == 0 || !visited.Add(type.Name))
                {
                    return;
                }

                switch (type)
                {
                    case StructTypeSymbol st:
                        reachableStructs.Add(st);
                        foreach (var field in st.Fields)
                        {
                            WalkType(field.Type);
                        }
                        WalkMethods(st.Methods);
                        break;
                    case InterfaceTypeSymbol iface:
                        reachableInterfaces.Add(iface);
                        WalkMethods(iface.Methods);
                        break;
                    default:
                        reachableNamedTypes.Add(type);
                        if (type.UnderlyingType != null)
                        {
                            WalkType(type.UnderlyingType);
                        }
                        WalkMethods(type.Methods);
                        break;
                }
            }

            void WalkMethods(IReadOnlyList<MethodSymbol> methods)
            {
                foreach (var method in methods)
                {
                    foreach (var p in method.Parameters)
                    {
                        WalkType(p.Type);
                    }
                    foreach (var r in method.ReturnTypes)
                    {
                        WalkType(r);
                    }
                }
            }

            // Walk from all exported symbols
            foreach (var s in structs)
            {
                foreach (var field in s.Fields)
                {
                    WalkType(field.Type);
                }
                WalkMethods(s.Methods);
            }
            foreach (var i in interfaces)
            {
                WalkMethods(i.Methods);
            }
            foreach (var nt in namedTypes)
            {
                if (nt.UnderlyingType != null)
                {
                    WalkType(nt.UnderlyingType);
                }
                WalkMethods(nt.Methods);
            }
            foreach (var f in functions)
            {
                foreach (var p in f.Parameters)
                {
                    WalkType(p.Type);
                }
                foreach (var r in f.ReturnTypes)
                {
                    WalkType(r);
                }
            }
            foreach (var v in variables)
            {
                WalkType(v.Type);
            }

            // Merge reachable unexported types into the main lists
            var allStructs = new List<StructTypeSymbol>(reachableStructs);
            allStructs.AddRange(structs);
            var allInterfaces = new List<InterfaceTypeSymbol>(reachableInterfaces);
            allInterfaces.AddRange(interfaces);
            var allNamedTypes = new List<TypeSymbol>(reachableNamedTypes);
            allNamedTypes.AddRange(namedTypes);

            // TypeNameTable: all type names and kinds, written before any bodies
            int totalTypeNames = allNamedTypes.Count + allInterfaces.Count + allStructs.Count;
            w.Write(totalTypeNames);
            foreach (var nt in allNamedTypes)
            {
                w.Write(nt.Name);
                w.Write((byte)0); // Named
            }
            foreach (var i in allInterfaces)
            {
                w.Write(i.Name);
                w.Write((byte)1); // Interface
            }
            foreach (var s in allStructs)
            {
                w.Write(s.Name);
                w.Write((byte)2); // Struct
            }

            // Functions
            w.Write(functions.Count);
            foreach (var func in functions)
            {
                WriteFunction(w, func, importPath);
            }

            // Named types
            w.Write(allNamedTypes.Count);
            foreach (var t in allNamedTypes)
            {
                WriteNamedType(w, t, importPath);
            }

            // Interfaces
            w.Write(allInterfaces.Count);
            foreach (var i in allInterfaces)
            {
                WriteInterfaceType(w, i, importPath);
            }

            // Structs
            w.Write(allStructs.Count);
            foreach (var s in allStructs)
            {
                WriteStructType(w, s, importPath);
            }

            // Constants
            w.Write(constants.Count);
            foreach (var c in constants)
            {
                WriteConstant(w, c, importPath);
            }

            // Variables
            w.Write(variables.Count);
            foreach (var v in variables)
            {
                WriteVariable(w, v, importPath);
            }

            // Imports
            w.Write(pkg.Imports.Count);
            foreach (var imp in pkg.Imports)
            {
                w.Write(imp);
            }
        }

        private static PackageSymbol ReadGoMetadataSection(BinaryReader r,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            var importPath = r.ReadString();
            var pkg = new PackageSymbol(name, importPath);
            var typeMap = new Dictionary<string, TypeSymbol>();

            // Read TypeNameTable — creates all type objects before any bodies are read.
            // This eliminates forward reference problems: any same-package type string
            // in a body will find the correct object in typeMap.
            int typeNameCount = r.ReadInt32();
            for (int i = 0; i < typeNameCount; i++)
            {
                var typeName = r.ReadString();
                var kind = r.ReadByte();
                if (!typeMap.ContainsKey(typeName))
                {
                    switch (kind)
                    {
                        case 0: // Named
                            typeMap[typeName] = new TypeSymbol(typeName, TypeKind.Struct, null);
                            break;
                        case 1: // Interface
                            typeMap[typeName] = new InterfaceTypeSymbol(typeName, new List<MethodSymbol>());
                            break;
                        case 2: // Struct
                            typeMap[typeName] = new StructTypeSymbol(typeName, new List<FieldSymbol>());
                            break;
                    }
                }
            }

            // Read functions
            int funcCount = r.ReadInt32();
            for (int i = 0; i < funcCount; i++)
            {
                var func = ReadFunction(r, typeMap, name, crossPkgResolver);
                pkg.AddExport(func);
            }

            // Read named types
            int namedCount = r.ReadInt32();
            for (int i = 0; i < namedCount; i++)
            {
                var t = ReadNamedType(r, typeMap, crossPkgResolver);
                t.PackagePath = importPath;
                typeMap[t.Name] = t;
                pkg.AddExport(t);
            }

            // Read interfaces
            int ifaceCount = r.ReadInt32();
            for (int i = 0; i < ifaceCount; i++)
            {
                var iface = ReadInterfaceType(r, typeMap, crossPkgResolver);
                iface.PackagePath = importPath;
                typeMap[iface.Name] = iface;
                pkg.AddExport(iface);
            }

            // Read structs
            int structCount = r.ReadInt32();
            for (int i = 0; i < structCount; i++)
            {
                var s = ReadStructType(r, typeMap, crossPkgResolver);
                s.PackagePath = importPath;
                typeMap[s.Name] = s;
                pkg.AddExport(s);
            }

            // Read constants
            int constCount = r.ReadInt32();
            for (int i = 0; i < constCount; i++)
            {
                var c = ReadConstant(r, typeMap, crossPkgResolver);
                pkg.AddExport(c);
            }

            // Read variables
            int varCount = r.ReadInt32();
            for (int i = 0; i < varCount; i++)
            {
                var v = ReadVariable(r, typeMap, crossPkgResolver);
                pkg.AddExport(v);
            }

            // Read imports
            int importCount = r.ReadInt32();
            var importPaths = new List<string>(importCount);
            for (int i = 0; i < importCount; i++)
            {
                importPaths.Add(r.ReadString());
            }
            pkg.SetImports(importPaths);

            return pkg;
        }

        // ----- Write helpers -----

        private static void WriteFunction(BinaryWriter w, FunctionSymbol func, string? pkgPath = null)
        {
            w.Write(func.Name);
            w.Write(func.IsVariadic);
            w.Write(func.TypeParameters.Count);
            foreach (var tp in func.TypeParameters)
            {
                w.Write(tp.Name);
            }
            w.Write(func.Parameters.Count);
            foreach (var p in func.Parameters)
            {
                w.Write(p.Name);
                w.Write(TypeToString(p.Type, pkgPath));
            }
            w.Write(func.ReturnTypes.Count);
            foreach (var r in func.ReturnTypes)
            {
                w.Write(TypeToString(r, pkgPath));
            }
        }

        private static void WriteMethod(BinaryWriter w, MethodSymbol method, string? pkgPath = null)
        {
            w.Write(method.Name);
            w.Write(method.IsVariadic);
            w.Write(method.Parameters.Count);
            foreach (var p in method.Parameters)
            {
                w.Write(p.Name);
                w.Write(TypeToString(p.Type, pkgPath));
            }
            w.Write(method.ReturnTypes.Count);
            foreach (var r in method.ReturnTypes)
                w.Write(TypeToString(r, pkgPath));
        }

        private static void WriteStructType(BinaryWriter w, StructTypeSymbol s, string? pkgPath = null)
        {
            w.Write(s.Name);
            w.Write(s.Fields.Count);
            foreach (var f in s.Fields)
            {
                w.Write(f.Name);
                w.Write(TypeToString(f.Type, pkgPath));
                w.Write(f.IsEmbedded);
            }
            w.Write(s.Methods.Count);
            foreach (var m in s.Methods)
                WriteMethod(w, m, pkgPath);
        }

        private static void WriteInterfaceType(BinaryWriter w, InterfaceTypeSymbol iface, string? pkgPath = null)
        {
            w.Write(iface.Name);
            w.Write(iface.Methods.Count);
            foreach (var m in iface.Methods)
                WriteMethod(w, m, pkgPath);
        }

        private static void WriteNamedType(BinaryWriter w, TypeSymbol t, string? pkgPath = null)
        {
            w.Write(t.Name);
            w.Write(t.UnderlyingType != null ? TypeToString(t.UnderlyingType, pkgPath) : "");
            w.Write(t.Methods.Count);
            foreach (var m in t.Methods)
                WriteMethod(w, m, pkgPath);
        }

        private static void WriteConstant(BinaryWriter w, ConstantSymbol c, string? pkgPath = null)
        {
            w.Write(c.Name);
            w.Write(TypeToString(c.Type, pkgPath));
            w.Write(c.Value?.ToString() ?? "");
        }

        private static void WriteVariable(BinaryWriter w, PackageVarSymbol v, string? pkgPath = null)
        {
            w.Write(v.Name);
            w.Write(TypeToString(v.Type, pkgPath));
        }

        // ----- Read helpers -----

        private static FunctionSymbol ReadFunction(BinaryReader r, Dictionary<string, TypeSymbol> typeMap,
            string packageName, Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            var isVariadic = r.ReadBoolean();
            int typeParamCount = r.ReadInt32();
            var typeParameters = new List<TypeParameterSymbol>(typeParamCount);
            for (int i = 0; i < typeParamCount; i++)
            {
                var tpName = r.ReadString();
                typeParameters.Add(new TypeParameterSymbol(tpName, i, ConstraintInfo.Any));
            }
            int paramCount = r.ReadInt32();
            var parameters = new List<ParameterSymbol>(paramCount);
            for (int i = 0; i < paramCount; i++)
            {
                var pName = r.ReadString();
                var pType = StringToType(r.ReadString(), typeMap, crossPkgResolver);
                parameters.Add(new ParameterSymbol(pName, pType, i));
            }
            int retCount = r.ReadInt32();
            var returnTypes = new List<TypeSymbol>(retCount);
            for (int i = 0; i < retCount; i++)
            {
                returnTypes.Add(StringToType(r.ReadString(), typeMap, crossPkgResolver));
            }

            return new FunctionSymbol(name, typeParameters, parameters, returnTypes, isVariadic, packageName);
        }

        private static MethodSymbol ReadMethod(BinaryReader r, TypeSymbol receiver, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            var isVariadic = r.ReadBoolean();
            int paramCount = r.ReadInt32();
            var parameters = new List<ParameterSymbol>(paramCount);
            for (int i = 0; i < paramCount; i++)
            {
                var pName = r.ReadString();
                var pType = StringToType(r.ReadString(), typeMap, crossPkgResolver);
                parameters.Add(new ParameterSymbol(pName, pType, i));
            }
            int retCount = r.ReadInt32();
            var returnTypes = new List<TypeSymbol>(retCount);
            for (int i = 0; i < retCount; i++)
                returnTypes.Add(StringToType(r.ReadString(), typeMap, crossPkgResolver));

            return new MethodSymbol(name, receiver, false,
                Array.Empty<TypeParameterSymbol>(), parameters, returnTypes, isVariadic);
        }

        private static StructTypeSymbol ReadStructType(BinaryReader r, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            int fieldCount = r.ReadInt32();
            var fields = new List<FieldSymbol>(fieldCount);
            for (int i = 0; i < fieldCount; i++)
            {
                var fName = r.ReadString();
                var fType = StringToType(r.ReadString(), typeMap, crossPkgResolver);
                var isEmbedded = r.ReadBoolean();
                fields.Add(new FieldSymbol(fName, fType, i, isEmbedded));
            }
            // Reuse existing placeholder if pre-registered (two-pass struct reading)
            StructTypeSymbol structType;
            if (typeMap.TryGetValue(name, out var existing) && existing is StructTypeSymbol existingStruct)
            {
                existingStruct.SetFields(fields);
                structType = existingStruct;
            }
            else
            {
                structType = new StructTypeSymbol(name, fields);
            }
            typeMap[name] = structType; // register before reading methods (methods may reference this type)

            int methodCount = r.ReadInt32();
            for (int i = 0; i < methodCount; i++)
            {
                var method = ReadMethod(r, structType, typeMap, crossPkgResolver);
                structType.AddMethod(method);
            }
            return structType;
        }

        private static InterfaceTypeSymbol ReadInterfaceType(BinaryReader r, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            int methodCount = r.ReadInt32();
            // Reuse pre-registered placeholder so existing references stay valid
            InterfaceTypeSymbol iface;
            if (typeMap.TryGetValue(name, out var existing) && existing is InterfaceTypeSymbol existingIface)
            {
                iface = existingIface;
            }
            else
            {
                iface = new InterfaceTypeSymbol(name, new List<MethodSymbol>());
            }
            typeMap[name] = iface;

            for (int i = 0; i < methodCount; i++)
            {
                var method = ReadMethod(r, iface, typeMap, crossPkgResolver);
                iface.AddMethod(method);
            }
            return iface;
        }

        private static TypeSymbol ReadNamedType(BinaryReader r, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            var underlyingStr = r.ReadString();
            var underlying = string.IsNullOrEmpty(underlyingStr)
                ? BuiltinTypes.EmptyInterface
                : StringToType(underlyingStr, typeMap, crossPkgResolver);
            // Reuse pre-registered placeholder so existing references stay valid
            TypeSymbol namedType;
            if (typeMap.TryGetValue(name, out var existing) && existing.GetType() == typeof(TypeSymbol))
            {
                existing.TypeKind = underlying.TypeKind;
                existing.UnderlyingType = underlying;
                namedType = existing;
            }
            else
            {
                namedType = new TypeSymbol(name, underlying.TypeKind, underlying);
            }
            typeMap[name] = namedType;

            int methodCount = r.ReadInt32();
            for (int i = 0; i < methodCount; i++)
            {
                var method = ReadMethod(r, namedType, typeMap, crossPkgResolver);
                namedType.AddMethod(method);
            }
            return namedType;
        }

        private static ConstantSymbol ReadConstant(BinaryReader r, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            var type = StringToType(r.ReadString(), typeMap, crossPkgResolver);
            var valueStr = r.ReadString();
            object? value = ParseConstValue(valueStr, type);
            return new ConstantSymbol(name, type, value);
        }

        private static PackageVarSymbol ReadVariable(BinaryReader r, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            var type = StringToType(r.ReadString(), typeMap, crossPkgResolver);
            return new PackageVarSymbol(name, type);
        }

        // ----- Type string conversion (delegates to PackageMetadataSerializer) -----

        private static string TypeToString(TypeSymbol type, string? currentPackagePath = null)
            => PackageMetadataSerializer.TypeToString(type, currentPackagePath);

        [System.ThreadStatic] internal static string? _currentArchivePath;

        private static TypeSymbol StringToType(string typeStr, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
            => PackageMetadataSerializer.StringToType(typeStr, typeMap, crossPkgResolver);

        private static object? ParseConstValue(string? value, TypeSymbol type)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (type.TypeKind == TypeKind.String || type.TypeKind == TypeKind.UntypedString)
                return value;
            if (long.TryParse(value, out long l)) return l;
            if (double.TryParse(value, out double d)) return d;
            if (bool.TryParse(value, out bool b)) return b;
            return value;
        }
    }
}

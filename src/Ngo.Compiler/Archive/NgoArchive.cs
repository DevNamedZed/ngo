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
using System.IO.Compression;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// ZIP-based archive format for compiled Go packages.
    ///
    /// Layout:
    ///   go-metadata.bin    — Go-level type info (PackageSymbol) for semantic analysis
    ///   il-metadata.bin    — CLR type/method/field definitions for linking
    ///   il-code.bin        — Raw MSIL bytecode + token tables
    ///   native/            — CGo static libraries + probe.json (optional)
    ///   checksums.txt      — SHA256 of source files for cache invalidation
    /// </summary>
    public static class NgoArchive
    {
        internal const string GoMetadataEntry = "go-metadata.bin";
        internal const string ILMetadataEntry = "il-metadata.bin";
        internal const string ILCodeEntry = "il-code.bin";
        internal const string ChecksumsEntry = "checksums.txt";
        internal const string NativeDir = "native/";
        internal const string ProbeEntry = "native/probe.json";

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
        /// </summary>
        public static string GetCacheDir(string projectRoot)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".ngo", "cache", "pkg");
        }

        /// <summary>
        /// Writes a .ngo ZIP archive containing Go metadata for a package.
        /// IL entries are added by ILSerializer.WriteArchive separately.
        /// </summary>
        public static void Write(string path, PackageSymbol pkg, string importPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

            WriteGoMetadataToZip(zip, pkg, importPath);
        }

        /// <summary>
        /// Writes a complete .ngo ZIP archive with Go metadata + IL.
        /// Called by ILSerializer.WriteArchive.
        /// </summary>
        public static void WriteComplete(string path, PackageSymbol pkg, string importPath,
            byte[] ilMetadata, byte[] ilCode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

            WriteGoMetadataToZip(zip, pkg, importPath);

            var ilMetaEntry = zip.CreateEntry(ILMetadataEntry, CompressionLevel.Fastest);
            using (var entryStream = ilMetaEntry.Open())
            {
                entryStream.Write(ilMetadata, 0, ilMetadata.Length);
            }

            var ilCodeEntry = zip.CreateEntry(ILCodeEntry, CompressionLevel.Fastest);
            using (var entryStream = ilCodeEntry.Open())
            {
                entryStream.Write(ilCode, 0, ilCode.Length);
            }
        }

        /// <summary>
        /// Reads only the Go metadata from a .ngo archive.
        /// Returns a PackageSymbol for type checking, without loading IL.
        /// </summary>
        public static PackageSymbol? ReadGoMetadata(string path,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

                var entry = zip.GetEntry(GoMetadataEntry);
                if (entry == null)
                {
                    return null;
                }

                using var entryStream = entry.Open();
                using var reader = new BinaryReader(entryStream);
                return ReadGoMetadataSection(reader, crossPkgResolver);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads IL metadata and IL code from a .ngo archive.
        /// Returns null if the archive has no IL entries.
        /// </summary>
        public static (byte[]? ilMetadata, byte[]? ilCode) ReadIL(string path)
        {
            if (!File.Exists(path))
            {
                return (null, null);
            }

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

                var metaEntry = zip.GetEntry(ILMetadataEntry);
                var codeEntry = zip.GetEntry(ILCodeEntry);
                if (metaEntry == null || codeEntry == null)
                {
                    return (null, null);
                }

                byte[] ilMeta;
                using (var entryStream = metaEntry.Open())
                using (var memStream = new MemoryStream())
                {
                    entryStream.CopyTo(memStream);
                    ilMeta = memStream.ToArray();
                }

                byte[] ilCode;
                using (var entryStream = codeEntry.Open())
                using (var memStream = new MemoryStream())
                {
                    entryStream.CopyTo(memStream);
                    ilCode = memStream.ToArray();
                }

                return (ilMeta, ilCode);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// Adds CGo native library and probe data to an existing .ngo archive.
        /// </summary>
        public static void WriteCgoData(string path, string nativeLibraryPath,
            Cgo.CgoProbeResult? probeResult)
        {
            if (!File.Exists(path) || !File.Exists(nativeLibraryPath))
            {
                return;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Update);

            // Add the static library
            var libFileName = Path.GetFileName(nativeLibraryPath);
            var libEntry = zip.CreateEntry(NativeDir + libFileName, CompressionLevel.Fastest);
            using (var entryStream = libEntry.Open())
            {
                var libBytes = File.ReadAllBytes(nativeLibraryPath);
                entryStream.Write(libBytes, 0, libBytes.Length);
            }

            // Add probe results as JSON
            if (probeResult != null)
            {
                var probeEntry = zip.CreateEntry(ProbeEntry, CompressionLevel.Fastest);
                using var entryStream = probeEntry.Open();
                using var writer = new StreamWriter(entryStream);
                var probeData = new Dictionary<string, object>
                {
                    ["typeSizes"] = probeResult.TypeSizes,
                    ["enumValues"] = probeResult.EnumValues,
                };
                writer.Write(System.Text.Json.JsonSerializer.Serialize(probeData,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
        }

        /// <summary>
        /// Reads the CGo native library path from a .ngo archive.
        /// Extracts the library to a temp directory if needed.
        /// </summary>
        public static string? ReadCgoNativeLibrary(string archivePath)
        {
            if (!File.Exists(archivePath))
            {
                return null;
            }

            try
            {
                using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.StartsWith(NativeDir) && entry.FullName.EndsWith(".a"))
                    {
                        // Extract to temp directory
                        var tempDir = Path.Combine(Path.GetTempPath(), "ngo", "native",
                            Path.GetFileNameWithoutExtension(archivePath));
                        Directory.CreateDirectory(tempDir);
                        var extractPath = Path.Combine(tempDir, entry.Name);
                        if (!File.Exists(extractPath))
                        {
                            entry.ExtractToFile(extractPath);
                        }
                        return extractPath;
                    }
                }
            }
            catch
            {
                // Archive corrupt or unreadable
            }

            return null;
        }

        // ----- ZIP entry helpers -----

        private static void WriteGoMetadataToZip(ZipArchive zip, PackageSymbol pkg, string importPath)
        {
            var entry = zip.CreateEntry(GoMetadataEntry, CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var writer = new BinaryWriter(entryStream);
            WriteGoMetadata(writer, pkg, importPath);
        }

        internal static void WriteGoMetadataPublic(BinaryWriter w, PackageSymbol pkg, string importPath)
            => WriteGoMetadata(w, pkg, importPath);

        // ----- Go Metadata Serialization -----

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
                        if (t.IsAlias && t.UnderlyingType is StructTypeSymbol aliasedStruct
                            && aliasedStruct.Name.StartsWith("struct"))
                        {
                            var namedStruct = new StructTypeSymbol(t.Name, aliasedStruct.Fields);
                            foreach (var method in aliasedStruct.Methods)
                            {
                                namedStruct.AddMethod(method);
                            }
                            foreach (var method in t.Methods)
                            {
                                if (namedStruct.LookupMethod(method.Name) == null)
                                {
                                    namedStruct.AddMethod(method);
                                }
                            }
                            structs.Add(namedStruct);
                        }
                        else
                        {
                            namedTypes.Add(t);
                        }
                        break;
                    case ConstantSymbol c:
                        constants.Add(c);
                        break;
                    case PackageVarSymbol v:
                        variables.Add(v);
                        break;
                }
            }

            // Collect all unexported types reachable from the exported API
            var visited = new HashSet<string>();
            foreach (var s in structs) { visited.Add(s.Name); }
            foreach (var i in interfaces) { visited.Add(i.Name); }
            foreach (var nt in namedTypes) { visited.Add(nt.Name); }

            var reachableStructs = new List<StructTypeSymbol>();
            var reachableInterfaces = new List<InterfaceTypeSymbol>();
            var reachableNamedTypes = new List<TypeSymbol>();

            void WalkType(TypeSymbol type)
            {
                if (type == null) { return; }

                switch (type)
                {
                    case PointerTypeSymbol ptr: WalkType(ptr.ElementType); return;
                    case SliceTypeSymbol slice: WalkType(slice.ElementType); return;
                    case ArrayTypeSymbol array: WalkType(array.ElementType); return;
                    case MapTypeSymbol map: WalkType(map.KeyType); WalkType(map.ValueType); return;
                    case ChannelTypeSymbol chan: WalkType(chan.ElementType); return;
                    case FunctionTypeSymbol funcType:
                        foreach (var p in funcType.ParameterTypes) { WalkType(p); }
                        foreach (var r in funcType.ReturnTypes) { WalkType(r); }
                        return;
                }

                if (!string.IsNullOrEmpty(type.PackagePath) && type.PackagePath != importPath) { return; }
                if (type.Name.Length == 0 || !visited.Add(type.Name)) { return; }

                switch (type)
                {
                    case StructTypeSymbol st:
                        reachableStructs.Add(st);
                        foreach (var field in st.Fields) { WalkType(field.Type); }
                        WalkMethods(st.Methods);
                        break;
                    case InterfaceTypeSymbol iface:
                        reachableInterfaces.Add(iface);
                        WalkMethods(iface.Methods);
                        break;
                    default:
                        if (type.IsAlias && type.UnderlyingType is StructTypeSymbol anonSt
                            && (anonSt.Name == "struct" || anonSt.Name == "struct{}"))
                        {
                            var promoted = new StructTypeSymbol(type.Name, anonSt.Fields);
                            foreach (var method in anonSt.Methods) { promoted.AddMethod(method); }
                            foreach (var method in type.Methods)
                            {
                                if (promoted.LookupMethod(method.Name) == null) { promoted.AddMethod(method); }
                            }
                            reachableStructs.Add(promoted);
                            foreach (var field in anonSt.Fields) { WalkType(field.Type); }
                            WalkMethods(promoted.Methods);
                        }
                        else
                        {
                            reachableNamedTypes.Add(type);
                            if (type.UnderlyingType != null) { WalkType(type.UnderlyingType); }
                            WalkMethods(type.Methods);
                        }
                        break;
                }
            }

            void WalkMethods(IReadOnlyList<MethodSymbol> methods)
            {
                foreach (var method in methods)
                {
                    foreach (var p in method.Parameters) { WalkType(p.Type); }
                    foreach (var r in method.ReturnTypes) { WalkType(r); }
                }
            }

            // Walk from all exported symbols
            foreach (var s in structs) { foreach (var field in s.Fields) { WalkType(field.Type); } WalkMethods(s.Methods); }
            foreach (var i in interfaces) { WalkMethods(i.Methods); }
            foreach (var nt in namedTypes) { if (nt.UnderlyingType != null) { WalkType(nt.UnderlyingType); } WalkMethods(nt.Methods); }
            foreach (var f in functions) { foreach (var p in f.Parameters) { WalkType(p.Type); } foreach (var r in f.ReturnTypes) { WalkType(r); } }
            foreach (var v in variables) { WalkType(v.Type); }

            // Merge reachable unexported types
            var allStructs = new List<StructTypeSymbol>(reachableStructs);
            allStructs.AddRange(structs);
            var allInterfaces = new List<InterfaceTypeSymbol>(reachableInterfaces);
            allInterfaces.AddRange(interfaces);
            var allNamedTypes = new List<TypeSymbol>(reachableNamedTypes);
            allNamedTypes.AddRange(namedTypes);

            // TypeNameTable
            int totalTypeNames = allNamedTypes.Count + allInterfaces.Count + allStructs.Count;
            w.Write(totalTypeNames);
            foreach (var nt in allNamedTypes) { w.Write(nt.Name); w.Write((byte)0); }
            foreach (var i in allInterfaces) { w.Write(i.Name); w.Write((byte)1); }
            foreach (var s in allStructs) { w.Write(s.Name); w.Write((byte)2); }

            // Functions
            w.Write(functions.Count);
            foreach (var func in functions) { WriteFunction(w, func, importPath); }

            // Named types
            w.Write(allNamedTypes.Count);
            foreach (var t in allNamedTypes) { WriteNamedType(w, t, importPath); }

            // Interfaces
            w.Write(allInterfaces.Count);
            foreach (var i in allInterfaces) { WriteInterfaceType(w, i, importPath); }

            // Structs
            w.Write(allStructs.Count);
            foreach (var s in allStructs) { WriteStructType(w, s, importPath); }

            // Constants
            w.Write(constants.Count);
            foreach (var c in constants) { WriteConstant(w, c, importPath); }

            // Variables
            w.Write(variables.Count);
            foreach (var v in variables) { WriteVariable(w, v, importPath); }

            // Imports
            w.Write(pkg.Imports.Count);
            foreach (var imp in pkg.Imports) { w.Write(imp); }
        }

        private static PackageSymbol ReadGoMetadataSection(BinaryReader r,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            var importPath = r.ReadString();
            var pkg = new PackageSymbol(name, importPath);
            var typeMap = new Dictionary<string, TypeSymbol>();

            // TypeNameTable
            int typeNameCount = r.ReadInt32();
            for (int i = 0; i < typeNameCount; i++)
            {
                var typeName = r.ReadString();
                var kind = r.ReadByte();
                if (!typeMap.ContainsKey(typeName))
                {
                    switch (kind)
                    {
                        case 0: typeMap[typeName] = new TypeSymbol(typeName, TypeKind.Struct, null); break;
                        case 1: typeMap[typeName] = new InterfaceTypeSymbol(typeName, new List<MethodSymbol>()); break;
                        case 2: typeMap[typeName] = new StructTypeSymbol(typeName, new List<FieldSymbol>()); break;
                    }
                }
            }

            // Functions
            int funcCount = r.ReadInt32();
            for (int i = 0; i < funcCount; i++)
            {
                var func = ReadFunction(r, typeMap, name, crossPkgResolver);
                pkg.AddExport(func);
            }

            // Named types
            int namedCount = r.ReadInt32();
            for (int i = 0; i < namedCount; i++)
            {
                var t = ReadNamedType(r, typeMap, crossPkgResolver);
                t.PackagePath = importPath;
                typeMap[t.Name] = t;
                pkg.AddExport(t);
            }

            // Interfaces
            int ifaceCount = r.ReadInt32();
            for (int i = 0; i < ifaceCount; i++)
            {
                var iface = ReadInterfaceType(r, typeMap, crossPkgResolver);
                iface.PackagePath = importPath;
                typeMap[iface.Name] = iface;
                pkg.AddExport(iface);
            }

            // Structs
            int structCount = r.ReadInt32();
            for (int i = 0; i < structCount; i++)
            {
                var s = ReadStructType(r, typeMap, crossPkgResolver);
                s.PackagePath = importPath;
                typeMap[s.Name] = s;
                pkg.AddExport(s);
            }

            // Constants
            int constCount = r.ReadInt32();
            for (int i = 0; i < constCount; i++)
            {
                var c = ReadConstant(r, typeMap, crossPkgResolver);
                pkg.AddExport(c);
            }

            // Variables
            int varCount = r.ReadInt32();
            for (int i = 0; i < varCount; i++)
            {
                var v = ReadVariable(r, typeMap, crossPkgResolver);
                pkg.AddExport(v);
            }

            // Imports
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
            foreach (var tp in func.TypeParameters) { w.Write(tp.Name); }
            w.Write(func.Parameters.Count);
            foreach (var p in func.Parameters) { w.Write(p.Name); w.Write(TypeToString(p.Type, pkgPath)); }
            w.Write(func.ReturnTypes.Count);
            foreach (var r in func.ReturnTypes) { w.Write(TypeToString(r, pkgPath)); }
        }

        private static void WriteMethod(BinaryWriter w, MethodSymbol method, string? pkgPath = null)
        {
            w.Write(method.Name);
            w.Write(method.IsVariadic);
            w.Write(method.Parameters.Count);
            foreach (var p in method.Parameters) { w.Write(p.Name); w.Write(TypeToString(p.Type, pkgPath)); }
            w.Write(method.ReturnTypes.Count);
            foreach (var r in method.ReturnTypes) { w.Write(TypeToString(r, pkgPath)); }
        }

        private static void WriteStructType(BinaryWriter w, StructTypeSymbol s, string? pkgPath = null)
        {
            w.Write(s.Name);
            var typeParams = s.IsGeneric ? s.TypeParameters : Array.Empty<TypeParameterSymbol>();
            w.Write(typeParams.Count);
            foreach (var tp in typeParams) { w.Write(tp.Name); }
            w.Write(s.Fields.Count);
            foreach (var f in s.Fields) { w.Write(f.Name); w.Write(TypeToString(f.Type, pkgPath)); w.Write(f.IsEmbedded); }
            w.Write(s.Methods.Count);
            foreach (var m in s.Methods) { WriteMethod(w, m, pkgPath); }
        }

        private static void WriteInterfaceType(BinaryWriter w, InterfaceTypeSymbol iface, string? pkgPath = null)
        {
            w.Write(iface.Name);
            var ifaceTypeParams = iface.IsGeneric ? iface.TypeParameters : Array.Empty<TypeParameterSymbol>();
            w.Write(ifaceTypeParams.Count);
            foreach (var tp in ifaceTypeParams) { w.Write(tp.Name); }
            w.Write(iface.Methods.Count);
            foreach (var m in iface.Methods) { WriteMethod(w, m, pkgPath); }
        }

        private static void WriteNamedType(BinaryWriter w, TypeSymbol t, string? pkgPath = null)
        {
            w.Write(t.Name);
            w.Write(t.UnderlyingType != null ? TypeToString(t.UnderlyingType, pkgPath) : "");
            w.Write(t.Methods.Count);
            foreach (var m in t.Methods) { WriteMethod(w, m, pkgPath); }
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
                typeParameters.Add(new TypeParameterSymbol(r.ReadString(), i, ConstraintInfo.Any));
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
            {
                returnTypes.Add(StringToType(r.ReadString(), typeMap, crossPkgResolver));
            }

            return new MethodSymbol(name, receiver, false,
                Array.Empty<TypeParameterSymbol>(), parameters, returnTypes, isVariadic);
        }

        private static StructTypeSymbol ReadStructType(BinaryReader r, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            int typeParamCount = r.ReadInt32();
            var structTypeParams = new List<TypeParameterSymbol>(typeParamCount);
            for (int i = 0; i < typeParamCount; i++)
            {
                structTypeParams.Add(new TypeParameterSymbol(r.ReadString(), i, ConstraintInfo.Any));
            }
            int fieldCount = r.ReadInt32();
            var fields = new List<FieldSymbol>(fieldCount);
            for (int i = 0; i < fieldCount; i++)
            {
                var fName = r.ReadString();
                var fType = StringToType(r.ReadString(), typeMap, crossPkgResolver);
                var isEmbedded = r.ReadBoolean();
                fields.Add(new FieldSymbol(fName, fType, i, isEmbedded));
            }

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
            if (structTypeParams.Count > 0)
            {
                structType.SetTypeParameters(structTypeParams);
            }
            typeMap[name] = structType;

            int methodCount = r.ReadInt32();
            for (int i = 0; i < methodCount; i++)
            {
                structType.AddMethod(ReadMethod(r, structType, typeMap, crossPkgResolver));
            }
            return structType;
        }

        private static InterfaceTypeSymbol ReadInterfaceType(BinaryReader r, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            var name = r.ReadString();
            int ifaceTypeParamCount = r.ReadInt32();
            var ifaceTypeParams = new List<TypeParameterSymbol>(ifaceTypeParamCount);
            for (int i = 0; i < ifaceTypeParamCount; i++)
            {
                ifaceTypeParams.Add(new TypeParameterSymbol(r.ReadString(), i, ConstraintInfo.Any));
            }
            int methodCount = r.ReadInt32();

            InterfaceTypeSymbol iface;
            if (typeMap.TryGetValue(name, out var existing) && existing is InterfaceTypeSymbol existingIface)
            {
                iface = existingIface;
            }
            else
            {
                iface = new InterfaceTypeSymbol(name, new List<MethodSymbol>());
            }
            if (ifaceTypeParams.Count > 0)
            {
                iface.SetTypeParameters(ifaceTypeParams);
            }
            typeMap[name] = iface;

            for (int i = 0; i < methodCount; i++)
            {
                iface.AddMethod(ReadMethod(r, iface, typeMap, crossPkgResolver));
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
                namedType.AddMethod(ReadMethod(r, namedType, typeMap, crossPkgResolver));
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

        // ----- Type string conversion -----

        private static string TypeToString(TypeSymbol type, string? currentPackagePath = null)
            => PackageMetadataSerializer.TypeToString(type, currentPackagePath);

        private static TypeSymbol StringToType(string typeStr, Dictionary<string, TypeSymbol> typeMap,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
            => PackageMetadataSerializer.StringToType(typeStr, typeMap, crossPkgResolver);

        private static object? ParseConstValue(string? value, TypeSymbol type)
        {
            if (string.IsNullOrEmpty(value)) { return null; }
            if (type.TypeKind == TypeKind.String || type.TypeKind == TypeKind.UntypedString) { return value; }
            if (long.TryParse(value, out long l)) { return l; }
            if (double.TryParse(value, out double d)) { return d; }
            if (bool.TryParse(value, out bool b)) { return b; }
            return value;
        }
    }
}

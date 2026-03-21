using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// .ngo v2 archive format — ZIP-based package archives.
    ///
    /// Contents:
    ///   metadata.json     — Package symbol info, imports, exports, type definitions
    ///   il/meta.bin       — IL type/method definitions (from NgoModuleBuilder)
    ///   il/code.bin       — IL bytecode
    ///   native/*.a        — CGo static libraries (optional)
    ///   source/checksums  — Source file checksums for invalidation
    ///
    /// ZIP format allows tools to inspect/extract with standard tools.
    /// Type metadata enables proper CLR type reconstruction during linking.
    /// </summary>
    public static class NgoArchiveV2
    {
        private const string MetadataEntry = "metadata.json";
        private const string ILMetaEntry = "il/meta.bin";
        private const string ILCodeEntry = "il/code.bin";

        /// <summary>
        /// Check if a file is a v2 (ZIP) archive by magic bytes.
        /// ZIP files start with PK (0x50 0x4B).
        /// </summary>
        public static bool IsV2Archive(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                if (fs.Length < 4) return false;
                var b1 = fs.ReadByte();
                var b2 = fs.ReadByte();
                return b1 == 0x50 && b2 == 0x4B; // PK
            }
            catch { return false; }
        }

        /// <summary>
        /// Write a v2 .ngo archive as a ZIP file.
        /// </summary>
        public static void Write(string path, PackageSymbol pkg, string importPath,
            byte[]? ilMeta = null, byte[]? ilCode = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

            // Write metadata.json
            var metadata = SerializeMetadata(pkg, importPath);
            var metaEntry = zip.CreateEntry(MetadataEntry, CompressionLevel.Fastest);
            using (var metaStream = metaEntry.Open())
            {
                var jsonBytes = System.Text.Encoding.UTF8.GetBytes(metadata);
                metaStream.Write(jsonBytes, 0, jsonBytes.Length);
            }

            // Write IL if available
            if (ilMeta != null && ilMeta.Length > 0)
            {
                var ilMetaEntry = zip.CreateEntry(ILMetaEntry, CompressionLevel.Fastest);
                using var s = ilMetaEntry.Open();
                s.Write(ilMeta, 0, ilMeta.Length);
            }

            if (ilCode != null && ilCode.Length > 0)
            {
                var ilCodeEntry = zip.CreateEntry(ILCodeEntry, CompressionLevel.Fastest);
                using var s = ilCodeEntry.Open();
                s.Write(ilCode, 0, ilCode.Length);
            }
        }

        /// <summary>
        /// Read package metadata from a v2 .ngo archive.
        /// </summary>
        public static PackageSymbol? ReadMetadata(string path,
            Func<string, string, TypeSymbol?>? crossPkgResolver = null)
        {
            if (!File.Exists(path) || !IsV2Archive(path)) return null;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read);

                var metaEntry = zip.GetEntry(MetadataEntry);
                if (metaEntry == null) return null;

                using var metaStream = metaEntry.Open();
                using var reader = new StreamReader(metaStream);
                var json = reader.ReadToEnd();

                return DeserializeMetadata(json, crossPkgResolver);
            }
            catch { return null; }
        }

        /// <summary>
        /// Read IL data from a v2 .ngo archive.
        /// Returns (ilMeta, ilCode) byte arrays.
        /// </summary>
        public static (byte[]? ilMeta, byte[]? ilCode) ReadIL(string path)
        {
            if (!File.Exists(path) || !IsV2Archive(path)) return (null, null);

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read);

                byte[]? ilMeta = null, ilCode = null;

                var metaEntry = zip.GetEntry(ILMetaEntry);
                if (metaEntry != null)
                {
                    using var s = metaEntry.Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    ilMeta = ms.ToArray();
                }

                var codeEntry = zip.GetEntry(ILCodeEntry);
                if (codeEntry != null)
                {
                    using var s = codeEntry.Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    ilCode = ms.ToArray();
                }

                return (ilMeta, ilCode);
            }
            catch { return (null, null); }
        }

        // ---- Serialization ----

        private static string SerializeMetadata(PackageSymbol pkg, string importPath)
        {
            var meta = new PackageMeta
            {
                Name = pkg.Name,
                ImportPath = importPath,
                Imports = new List<string>(),
                Exports = new List<ExportMeta>(),
            };

            foreach (var imp in pkg.Imports)
                meta.Imports.Add(imp);

            foreach (var export in pkg.Exports)
            {
                var em = new ExportMeta { Name = export.Key, Kind = export.Value.Kind.ToString() };

                if (export.Value is FunctionSymbol func)
                {
                    em.Params = new List<string>();
                    foreach (var p in func.Parameters)
                        em.Params.Add(PackageMetadataSerializer.TypeToString(p.Type));
                    em.Returns = new List<string>();
                    foreach (var r in func.ReturnTypes)
                        em.Returns.Add(PackageMetadataSerializer.TypeToString(r));
                }
                else if (export.Value is TypeSymbol type)
                {
                    em.TypeKind = type.TypeKind.ToString();
                    if (type is StructTypeSymbol st)
                    {
                        em.Fields = new List<FieldMeta>();
                        foreach (var f in st.Fields)
                            em.Fields.Add(new FieldMeta
                            {
                                Name = f.Name,
                                Type = PackageMetadataSerializer.TypeToString(f.Type),
                                Embedded = f.IsEmbedded,
                            });
                    }
                    else if (type is InterfaceTypeSymbol iface)
                    {
                        em.Methods = new List<MethodMeta>();
                        foreach (var m in iface.Methods)
                            em.Methods.Add(new MethodMeta { Name = m.Name });
                    }
                }
                else if (export.Value is ConstantSymbol constant)
                {
                    em.Value = constant.Value?.ToString();
                }

                meta.Exports.Add(em);
            }

            return JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        }

        private static PackageSymbol? DeserializeMetadata(string json,
            Func<string, string, TypeSymbol?>? crossPkgResolver)
        {
            var meta = JsonSerializer.Deserialize<PackageMeta>(json);
            if (meta == null) return null;

            var pkg = new PackageSymbol(meta.Name, meta.ImportPath);
            var typeMap = new Dictionary<string, TypeSymbol>();

            // First pass: create type shells
            foreach (var export in meta.Exports)
            {
                if (export.Kind == "Type" && export.TypeKind != null)
                {
                    TypeSymbol type;
                    if (export.TypeKind == "Struct" && export.Fields != null)
                    {
                        var fields = new List<FieldSymbol>();
                        for (int i = 0; i < export.Fields.Count; i++)
                        {
                            var f = export.Fields[i];
                            var fieldType = PackageMetadataSerializer.StringToType(f.Type, typeMap, crossPkgResolver);
                            fields.Add(new FieldSymbol(f.Name, fieldType, i, f.Embedded));
                        }
                        type = new StructTypeSymbol(export.Name, fields);
                    }
                    else if (export.TypeKind == "Interface" && export.Methods != null)
                    {
                        var methods = new List<MethodSymbol>();
                        foreach (var m in export.Methods)
                            methods.Add(new MethodSymbol(m.Name, null!, false,
                                Array.Empty<ParameterSymbol>(), Array.Empty<TypeSymbol>()));
                        type = new InterfaceTypeSymbol(export.Name, methods);
                    }
                    else
                    {
                        var kind = Enum.TryParse<TypeKind>(export.TypeKind, out var k) ? k : TypeKind.Struct;
                        type = new TypeSymbol(export.Name, kind, null);
                    }
                    type.PackagePath = meta.ImportPath;
                    typeMap[export.Name] = type;
                    pkg.AddExport(type);
                }
            }

            // Second pass: functions and constants
            foreach (var export in meta.Exports)
            {
                if (export.Kind == "Function")
                {
                    var parameters = new List<ParameterSymbol>();
                    if (export.Params != null)
                    {
                        for (int i = 0; i < export.Params.Count; i++)
                        {
                            var paramType = PackageMetadataSerializer.StringToType(export.Params[i], typeMap, crossPkgResolver);
                            parameters.Add(new ParameterSymbol($"p{i}", paramType, i));
                        }
                    }
                    var returnTypes = new List<TypeSymbol>();
                    if (export.Returns != null)
                    {
                        foreach (var r in export.Returns)
                            returnTypes.Add(PackageMetadataSerializer.StringToType(r, typeMap, crossPkgResolver));
                    }
                    pkg.AddExport(new FunctionSymbol(export.Name, parameters, returnTypes));
                }
                else if (export.Kind == "Constant")
                {
                    if (long.TryParse(export.Value, out var lv))
                        pkg.AddExport(new ConstantSymbol(export.Name, BuiltinTypes.Int, lv));
                    else
                        pkg.AddExport(new ConstantSymbol(export.Name, BuiltinTypes.String, export.Value));
                }
            }

            if (meta.Imports != null)
                pkg.SetImports(meta.Imports);

            return pkg;
        }

        // ---- Data models for JSON serialization ----

        private class PackageMeta
        {
            public string Name { get; set; } = "";
            public string ImportPath { get; set; } = "";
            public List<string> Imports { get; set; } = new();
            public List<ExportMeta> Exports { get; set; } = new();
        }

        private class ExportMeta
        {
            public string Name { get; set; } = "";
            public string Kind { get; set; } = "";
            public string? TypeKind { get; set; }
            public List<FieldMeta>? Fields { get; set; }
            public List<MethodMeta>? Methods { get; set; }
            public List<string>? Params { get; set; }
            public List<string>? Returns { get; set; }
            public string? Value { get; set; }
        }

        private class FieldMeta
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public bool Embedded { get; set; }
        }

        private class MethodMeta
        {
            public string Name { get; set; } = "";
        }
    }
}

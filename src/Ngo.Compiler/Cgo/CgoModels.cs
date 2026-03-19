using System.Collections.Generic;

namespace Ngo.Compiler.Cgo
{
    public class NetTypeMapping
    {
        public string CSharpType { get; }
        public string ClrType { get; }
        public bool IsPointer { get; }
        public bool IsStruct { get; }

        public NetTypeMapping(string csharpType, string clrType, bool isPointer = false, bool isStruct = false)
        {
            CSharpType = csharpType;
            ClrType = clrType;
            IsPointer = isPointer;
            IsStruct = isStruct;
        }
    }

    public class PInvokeStub
    {
        public string FunctionName { get; }
        public string LibraryName { get; }
        public NetTypeMapping ReturnType { get; }
        public IReadOnlyList<PInvokeParameter> Parameters { get; }
        public bool IsVariadic { get; }

        public PInvokeStub(string functionName, string libraryName, NetTypeMapping returnType,
            List<PInvokeParameter> parameters, bool isVariadic)
        {
            FunctionName = functionName;
            LibraryName = libraryName;
            ReturnType = returnType;
            Parameters = parameters;
            IsVariadic = isVariadic;
        }
    }

    public class PInvokeParameter
    {
        public string Name { get; }
        public NetTypeMapping Type { get; }

        public PInvokeParameter(string name, NetTypeMapping type)
        {
            Name = name;
            Type = type;
        }
    }

    public class StructLayout
    {
        public string NetTypeName { get; }
        public IReadOnlyList<StructFieldLayout> Fields { get; }
        public long TotalSize { get; }
        public long Alignment { get; }
        public bool IsUnion { get; }

        public StructLayout(string netTypeName, List<StructFieldLayout> fields,
            long totalSize, long alignment, bool isUnion)
        {
            NetTypeName = netTypeName;
            Fields = fields;
            TotalSize = totalSize;
            Alignment = alignment;
            IsUnion = isUnion;
        }
    }

    public class StructFieldLayout
    {
        public string Name { get; }
        public NetTypeMapping Type { get; }
        public long Offset { get; }
        public long Size { get; }

        public StructFieldLayout(string name, NetTypeMapping type, long offset, long size)
        {
            Name = name;
            Type = type;
            Offset = offset;
            Size = size;
        }
    }

    public class CgoFunctionInfo
    {
        public string Name { get; set; } = "";
        public string ReturnType { get; set; } = "void";
        public List<CgoParameterInfo> Parameters { get; set; } = new();
        public bool IsVariadic { get; set; }
    }

    public class CgoParameterInfo
    {
        public string Name { get; set; } = "";
        public string CType { get; set; } = "";
    }

    public class CgoStructInfo
    {
        public string CName { get; set; } = "";
        public string GoName { get; set; } = "";
        public List<CgoFieldInfo> Fields { get; set; } = new();
        public bool IsUnion { get; set; }
    }

    public class CgoFieldInfo
    {
        public string Name { get; set; } = "";
        public string CType { get; set; } = "";
    }

    public class CgoProbeRequest
    {
        public List<string> TypeSizes { get; } = new();
        public List<string> TypeAlignments { get; } = new();
        public List<CgoFieldProbe> FieldOffsets { get; } = new();
        public List<CgoFieldProbe> FieldSizes { get; } = new();
        public List<string> EnumValues { get; } = new();
        public List<CgoFunctionProbe> FunctionProbes { get; } = new();
    }

    public class CgoFieldProbe
    {
        public string StructName { get; }
        public string FieldName { get; }

        public CgoFieldProbe(string structName, string fieldName)
        {
            StructName = structName;
            FieldName = fieldName;
        }
    }

    public class CgoFunctionProbe
    {
        public string Name { get; }

        public CgoFunctionProbe(string name)
        {
            Name = name;
        }
    }

    public class CgoPreamble
    {
        public string CSource { get; }
        public IReadOnlyList<CgoDirective> Directives { get; }
        public string SourceDirectory { get; }

        public CgoPreamble(string cSource, List<CgoDirective> directives, string sourceDirectory)
        {
            CSource = cSource;
            Directives = directives;
            SourceDirectory = sourceDirectory;
        }

        public bool HasCSource => !string.IsNullOrWhiteSpace(CSource);

        public string GetCFlags(string currentOS)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var directive in Directives)
            {
                if (directive.Kind == "CFLAGS" && directive.MatchesOS(currentOS))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(directive.ExpandedValue(SourceDirectory));
                }
            }
            return sb.ToString();
        }

        public string GetLDFlags(string currentOS)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var directive in Directives)
            {
                if (directive.Kind == "LDFLAGS" && directive.MatchesOS(currentOS))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    sb.Append(directive.ExpandedValue(SourceDirectory));
                }
            }
            return sb.ToString();
        }

        public List<string> GetPkgConfigPackages(string currentOS)
        {
            var packages = new List<string>();
            foreach (var directive in Directives)
            {
                if (directive.Kind == "pkg-config" && directive.MatchesOS(currentOS))
                {
                    foreach (var pkg in directive.Value.Split(' '))
                    {
                        string trimmed = pkg.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            packages.Add(trimmed);
                        }
                    }
                }
            }
            return packages;
        }
    }

    public class CgoCompilationResult
    {
        public CgoProbeResult? ProbeResult { get; set; }
        /// <summary>
        /// Path to the compiled static library (.a/.lib) for this package.
        /// </summary>
        public string? NativeLibraryPath { get; set; }
        public CCompilerInfo? CompilerInfo { get; set; }
        public string? Error { get; set; }
        public bool CacheHit { get; set; }
        /// <summary>
        /// LDFLAGS from #cgo directives, needed at final link time.
        /// </summary>
        public string? LDFlags { get; set; }
        public bool Success => Error == null;
    }

    public class CCompilerInfo
    {
        public string Path { get; }
        public CCompilerKind Kind { get; }
        public string Version { get; }

        public CCompilerInfo(string path, CCompilerKind kind, string version)
        {
            Path = path;
            Kind = kind;
            Version = version;
        }

        public override string ToString() => $"{Kind} ({Path}) {Version}";
    }

    public enum CCompilerKind
    {
        GCC,
        Clang,
        MSVC,
    }

    public class CgoProbeResult
    {
        public Dictionary<string, long> TypeSizes { get; } = new();
        public Dictionary<string, long> TypeAlignments { get; } = new();
        public Dictionary<string, long> FieldOffsets { get; } = new();
        public Dictionary<string, long> FieldSizes { get; } = new();
        public Dictionary<string, long> EnumValues { get; } = new();

        public long GetTypeSize(string sanitizedName)
        {
            return TypeSizes.TryGetValue(sanitizedName, out var size) ? size : -1;
        }

        public long GetTypeAlignment(string sanitizedName)
        {
            return TypeAlignments.TryGetValue(sanitizedName, out var align) ? align : -1;
        }

        public long GetFieldOffset(string structName, string fieldName)
        {
            string key = $"{structName}_{fieldName}";
            return FieldOffsets.TryGetValue(key, out var offset) ? offset : -1;
        }

        public long GetFieldSize(string structName, string fieldName)
        {
            string key = $"{structName}_{fieldName}";
            return FieldSizes.TryGetValue(key, out var size) ? size : -1;
        }

        public long? GetEnumValue(string name)
        {
            return EnumValues.TryGetValue(name, out var value) ? value : null;
        }
    }
}

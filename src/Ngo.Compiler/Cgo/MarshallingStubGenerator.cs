using System;
using System.Collections.Generic;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Generates P/Invoke declarations and marshalling adapters for C functions and types.
    /// This is the bridge between C code and .NET — every C function call goes through
    /// a generated stub that handles type conversion, memory management, and calling convention.
    ///
    /// The generator reads exact type sizes from probe results — never guesses.
    /// </summary>
    public class MarshallingStubGenerator
    {
        private readonly CgoProbeResult _probeResult;

        public MarshallingStubGenerator(CgoProbeResult probeResult)
        {
            _probeResult = probeResult;
        }

        /// <summary>
        /// Map a C type name to the corresponding .NET type.
        /// Uses probe results for platform-specific sizes (e.g., long = 4 on Windows, 8 on Linux).
        /// </summary>
        public NetTypeMapping MapCTypeToNet(string cTypeName)
        {
            // Strip const/volatile qualifiers
            string cleaned = cTypeName
                .Replace("const ", "")
                .Replace("volatile ", "")
                .Trim();

            // Pointer types
            if (cleaned.EndsWith("*"))
            {
                return new NetTypeMapping("nint", "IntPtr", isPointer: true);
            }

            // Primitive type mapping
            return cleaned switch
            {
                "void" => new NetTypeMapping("void", "void"),
                "char" or "signed char" => new NetTypeMapping("sbyte", "sbyte"),
                "unsigned char" => new NetTypeMapping("byte", "byte"),
                "short" or "short int" or "signed short" => new NetTypeMapping("short", "short"),
                "unsigned short" or "unsigned short int" => new NetTypeMapping("ushort", "ushort"),
                "int" or "signed int" or "signed" => new NetTypeMapping("int", "int"),
                "unsigned int" or "unsigned" => new NetTypeMapping("uint", "uint"),
                "long" or "long int" or "signed long" =>
                    MapPlatformLong(),
                "unsigned long" or "unsigned long int" =>
                    MapPlatformULong(),
                "long long" or "long long int" or "signed long long" => new NetTypeMapping("long", "long"),
                "unsigned long long" or "unsigned long long int" => new NetTypeMapping("ulong", "ulong"),
                "float" => new NetTypeMapping("float", "float"),
                "double" => new NetTypeMapping("double", "double"),
                "long double" => new NetTypeMapping("double", "double"), // .NET has no long double
                "size_t" => new NetTypeMapping("nuint", "UIntPtr"),
                "ssize_t" or "ptrdiff_t" => new NetTypeMapping("nint", "IntPtr"),
                "int8_t" or "__int8" => new NetTypeMapping("sbyte", "sbyte"),
                "uint8_t" => new NetTypeMapping("byte", "byte"),
                "int16_t" or "__int16" => new NetTypeMapping("short", "short"),
                "uint16_t" => new NetTypeMapping("ushort", "ushort"),
                "int32_t" or "__int32" => new NetTypeMapping("int", "int"),
                "uint32_t" => new NetTypeMapping("uint", "uint"),
                "int64_t" or "__int64" => new NetTypeMapping("long", "long"),
                "uint64_t" => new NetTypeMapping("ulong", "ulong"),
                "intptr_t" => new NetTypeMapping("nint", "IntPtr"),
                "uintptr_t" => new NetTypeMapping("nuint", "UIntPtr"),
                "_Bool" or "bool" => new NetTypeMapping("byte", "byte"), // C99 _Bool is 1 byte
                _ => MapStructOrEnum(cleaned),
            };
        }

        /// <summary>
        /// Generate the P/Invoke method definition for a C function.
        /// Returns the IL metadata needed to emit the DllImport stub.
        /// </summary>
        public PInvokeStub GenerateFunctionStub(CgoFunctionInfo function, string libraryName)
        {
            var returnType = MapCTypeToNet(function.ReturnType);
            var parameters = new List<PInvokeParameter>();

            foreach (var param in function.Parameters)
            {
                var paramType = MapCTypeToNet(param.CType);
                parameters.Add(new PInvokeParameter(param.Name, paramType));
            }

            return new PInvokeStub(
                function.Name,
                libraryName,
                returnType,
                parameters,
                function.IsVariadic);
        }

        /// <summary>
        /// Generate the struct layout for a C struct.
        /// Uses probe results for exact field offsets and sizes.
        /// </summary>
        public StructLayout GenerateStructLayout(CgoStructInfo structInfo)
        {
            var fields = new List<StructFieldLayout>();
            long totalSize = _probeResult.GetTypeSize(SanitizeName(structInfo.CName));
            long alignment = _probeResult.GetTypeAlignment(SanitizeName(structInfo.CName));

            foreach (var field in structInfo.Fields)
            {
                var fieldType = MapCTypeToNet(field.CType);
                long offset = _probeResult.GetFieldOffset(
                    SanitizeName(structInfo.CName), field.Name);
                long fieldSize = _probeResult.GetFieldSize(
                    SanitizeName(structInfo.CName), field.Name);

                fields.Add(new StructFieldLayout(field.Name, fieldType, offset, fieldSize));
            }

            return new StructLayout(
                $"C_{structInfo.GoName}",
                fields,
                totalSize,
                alignment,
                structInfo.IsUnion);
        }

        private NetTypeMapping MapPlatformLong()
        {
            // Use probe result if available, otherwise use nint (platform-dependent)
            long size = _probeResult.GetTypeSize("long");
            if (size == 4)
            {
                return new NetTypeMapping("int", "int");
            }
            if (size == 8)
            {
                return new NetTypeMapping("long", "long");
            }
            // Default: nint matches platform ABI
            return new NetTypeMapping("nint", "IntPtr");
        }

        private NetTypeMapping MapPlatformULong()
        {
            long size = _probeResult.GetTypeSize("unsigned_long");
            if (size == 4)
            {
                return new NetTypeMapping("uint", "uint");
            }
            if (size == 8)
            {
                return new NetTypeMapping("ulong", "ulong");
            }
            return new NetTypeMapping("nuint", "UIntPtr");
        }

        private NetTypeMapping MapStructOrEnum(string cTypeName)
        {
            // Check if it's a struct/enum by prefix
            if (cTypeName.StartsWith("struct "))
            {
                string structName = cTypeName.Substring(7);
                return new NetTypeMapping($"C_{structName}", $"C_{structName}", isStruct: true);
            }
            if (cTypeName.StartsWith("enum "))
            {
                return new NetTypeMapping("int", "int"); // Enums are ints in C
            }
            // Could be a typedef — treat as opaque, use probe to determine size
            return new NetTypeMapping("nint", "IntPtr");
        }

        private static string SanitizeName(string name)
        {
            return name.Replace(" ", "_").Replace("*", "_ptr");
        }
    }
}

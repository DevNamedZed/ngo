using System.Collections.Generic;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Builds Go-typed symbols for the C pseudo-package from probe results.
    /// Each C function becomes a FunctionSymbol, each C type becomes a TypeSymbol,
    /// and they're registered as exports on a PackageSymbol named "C".
    /// </summary>
    public class CgoSymbolBuilder
    {
        private readonly MarshallingStubGenerator _marshaller;
        private readonly CgoProbeResult _probeResult;

        public CgoSymbolBuilder(CgoProbeResult probeResult)
        {
            _probeResult = probeResult;
            _marshaller = new MarshallingStubGenerator(probeResult);
        }

        /// <summary>
        /// Build the C pseudo-package with all exported symbols.
        /// </summary>
        public PackageSymbol BuildCPackage(
            List<CgoFunctionInfo> functions,
            List<CgoStructInfo> structs,
            string libraryName)
        {
            var package = new PackageSymbol("C", "C");

            AddPrimitiveTypeAliases(package);

            foreach (var func in functions)
            {
                var funcSymbol = BuildFunctionSymbol(func);
                if (funcSymbol != null)
                {
                    package.AddExport(funcSymbol);
                }
            }

            foreach (var structInfo in structs)
            {
                var typeSymbol = BuildStructTypeSymbol(structInfo);
                if (typeSymbol != null)
                {
                    package.AddExport(typeSymbol);
                }
            }

            AddHelperFunctions(package);

            // Add C.sizeof_T constants from probe results
            AddSizeofConstants(package);

            // Add enum constants from probe results
            AddEnumConstants(package);

            return package;
        }

        private void AddPrimitiveTypeAliases(PackageSymbol package)
        {
            var primitives = new Dictionary<string, TypeKind>
            {
                { "char", TypeKind.Int8 },
                { "schar", TypeKind.Int8 },
                { "uchar", TypeKind.Uint8 },
                { "short", TypeKind.Int16 },
                { "ushort", TypeKind.Uint16 },
                { "int", TypeKind.Int32 },
                { "uint", TypeKind.Uint32 },
                { "long", GetGoLongTypeKind() },
                { "ulong", GetGoULongTypeKind() },
                { "longlong", TypeKind.Int64 },
                { "ulonglong", TypeKind.Uint64 },
                { "float", TypeKind.Float32 },
                { "double", TypeKind.Float64 },
                { "size_t", TypeKind.Uintptr },
            };

            foreach (var kv in primitives)
            {
                var typeSymbol = new TypeSymbol(kv.Key, kv.Value, null);
                package.AddExport(typeSymbol);
            }
        }

        private FunctionSymbol? BuildFunctionSymbol(CgoFunctionInfo func)
        {
            var returnType = MapCToGoType(func.ReturnType);
            var parameters = new List<ParameterSymbol>();

            for (int i = 0; i < func.Parameters.Count; i++)
            {
                var param = func.Parameters[i];
                var paramType = MapCToGoType(param.CType);
                parameters.Add(new ParameterSymbol(param.Name, paramType, i));
            }

            return new FunctionSymbol(func.Name, parameters, returnType);
        }

        private TypeSymbol? BuildStructTypeSymbol(CgoStructInfo structInfo)
        {
            var fields = new List<FieldSymbol>();
            for (int i = 0; i < structInfo.Fields.Count; i++)
            {
                var field = structInfo.Fields[i];
                var fieldType = MapCToGoType(field.CType);
                fields.Add(new FieldSymbol(field.Name, fieldType, i));
            }
            return new StructTypeSymbol(structInfo.GoName, fields);
        }

        private void AddHelperFunctions(PackageSymbol package)
        {
            var stringType = new TypeSymbol("string", TypeKind.String, null);
            var ptrType = new TypeSymbol("unsafe.Pointer", TypeKind.Uintptr, null);
            var intType = new TypeSymbol("int32", TypeKind.Int32, null);
            var byteSliceType = new TypeSymbol("[]byte", TypeKind.Slice, null);
            var voidType = new TypeSymbol("void", TypeKind.Void, null);

            package.AddExport(new FunctionSymbol("CString",
                new List<ParameterSymbol> { new("s", stringType, 0) }, ptrType));

            package.AddExport(new FunctionSymbol("GoString",
                new List<ParameterSymbol> { new("p", ptrType, 0) }, stringType));

            package.AddExport(new FunctionSymbol("GoStringN",
                new List<ParameterSymbol> { new("p", ptrType, 0), new("n", intType, 1) }, stringType));

            package.AddExport(new FunctionSymbol("GoBytes",
                new List<ParameterSymbol> { new("p", ptrType, 0), new("n", intType, 1) }, byteSliceType));

            package.AddExport(new FunctionSymbol("CBytes",
                new List<ParameterSymbol> { new("b", byteSliceType, 0) }, ptrType));

            package.AddExport(new FunctionSymbol("free",
                new List<ParameterSymbol> { new("p", ptrType, 0) }, voidType));
        }

        private TypeSymbol MapCToGoType(string cType)
        {
            var mapping = _marshaller.MapCTypeToNet(cType);
            TypeKind kind = mapping.CSharpType switch
            {
                "void" => TypeKind.Void,
                "sbyte" => TypeKind.Int8,
                "byte" => TypeKind.Uint8,
                "short" => TypeKind.Int16,
                "ushort" => TypeKind.Uint16,
                "int" => TypeKind.Int32,
                "uint" => TypeKind.Uint32,
                "long" => TypeKind.Int64,
                "ulong" => TypeKind.Uint64,
                "nint" => TypeKind.Uintptr,
                "nuint" => TypeKind.Uintptr,
                "float" => TypeKind.Float32,
                "double" => TypeKind.Float64,
                _ => TypeKind.Uintptr,
            };
            return new TypeSymbol(cType, kind, null);
        }

        private TypeKind GetGoLongTypeKind()
        {
            long size = _probeResult.GetTypeSize("long");
            return size == 8 ? TypeKind.Int64 : TypeKind.Int32;
        }

        private TypeKind GetGoULongTypeKind()
        {
            long size = _probeResult.GetTypeSize("unsigned_long");
            return size == 8 ? TypeKind.Uint64 : TypeKind.Uint32;
        }

        private void AddSizeofConstants(PackageSymbol package)
        {
            // Add C.sizeof_int, C.sizeof_long, C.sizeof_struct_X, etc.
            var uintptrType = new TypeSymbol("uintptr", TypeKind.Uintptr, null);

            // Standard type sizes
            var standardTypes = new Dictionary<string, string>
            {
                { "sizeof_char", "char" },
                { "sizeof_short", "short" },
                { "sizeof_int", "int" },
                { "sizeof_long", "long" },
                { "sizeof_longlong", "long_long" },
                { "sizeof_float", "float" },
                { "sizeof_double", "double" },
                { "sizeof_void_ptr", "void_ptr" },
            };

            // Add known sizes (even without probe — use platform defaults)
            foreach (var kv in standardTypes)
            {
                long size = _probeResult.GetTypeSize(kv.Value);
                if (size < 0)
                {
                    // Default sizes when probe hasn't run
                    size = kv.Value switch
                    {
                        "char" => 1,
                        "short" => 2,
                        "int" => 4,
                        "long" => System.IntPtr.Size, // Platform-dependent
                        "long_long" => 8,
                        "float" => 4,
                        "double" => 8,
                        "void_ptr" => System.IntPtr.Size,
                        _ => System.IntPtr.Size,
                    };
                }
                package.AddExport(new ConstantSymbol(kv.Key, uintptrType, size));
            }

            // Add sizes from probe results for custom types
            foreach (var kv in _probeResult.TypeSizes)
            {
                string constName = $"sizeof_{kv.Key}";
                if (package.LookupExport(constName) == null)
                {
                    package.AddExport(new ConstantSymbol(constName, uintptrType, kv.Value));
                }
            }
        }

        private void AddEnumConstants(PackageSymbol package)
        {
            // Add C.ENUM_VALUE constants from probe results
            var intType = new TypeSymbol("int", TypeKind.Int32, null);

            foreach (var kv in _probeResult.EnumValues)
            {
                if (package.LookupExport(kv.Key) == null)
                {
                    package.AddExport(new ConstantSymbol(kv.Key, intType, kv.Value));
                }
            }
        }
    }
}

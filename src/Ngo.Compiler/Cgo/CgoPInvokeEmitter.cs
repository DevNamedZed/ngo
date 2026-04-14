using System;
using System.Collections.Generic;
using System.Reflection;
using Ngo.Compiler.Emit;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Emits P/Invoke method stubs and helper methods for CGo interop.
    /// Creates a static class with [DllImport] methods for each C function,
    /// plus marshalling helpers (CString, GoString, free).
    /// Registers the methods in EmitContext so MethodBodyEmitter can call them.
    /// </summary>
    internal class CgoPInvokeEmitter
    {
        public static void Emit(EmitContext context, CompilationContext compilation)
        {
            var preamble = compilation.CgoPreamble;
            if (preamble == null)
            {
                return;
            }

            var moduleBuilder = context.Module;
            var cgoType = moduleBuilder.DefineType(
                "Cgo_Helpers",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            // Emit marshalling helpers (CString, GoString, GoStringN, Free)
            var cstringMethod = EmitMarshallingHelper(cgoType, "CString",
                typeof(IntPtr), new[] { typeof(string) }, writer =>
                {
                    writer.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    writer.Emit(System.Reflection.Emit.OpCodes.Call,
                        typeof(System.Runtime.InteropServices.Marshal)
                            .GetMethod("StringToHGlobalAnsi", new[] { typeof(string) })!);
                    writer.Emit(System.Reflection.Emit.OpCodes.Ret);
                });

            var gostringMethod = EmitMarshallingHelper(cgoType, "GoString",
                typeof(string), new[] { typeof(IntPtr) }, writer =>
                {
                    writer.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    writer.Emit(System.Reflection.Emit.OpCodes.Call,
                        typeof(System.Runtime.InteropServices.Marshal)
                            .GetMethod("PtrToStringAnsi", new[] { typeof(IntPtr) })!);
                    writer.Emit(System.Reflection.Emit.OpCodes.Ret);
                });

            var gostringNMethod = EmitMarshallingHelper(cgoType, "GoStringN",
                typeof(string), new[] { typeof(IntPtr), typeof(int) }, writer =>
                {
                    writer.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    writer.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
                    writer.Emit(System.Reflection.Emit.OpCodes.Call,
                        typeof(System.Runtime.InteropServices.Marshal)
                            .GetMethod("PtrToStringAnsi", new[] { typeof(IntPtr), typeof(int) })!);
                    writer.Emit(System.Reflection.Emit.OpCodes.Ret);
                });

            var freeMethod = EmitMarshallingHelper(cgoType, "Free",
                typeof(void), new[] { typeof(IntPtr) }, writer =>
                {
                    writer.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                    writer.Emit(System.Reflection.Emit.OpCodes.Call,
                        typeof(System.Runtime.InteropServices.Marshal)
                            .GetMethod("FreeHGlobal", new[] { typeof(IntPtr) })!);
                    writer.Emit(System.Reflection.Emit.OpCodes.Ret);
                });

            cgoType.CreateType();

            // Register helper methods in EmitContext so C.CString(), C.GoString(), C.free() resolve
            RegisterCgoHelpers(context, compilation, cgoType,
                cstringMethod, gostringMethod, gostringNMethod, freeMethod);

            // Emit P/Invoke stubs for user-declared C functions
            EmitUserFunctionStubs(context, compilation);

            // Emit .NET struct types for C structs defined in the preamble
            EmitCgoStructTypes(context, compilation);

            // Emit NativeLibrary resolver so the CLR can find the native library
            EmitNativeLibraryResolver(context, compilation);
        }

        private static void RegisterCgoHelpers(
            EmitContext context, CompilationContext compilation, ITypeBuilder cgoType,
            IMethodBuilder cstringMethod, IMethodBuilder gostringMethod,
            IMethodBuilder gostringNMethod, IMethodBuilder freeMethod)
        {
            var cgoPackage = compilation.CgoPackage;
            if (cgoPackage == null)
            {
                return;
            }

            // Match helper FunctionSymbols from the C package to their emitted methods
            var helperMap = new Dictionary<string, IMethodBuilder>
            {
                { "CString", cstringMethod },
                { "GoString", gostringMethod },
                { "GoStringN", gostringNMethod },
                { "free", freeMethod },
            };

            foreach (var export in cgoPackage.Exports)
            {
                if (export.Value is FunctionSymbol funcSym && helperMap.TryGetValue(funcSym.Name, out var methodBuilder))
                {
                    context.Methods[funcSym] = methodBuilder;
                }
            }
        }

        private static void EmitUserFunctionStubs(EmitContext context, CompilationContext compilation)
        {
            var catalog = compilation.CgoCatalog;
            var cgoPackage = compilation.CgoPackage;
            if (catalog == null || catalog.Functions.Count == 0 || cgoPackage == null)
            {
                return;
            }

            var probeResult = compilation.CgoResult?.ProbeResult ?? new CgoProbeResult();
            var marshaller = new MarshallingStubGenerator(probeResult);

            var pinvokeType = context.Module.DefineType(
                "Cgo_PInvoke",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            foreach (CgoFunctionInfo function in catalog.Functions.Values)
            {
                PInvokeStub stub = marshaller.GenerateFunctionStub(function, "ngo_native");

                Type returnType = MapToClrType(stub.ReturnType);
                Type[] parameterTypes = new Type[stub.Parameters.Count];
                for (int parameterIndex = 0; parameterIndex < stub.Parameters.Count; parameterIndex++)
                {
                    parameterTypes[parameterIndex] = MapToClrType(stub.Parameters[parameterIndex].Type);
                }

                const string libraryName = "ngo_native";
                var pinvokeMethod = pinvokeType.DefinePInvokeMethod(
                    function.Name,
                    libraryName,
                    function.Name,
                    MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PinvokeImpl,
                    CallingConventions.Standard,
                    returnType,
                    parameterTypes,
                    System.Runtime.InteropServices.CallingConvention.Cdecl,
                    System.Runtime.InteropServices.CharSet.Ansi);

                var functionSymbol = cgoPackage.LookupExport(function.Name);
                if (functionSymbol != null)
                {
                    context.CachedMethods[functionSymbol] = pinvokeMethod;
                }
            }

            pinvokeType.CreateType();
        }

        private static IMethodBuilder EmitMarshallingHelper(
            ITypeBuilder typeBuilder, string name, Type returnType, Type[] paramTypes,
            Action<CilWriter> emitBody)
        {
            var method = typeBuilder.DefineMethod(
                name,
                MethodAttributes.Public | MethodAttributes.Static,
                returnType,
                paramTypes);

            var writer = method.GetILWriter();
            emitBody(writer);
            return method;
        }

        /// <summary>
        /// Emit .NET struct types with sequential layout for C structs in the catalog.
        /// Each C struct becomes a .NET value type with exact field offsets from the probe.
        /// </summary>
        private static void EmitCgoStructTypes(EmitContext context, CompilationContext compilation)
        {
            var catalog = compilation.CgoCatalog;
            var cgoPackage = compilation.CgoPackage;
            if (catalog == null || catalog.StructsAndUnions.Count == 0 || cgoPackage == null)
            {
                return;
            }

            var probeResult = compilation.CgoResult?.ProbeResult ?? new CgoProbeResult();
            var marshaller = new MarshallingStubGenerator(probeResult);

            foreach (CgoStructInfo structInfo in catalog.StructsAndUnions.Values)
            {
                var layout = marshaller.GenerateStructLayout(structInfo);

                var structType = context.Module.DefineType(
                    layout.NetTypeName,
                    TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
                    typeof(System.ValueType));

                foreach (var field in layout.Fields)
                {
                    var fieldType = MapToClrType(field.Type);
                    structType.DefineField(
                        field.Name,
                        fieldType,
                        FieldAttributes.Public);
                }

                var goStructSym = cgoPackage.LookupExport(structInfo.GoName);
                if (goStructSym is StructTypeSymbol sts)
                {
                    context.StructTypes[sts] = structType;
                }
                else
                {
                    structType.CreateType();
                }
            }
        }

        /// <summary>
        /// Emit a static class with a resolver method that helps the CLR find
        /// libngo_native.so/ngo_native.dll at runtime. The resolver searches
        /// relative to the assembly's directory and common native lib paths.
        ///
        /// The main package's .cctor calls Cgo_NativeResolver.Initialize()
        /// which registers the DllImport resolver via NativeLibrary.SetDllImportResolver.
        /// </summary>
        private static void EmitNativeLibraryResolver(EmitContext context, CompilationContext compilation)
        {
            var catalog = compilation.CgoCatalog;
            if (catalog == null || catalog.Functions.Count == 0)
            {
                return;
            }

            // Create a static class with Initialize() that registers the resolver
            var resolverType = context.Module.DefineType(
                "Cgo_NativeResolver",
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);

            // Emit: public static void Initialize()
            // {
            //     NativeLibrary.SetDllImportResolver(
            //         typeof(Cgo_PInvoke).Assembly,
            //         (name, assembly, paths) => {
            //             var dir = Path.GetDirectoryName(assembly.Location) ?? ".";
            //             var libPath = Path.Combine(dir, GetPlatformLibName(name));
            //             if (NativeLibrary.TryLoad(libPath, out var handle))
            //                 return handle;
            //             return NativeLibrary.Load(name);
            //         });
            // }
            //
            // We can't emit lambda IL easily, so instead we register the resolver
            // by calling a runtime helper in Ngo.Runtime.
            var initMethod = resolverType.DefineMethod(
                "Initialize",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(void),
                Type.EmptyTypes);

            var writer = initMethod.GetILWriter();
            // Call Ngo.Runtime.CgoNativeResolver.Register() which handles
            // the NativeLibrary.SetDllImportResolver call at runtime
            var registerMethod = typeof(Ngo.Runtime.CgoNativeResolver).GetMethod(
                "Register", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (registerMethod != null)
            {
                // Pass the calling assembly so the resolver knows where to search
                writer.Emit(System.Reflection.Emit.OpCodes.Call,
                    typeof(System.Reflection.Assembly).GetMethod("GetCallingAssembly")!);
                writer.Emit(System.Reflection.Emit.OpCodes.Call, registerMethod);
            }
            writer.Emit(System.Reflection.Emit.OpCodes.Ret);

            resolverType.CreateType();

            // Store resolver type so the package .cctor can call Initialize()
            context.CgoResolverInitMethod = initMethod;
        }

        private static Type MapToClrType(NetTypeMapping mapping)
        {
            return mapping.CSharpType switch
            {
                "void" => typeof(void),
                "sbyte" => typeof(sbyte),
                "byte" => typeof(byte),
                "short" => typeof(short),
                "ushort" => typeof(ushort),
                "int" => typeof(int),
                "uint" => typeof(uint),
                "long" => typeof(long),
                "ulong" => typeof(ulong),
                "nint" => typeof(nint),
                "nuint" => typeof(nuint),
                "float" => typeof(float),
                "double" => typeof(double),
                _ => typeof(nint),
            };
        }
    }
}

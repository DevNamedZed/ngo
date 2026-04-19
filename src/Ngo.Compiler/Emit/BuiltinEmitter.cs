// -----------------------------------------------------------------------
// <copyright file="BuiltinEmitter.cs" company="Ziad">
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
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Ast;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Symbols;
using Ngo.Runtime;
using Ngo.Runtime.Io;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Emits IL for builtin function calls and standard library dispatch.
    /// Uses [GoPackage] annotations for automatic package → CLR type resolution.
    /// </summary>
    internal sealed class BuiltinEmitter
    {
        private readonly EmitContext _ctx;
        private readonly MethodBodyEmitter _body;

        /// <summary>
        /// Package import path → CLR type mapping, built from [GoPackage] attributes.
        /// </summary>
        private static readonly Dictionary<string, Type> _packageTypes = BuildPackageTypeMap();

        private static Dictionary<string, Type> BuildPackageTypeMap()
        {
            var map = new Dictionary<string, Type>();
            var asm = typeof(Ngo.Runtime.Discovery.GoPackageAttribute).Assembly;
            foreach (var type in asm.GetTypes())
            {
                var attr = type.GetCustomAttribute<Ngo.Runtime.Discovery.GoPackageAttribute>();
                if (attr != null)
                {
                    map[attr.ImportPath] = type;
                    // Also map by short name (e.g., "fmt" for "fmt", "json" for "encoding/json")
                    var lastSlash = attr.ImportPath.LastIndexOf('/');
                    var shortName = lastSlash >= 0 ? attr.ImportPath.Substring(lastSlash + 1) : attr.ImportPath;
                    if (!map.ContainsKey(shortName))
                        map[shortName] = type;
                }
            }
            return map;
        }

        public BuiltinEmitter(EmitContext ctx, MethodBodyEmitter body)
        {
            _ctx = ctx;
            _body = body;
        }

        public bool EmitBuiltinCall(CallExpression call)
        {
            var name = call.Function.Name;
            var pkg = string.IsNullOrEmpty(call.Function.PackageName) ? null : call.Function.PackageName;

            // Special cases that need custom emission (variadic, interface wrapping, constructors)
            if (pkg != null && TryEmitSpecialCase(call, pkg, name))
                return true;

            // Generic package dispatch via [GoPackage] annotations
            if (pkg != null)
            {
                Type? targetType = null;
                _packageTypes.TryGetValue(pkg, out targetType);
                if (targetType != null)
                {
                    return EmitStaticCall(call, targetType, name);
                }
            }

            // True language builtins (no package qualifier)
            switch (name)
            {
                case "println":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, typeof(BuiltIn).GetMethod("Println")!);
                    return true;

                case "print":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, typeof(BuiltIn).GetMethod("Print")!);
                    return true;

                case "len":
                    return EmitBuiltinLen(call);

                case "cap":
                    return EmitBuiltinCap(call);

                case "append":
                    return EmitBuiltinAppend(call);

                case "make":
                    return EmitBuiltinMake(call);

                case "delete":
                    return EmitBuiltinDelete(call);

                case "copy":
                    return EmitBuiltinCopy(call);

                case "close":
                    return EmitBuiltinClose(call);

                case "panic":
                    _body.EmitExpression(call.Arguments[0]);
                    _body.EmitBoxIfNeeded(call.Arguments[0].Type);
                    _ctx.IL.Emit(OpCodes.Call, typeof(BuiltIn).GetMethod("Panic")!);
                    return true;

                case "recover":
                    _ctx.IL.Emit(OpCodes.Call, typeof(BuiltIn).GetMethod("Recover")!);
                    return true;

                case "min":
                    return EmitMinMax(call, isMin: true);

                case "max":
                    return EmitMinMax(call, isMin: false);

                case "clear":
                    return EmitClear(call);

                case "new":
                    return EmitBuiltinNew(call);

                case "complex":
                    return EmitBuiltinComplex(call);
                case "real":
                    return EmitBuiltinReal(call);
                case "imag":
                    return EmitBuiltinImag(call);

                default:
                    return false;
            }
        }

        private bool TryEmitSpecialCase(CallExpression call, string pkg, string name)
        {
            switch (pkg)
            {
                case "fmt":
                    return TryEmitFmtSpecial(call, name);

                case "io":
                    return TryEmitIoSpecial(call, name);

                case "ioutil":
                    return TryEmitIoutilSpecial(call, name);

                case "bufio":
                    return TryEmitBufioSpecial(call, name);

                case "csv":
                    return TryEmitCsvSpecial(call, name);

                case "errors":
                    if (name == "Join")
                    {
                        EmitVariadicObjectArray(call, 0);
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Ngo.Runtime.Errors.Package).GetMethod("Join")!);
                        return true;
                    }
                    return false;

                case "filepath":
                    if (name == "Join")
                    {
                        return false;
                    }
                    return false;

                case "log":
                    return TryEmitLogSpecial(call, name);

                case "strings":
                    return TryEmitStringsSpecial(call, name);

                case "dotnet":
                    return EmitDotnetCall(call, name);

                case "internal/abi":
                case "abi":
                    return TryEmitInternalAbiSpecial(call, name);

                default:
                    return false;
            }
        }

        private bool TryEmitInternalAbiSpecial(CallExpression call, string name)
        {
            switch (name)
            {
                case "Escape":
                case "NoEscape":
                {
                    if (call.Arguments.Count != 1)
                    {
                        throw new NotSupportedException(
                            $"internal/abi.{name} expects exactly 1 argument, got {call.Arguments.Count}");
                    }
                    _body.EmitExpression(call.Arguments[0]);
                    return true;
                }

                case "TypeOf":
                {
                    if (call.Arguments.Count != 1)
                    {
                        throw new NotSupportedException(
                            $"internal/abi.TypeOf expects exactly 1 argument, got {call.Arguments.Count}");
                    }
                    _body.EmitExpression(call.Arguments[0]);
                    var argClrType = _ctx.Mapper.Map(call.Arguments[0].Type);
                    if (argClrType.IsValueType)
                    {
                        _ctx.IL.Emit(OpCodes.Box, argClrType);
                    }
                    var typeOfMethod = typeof(Ngo.Runtime.Internal.Abi.Package)
                        .GetMethod("TypeOf", new[] { typeof(object) });
                    if (typeOfMethod == null)
                    {
                        throw new InvalidOperationException(
                            "Ngo.Runtime.Internal.Abi.Package.TypeOf(object) not found");
                    }
                    _ctx.IL.Emit(OpCodes.Call, typeOfMethod);
                    return true;
                }

                case "TypeFor":
                {
                    if (call.TypeArguments == null || call.TypeArguments.Count != 1)
                    {
                        throw new NotSupportedException(
                            $"internal/abi.TypeFor expects exactly 1 type argument, got {call.TypeArguments?.Count ?? 0}");
                    }
                    var typeArg = _ctx.Mapper.Map(call.TypeArguments[0]);
                    _ctx.IL.Emit(OpCodes.Ldtoken, typeArg);
                    var getTypeFromHandle = typeof(Type).GetMethod(
                        "GetTypeFromHandle",
                        new[] { typeof(RuntimeTypeHandle) });
                    if (getTypeFromHandle == null)
                    {
                        throw new InvalidOperationException(
                            "System.Type.GetTypeFromHandle(RuntimeTypeHandle) not found");
                    }
                    _ctx.IL.Emit(OpCodes.Call, getTypeFromHandle);
                    var typeForMethod = typeof(Ngo.Runtime.Internal.Abi.Package)
                        .GetMethod("TypeForType", new[] { typeof(Type) });
                    if (typeForMethod == null)
                    {
                        throw new InvalidOperationException(
                            "Ngo.Runtime.Internal.Abi.Package.TypeForType(Type) not found");
                    }
                    _ctx.IL.Emit(OpCodes.Call, typeForMethod);
                    return true;
                }
            }
            return false;
        }

        private bool TryEmitFmtSpecial(CallExpression call, string name)
        {
            var fmtType = typeof(Ngo.Runtime.Fmt.Package);
            switch (name)
            {
                case "Printf":
                    EmitFmtFormatCall(call, "Printf", fmtType);
                    return true;
                case "Sprintf":
                    EmitFmtFormatCall(call, "Sprintf", fmtType);
                    return true;
                case "Errorf":
                    EmitFmtFormatCall(call, "Errorf", fmtType);
                    return true;
                case "Fprintf":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                    EmitFmtFormatCallFrom(call, "Fprintf", fmtType, 1);
                    return true;
                case "Fprintln":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                    EmitFmtWriterPrintArgs(call, 1);
                    _ctx.IL.Emit(OpCodes.Call,
                        fmtType.GetMethod("Fprintln", new[] { typeof(IGoWriter), typeof(object[]) })!);
                    return true;
                case "Fprint":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                    EmitFmtWriterPrintArgs(call, 1);
                    _ctx.IL.Emit(OpCodes.Call,
                        fmtType.GetMethod("Fprint", new[] { typeof(IGoWriter), typeof(object[]) })!);
                    return true;
                case "Println":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, fmtType.GetMethod("Println")!);
                    return true;
                case "Print":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, fmtType.GetMethod("Print")!);
                    return true;
                case "Sprint":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, fmtType.GetMethod("Sprint")!);
                    return true;
                case "Sprintln":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, fmtType.GetMethod("Sprintln")!);
                    return true;
                case "Scan":
                    EmitFmtWriterPrintArgs(call, 0);
                    _ctx.IL.Emit(OpCodes.Call,
                        fmtType.GetMethod("Scan", new[] { typeof(object[]) })!);
                    return true;
                case "Scanf":
                    _body.EmitExpression(call.Arguments[0]);
                    EmitFmtWriterPrintArgs(call, 1);
                    _ctx.IL.Emit(OpCodes.Call,
                        fmtType.GetMethod("Scanf", new[] { typeof(string), typeof(object[]) })!);
                    return true;
                case "Scanln":
                    EmitFmtWriterPrintArgs(call, 0);
                    _ctx.IL.Emit(OpCodes.Call,
                        fmtType.GetMethod("Scanln", new[] { typeof(object[]) })!);
                    return true;
                case "Sscan":
                    _body.EmitExpression(call.Arguments[0]);
                    EmitFmtWriterPrintArgs(call, 1);
                    _ctx.IL.Emit(OpCodes.Call,
                        fmtType.GetMethod("Sscan", new[] { typeof(string), typeof(object[]) })!);
                    return true;
                case "Sscanf":
                    _body.EmitExpression(call.Arguments[0]);
                    _body.EmitExpression(call.Arguments[1]);
                    EmitFmtWriterPrintArgs(call, 2);
                    _ctx.IL.Emit(OpCodes.Call,
                        fmtType.GetMethod("Sscanf", new[] { typeof(string), typeof(string), typeof(object[]) })!);
                    return true;
                case "Sscanln":
                    _body.EmitExpression(call.Arguments[0]);
                    EmitFmtWriterPrintArgs(call, 1);
                    _ctx.IL.Emit(OpCodes.Call,
                        fmtType.GetMethod("Sscanln", new[] { typeof(string), typeof(object[]) })!);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryEmitIoSpecial(CallExpression call, string name)
        {
            var ioType = typeof(Ngo.Runtime.Io.GoIo);
            switch (name)
            {
                case "Copy":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                    _body.EmitExpression(call.Arguments[1]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                    _ctx.IL.Emit(OpCodes.Call, ioType.GetMethod("Copy")!);
                    return true;
                case "ReadAll":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                    _ctx.IL.Emit(OpCodes.Call, ioType.GetMethod("ReadAll")!);
                    return true;
                case "WriteString":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                    _body.EmitExpression(call.Arguments[1]);
                    _ctx.IL.Emit(OpCodes.Call, ioType.GetMethod("WriteString")!);
                    return true;
                case "NopCloser":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                    _ctx.IL.Emit(OpCodes.Call, ioType.GetMethod("NopCloser")!);
                    return true;
                case "LimitReader":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                    _body.EmitExpression(call.Arguments[1]);
                    _ctx.IL.Emit(OpCodes.Call, ioType.GetMethod("LimitReader")!);
                    return true;
                case "MultiReader":
                    EmitVariadicInterfaceArray(call, typeof(IGoReader), 0);
                    _ctx.IL.Emit(OpCodes.Call, ioType.GetMethod("MultiReader")!);
                    return true;
                case "MultiWriter":
                    EmitVariadicInterfaceArray(call, typeof(IGoWriter), 0);
                    _ctx.IL.Emit(OpCodes.Call, ioType.GetMethod("MultiWriter")!);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryEmitIoutilSpecial(CallExpression call, string name)
        {
            var ioType = typeof(Ngo.Runtime.Io.GoIo);
            var osType = typeof(Ngo.Runtime.Os.GoOs);
            switch (name)
            {
                case "ReadAll":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                    _ctx.IL.Emit(OpCodes.Call, ioType.GetMethod("ReadAll")!);
                    return true;
                case "ReadFile":
                    return EmitStaticCall(call, osType, "ReadFile");
                case "WriteFile":
                    return EmitStaticCall(call, osType, "WriteFile");
                case "NopCloser":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                    _ctx.IL.Emit(OpCodes.Call, ioType.GetMethod("NopCloser")!);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryEmitBufioSpecial(CallExpression call, string name)
        {
            switch (name)
            {
                // bufio compiles from Go source
                default:
                    return false;
            }
        }

        private bool TryEmitCsvSpecial(CallExpression call, string name)
        {
            return false;
        }

        private bool TryEmitLogSpecial(CallExpression call, string name)
        {
            return false;
        }

        private bool TryEmitStringsSpecial(CallExpression call, string name)
        {
            switch (name)
            {
                case "NewReader":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Newobj,
                        typeof(Ngo.Runtime.Strings.Reader).GetConstructor(new[] { typeof(GoString) })!);
                    return true;
                case "NewReplacer":
                    EmitVariadicStringArray(call, 0);
                    _ctx.IL.Emit(OpCodes.Newobj,
                        typeof(Ngo.Runtime.Strings.Replacer).GetConstructor(new[] { typeof(string[]) })!);
                    return true;
                default:
                    return false;
            }
        }

        // --- Variadic helpers ---

        private void EmitVariadicObjectArray(CallExpression call, int startIndex)
        {
            int count = call.Arguments.Count - startIndex;
            _ctx.IL.Emit(OpCodes.Ldc_I4, count);
            _ctx.IL.Emit(OpCodes.Newarr, typeof(object));
            for (int i = 0; i < count; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _body.EmitExpression(call.Arguments[startIndex + i]);
                var argType = _ctx.Mapper.Map(call.Arguments[startIndex + i].Type);
                if (argType.IsValueType)
                    _ctx.IL.Emit(OpCodes.Box, argType);
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitVariadicStringArray(CallExpression call, int startIndex)
        {
            int count = call.Arguments.Count - startIndex;
            _ctx.IL.Emit(OpCodes.Ldc_I4, count);
            _ctx.IL.Emit(OpCodes.Newarr, typeof(string));
            for (int i = 0; i < count; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _body.EmitExpression(call.Arguments[startIndex + i]);
                EmitGoStringToNetString();
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitVariadicInterfaceArray(CallExpression call, Type ifaceType, int startIndex)
        {
            int count = call.Arguments.Count - startIndex;
            _ctx.IL.Emit(OpCodes.Ldc_I4, count);
            _ctx.IL.Emit(OpCodes.Newarr, ifaceType);
            for (int i = 0; i < count; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _body.EmitExpression(call.Arguments[startIndex + i]);
                var argClrType = _ctx.Mapper.Map(call.Arguments[startIndex + i].Type);
                if (argClrType.IsValueType)
                {
                    _ctx.IL.Emit(OpCodes.Box, argClrType);
                }
                _ctx.IL.Emit(OpCodes.Castclass, ifaceType);
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }
        }

        // --- dotnet interop ---

        private bool EmitDotnetCall(CallExpression call, string name)
        {
            var method = typeof(Ngo.Runtime.GoDotnet).GetMethod(name);
            if (method == null)
                throw new NotSupportedException($"Method GoDotnet.{name} not found (pkg={call.Function.PackageName}, argCount={call.Arguments.Count})");

            var methodParams = method.GetParameters();
            bool isVariadic = methodParams.Length > 0 &&
                methodParams[methodParams.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);

            if (!isVariadic)
            {
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    _body.EmitExpression(call.Arguments[i]);
                    var argType = _ctx.Mapper.Map(call.Arguments[i].Type);
                    var paramType = methodParams[i].ParameterType;
                    if (argType == typeof(GoString) && paramType == typeof(object))
                    {
                        EmitGoStringToNetString();
                        _ctx.IL.Emit(OpCodes.Box, typeof(string));
                    }
                    else if (argType != paramType)
                    {
                        if (paramType == typeof(object) && argType.IsValueType)
                        {
                            _ctx.IL.Emit(OpCodes.Box, argType);
                        }
                        else
                        {
                            EmitImplicitConv(argType, paramType);
                        }
                    }
                }
                _ctx.IL.Emit(OpCodes.Call, method);
                if (method.ReturnType == typeof(string))
                {
                    _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("FromNetString")!);
                }
                return true;
            }

            int fixedCount = methodParams.Length - 1;
            for (int i = 0; i < fixedCount && i < call.Arguments.Count; i++)
            {
                _body.EmitExpression(call.Arguments[i]);
                var argType = _ctx.Mapper.Map(call.Arguments[i].Type);
                var paramType = methodParams[i].ParameterType;
                if (argType != paramType)
                {
                    if (paramType == typeof(object) && argType.IsValueType)
                        _ctx.IL.Emit(OpCodes.Box, argType);
                    else
                        EmitImplicitConv(argType, paramType);
                }
            }

            int varArgCount = call.Arguments.Count - fixedCount;
            if (varArgCount < 0) varArgCount = 0;
            _ctx.IL.Emit(OpCodes.Ldc_I4, varArgCount);
            _ctx.IL.Emit(OpCodes.Newarr, typeof(object));
            for (int i = 0; i < varArgCount; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _body.EmitExpression(call.Arguments[fixedCount + i]);
                var argType = _ctx.Mapper.Map(call.Arguments[fixedCount + i].Type);
                if (argType == typeof(GoString))
                {
                    EmitGoStringToNetString();
                    _ctx.IL.Emit(OpCodes.Box, typeof(string));
                }
                else if (argType.IsValueType)
                {
                    _ctx.IL.Emit(OpCodes.Box, argType);
                }
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }

            _ctx.IL.Emit(OpCodes.Call, method);
            if (method.ReturnType == typeof(string))
            {
                _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("FromNetString")!);
            }
            return true;
        }

        // --- Static call dispatch ---

        public bool EmitStaticCall(CallExpression call, Type targetType, string methodName)
        {
            var paramTypes = new Type[call.Arguments.Count];
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                paramTypes[i] = _ctx.Mapper.Map(call.Arguments[i].Type);
            }

            MethodInfo? method = null;
            if (!EmitContext.HasAnyTypeBuilderPublic(paramTypes))
                method = targetType.GetMethod(methodName, paramTypes);

            if (method == null)
            {
                var candidates = targetType.GetMethods(BindingFlags.Public | BindingFlags.Static);

                foreach (var candidate in candidates)
                {
                    if (candidate.Name != methodName)
                    {
                        continue;
                    }
                    var candidateParams = candidate.GetParameters();
                    bool candidateVariadic = candidateParams.Length > 0
                        && candidateParams[candidateParams.Length - 1]
                            .IsDefined(typeof(ParamArrayAttribute), false);
                    if (!candidateVariadic && candidateParams.Length == call.Arguments.Count)
                    {
                        method = candidate;
                        break;
                    }
                }

                if (method == null)
                {
                    foreach (var candidate in candidates)
                    {
                        if (candidate.Name != methodName)
                        {
                            continue;
                        }
                        var candidateParams = candidate.GetParameters();
                        bool candidateVariadic = candidateParams.Length > 0
                            && candidateParams[candidateParams.Length - 1]
                                .IsDefined(typeof(ParamArrayAttribute), false);
                        if (candidateVariadic && call.Arguments.Count >= candidateParams.Length - 1)
                        {
                            method = candidate;
                            break;
                        }
                    }
                }
            }

            if (method == null)
            {
                throw new NotSupportedException(
                    $"Method {targetType.Name}.{methodName}({string.Join(", ", (object[])paramTypes)}) not found");
            }

            var methodParams = method.GetParameters();

            // Check if last param is params array (variadic C# method)
            bool hasParamsArray = methodParams.Length > 0
                && methodParams[methodParams.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);

            // Collect &localVar arguments for pointer writeback after call
            var writebacks = new List<(Symbol symbol, LocalSlot ptrLocal, Type innerType)>();

            void EmitArgWithWriteback(Expression arg, Type expectedParamType)
            {
                if (arg is AddressOfExpression addrArg
                    && addrArg.Operand is IdentifierExpression idArg
                    && _ctx.Locals.TryGetValue(idArg.Symbol, out _))
                {
                    var innerType = _ctx.Mapper.Map(addrArg.Operand.Type);
                    if (innerType.IsValueType)
                    {
                        var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                        var ctor = _ctx.Definitions.GetConstructor(ptrType, new[] { innerType });
                        var ptrLocal = _ctx.IL.DeclareLocal(ptrType);

                        _body.EmitExpression(addrArg.Operand);
                        _ctx.IL.Emit(OpCodes.Newobj, ctor);
                        _ctx.IL.Emit(OpCodes.Dup);
                        _ctx.IL.Emit(OpCodes.Stloc, ptrLocal);

                        writebacks.Add((idArg.Symbol, ptrLocal, innerType));
                        return;
                    }
                }
                _body.EmitExpression(arg);
            }

            if (hasParamsArray && call.Arguments.Count >= methodParams.Length - 1)
            {
                int fixedCount = methodParams.Length - 1;
                var arrayElemType = methodParams[fixedCount].ParameterType.GetElementType()!;

                // Emit fixed arguments
                for (int i = 0; i < fixedCount; i++)
                {
                    EmitArgWithWriteback(call.Arguments[i], methodParams[i].ParameterType);
                    EmitImplicitConv(paramTypes[i], methodParams[i].ParameterType);
                }

                // Pack variadic args into T[]
                int varCount = call.Arguments.Count - fixedCount;
                _ctx.IL.Emit(OpCodes.Ldc_I4, varCount);
                _ctx.IL.Emit(OpCodes.Newarr, arrayElemType);
                for (int i = 0; i < varCount; i++)
                {
                    _ctx.IL.Emit(OpCodes.Dup);
                    _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                    _body.EmitExpression(call.Arguments[fixedCount + i]);
                    var argClrType = _ctx.Mapper.Map(call.Arguments[fixedCount + i].Type);
                    if (argClrType.IsValueType && arrayElemType == typeof(object))
                    {
                        _ctx.IL.Emit(OpCodes.Box, argClrType);
                    }
                    if (arrayElemType.IsValueType)
                    {
                        _ctx.IL.Emit(OpCodes.Stelem, arrayElemType);
                    }
                    else
                    {
                        _ctx.IL.Emit(OpCodes.Stelem_Ref);
                    }
                }
            }
            else
            {
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    EmitArgWithWriteback(call.Arguments[i], methodParams[i].ParameterType);
                    // Interface parameter: generate wrapper if source is a value type
                    if (methodParams[i].ParameterType.IsInterface
                        && paramTypes[i].IsValueType
                        && i < call.Function.Parameters.Count
                        && call.Function.Parameters[i].Type is InterfaceTypeSymbol)
                    {
                        _body.EmitInterfaceWrapIfNeeded(
                            call.Arguments[i].Type,
                            call.Function.Parameters[i].Type,
                            methodParams[i].ParameterType);
                    }
                    else
                    {
                        EmitImplicitConv(paramTypes[i], methodParams[i].ParameterType);
                    }
                }
            }

            _ctx.IL.Emit(OpCodes.Call, method);

            // Writeback pointer modifications to local variables
            foreach (var (symbol, ptrLocal, innerType) in writebacks)
            {
                var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                var valueField = _ctx.Definitions.GetField(ptrType, "Value");
                _ctx.IL.Emit(OpCodes.Ldloc, ptrLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, valueField);
                _body.EmitStore(symbol);
            }

            return true;
        }

        private void EmitImplicitConv(Type source, Type target)
        {
            if (source == target) return;
            if (source == typeof(GoString) && target == typeof(string))
            {
                EmitGoStringToNetString();
            }
            else if (target == typeof(object) && source.IsValueType)
            {
                _ctx.IL.Emit(OpCodes.Box, source);
            }
            else if (source.IsValueType && target.IsInterface)
            {
                _ctx.IL.Emit(OpCodes.Box, source);
            }
            else if (target == typeof(byte)) { _ctx.IL.Emit(OpCodes.Conv_U1); }
            else if (target == typeof(short)) { _ctx.IL.Emit(OpCodes.Conv_I2); }
            else if (target == typeof(int)) { _ctx.IL.Emit(OpCodes.Conv_I4); }
            else if (target == typeof(long)) { _ctx.IL.Emit(OpCodes.Conv_I8); }
            else if (target == typeof(float)) { _ctx.IL.Emit(OpCodes.Conv_R4); }
            else if (target == typeof(double)) { _ctx.IL.Emit(OpCodes.Conv_R8); }
            else if (target.IsInterface && !target.IsAssignableFrom(source))
            {
                _ctx.IL.Emit(OpCodes.Castclass, target);
            }
        }

        // --- True builtins ---

        private bool EmitBuiltinLen(CallExpression call)
        {
            var arg = call.Arguments[0];
            var argType = arg.Type;

            // Resolve type parameter constraints (S ~[]E → []E)
            if (argType is TypeParameterSymbol tpLen && tpLen.Constraint.TypeElements.Count > 0)
            {
                argType = tpLen.Constraint.TypeElements[0].Type;
            }

            // Unwrap pointers: len(*[N]T) == N
            if (argType is PointerTypeSymbol ptrLen && ptrLen.ElementType != null)
            {
                argType = ptrLen.ElementType;
            }

            // Unwrap named types to find underlying slice/map/array
            var resolvedArgType = argType;
            while (resolvedArgType != null && resolvedArgType.GetType() == typeof(TypeSymbol)
                   && resolvedArgType.UnderlyingType != null)
            {
                resolvedArgType = resolvedArgType.UnderlyingType;
            }

            if (argType.TypeKind == TypeKind.String || argType.TypeKind == TypeKind.UntypedString)
            {
                _body.EmitExpression(arg);
                var tempStr = _ctx.IL.DeclareLocal(typeof(GoString));
                _ctx.IL.Emit(OpCodes.Stloc, tempStr);
                _ctx.IL.Emit(OpCodes.Ldloca, tempStr);
                _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetProperty("Len")!.GetGetMethod()!);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (resolvedArgType is SliceTypeSymbol || argType.TypeKind == TypeKind.Slice)
            {
                var sliceClrType = _ctx.Mapper.Map(resolvedArgType is SliceTypeSymbol ? resolvedArgType : argType);
                _body.EmitExpressionAddress(arg, sliceClrType);
                var lenGetter = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Len");
                _ctx.IL.Emit(OpCodes.Call, lenGetter);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (resolvedArgType is MapTypeSymbol || argType.TypeKind == TypeKind.Map)
            {
                _body.EmitExpression(arg);
                var mapClrType = _ctx.Mapper.Map(resolvedArgType is MapTypeSymbol ? resolvedArgType : argType);
                var lenGetter = _ctx.Definitions.GetPropertyGetter(mapClrType, "Len");
                _ctx.IL.Emit(OpCodes.Call, lenGetter);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (resolvedArgType is ArrayTypeSymbol || argType.TypeKind == TypeKind.Array)
            {
                var arrLenType = resolvedArgType as ArrayTypeSymbol
                    ?? argType.Resolved() as ArrayTypeSymbol;
                bool isInlineField = false;
                if (arrLenType != null && arg is Ast.SelectorExpression lenSel
                    && lenSel.Field?.Type is ArrayTypeSymbol
                    && _ctx.StructFields.TryGetValue(lenSel.Field, out var lenFb)
                    && !lenFb.FieldType.IsArray)
                {
                    isInlineField = true;
                }
                if (isInlineField)
                {
                    _ctx.IL.Emit(OpCodes.Ldc_I4, arrLenType!.Length);
                    _ctx.IL.Emit(OpCodes.Conv_I8);
                }
                else
                {
                    _body.EmitExpression(arg);
                    _ctx.IL.Emit(OpCodes.Ldlen);
                    _ctx.IL.Emit(OpCodes.Conv_I8);
                }
                return true;
            }

            if (resolvedArgType is ChannelTypeSymbol || argType.TypeKind == TypeKind.Channel)
            {
                _body.EmitExpression(arg);
                var chanClrType = _ctx.Mapper.Map(resolvedArgType is ChannelTypeSymbol ? resolvedArgType : argType);
                var lenGetter = _ctx.Definitions.GetPropertyGetter(chanClrType, "Length");
                _ctx.IL.Emit(OpCodes.Call, lenGetter);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (resolvedArgType is Symbols.InterfaceTypeSymbol || argType.TypeKind == TypeKind.Interface)
            {
                _body.EmitExpression(arg);
                if (_ctx.Mapper.Map(argType).IsValueType)
                {
                    _ctx.IL.Emit(OpCodes.Box, _ctx.Mapper.Map(argType));
                }
                var lenMethod = typeof(Ngo.Runtime.BuiltIn).GetMethod("Len", new[] { typeof(object) })!;
                _ctx.IL.Emit(OpCodes.Call, lenMethod);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            return false;
        }

        private bool EmitBuiltinCap(CallExpression call)
        {
            var arg = call.Arguments[0];
            var argType = arg.Type;

            // Resolve type parameter constraints (S ~[]E → []E)
            if (argType is TypeParameterSymbol tpCap && tpCap.Constraint.TypeElements.Count > 0)
            {
                argType = tpCap.Constraint.TypeElements[0].Type;
            }

            // Unwrap named types to find underlying slice
            var resolvedArgType = argType;
            while (resolvedArgType != null && resolvedArgType.GetType() == typeof(TypeSymbol)
                   && resolvedArgType.UnderlyingType != null)
            {
                resolvedArgType = resolvedArgType.UnderlyingType;
            }

            if (resolvedArgType is SliceTypeSymbol || argType.TypeKind == TypeKind.Slice)
            {
                var sliceClrType = _ctx.Mapper.Map(resolvedArgType is SliceTypeSymbol ? resolvedArgType : argType);
                _body.EmitExpressionAddress(arg, sliceClrType);
                var capGetter = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Cap");
                _ctx.IL.Emit(OpCodes.Call, capGetter);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (argType is ChannelTypeSymbol)
            {
                _body.EmitExpression(arg);
                var chanClrType = _ctx.Mapper.Map(argType);
                var capGetter = _ctx.Definitions.GetPropertyGetter(chanClrType, "Capacity");
                _ctx.IL.Emit(OpCodes.Call, capGetter);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (resolvedArgType is ArrayTypeSymbol || argType.TypeKind == TypeKind.Array)
            {
                var arrCapType = resolvedArgType as ArrayTypeSymbol
                    ?? argType.Resolved() as ArrayTypeSymbol;
                bool isInlineField = false;
                if (arrCapType != null && arg is Ast.SelectorExpression capSel
                    && capSel.Field?.Type is ArrayTypeSymbol
                    && _ctx.StructFields.TryGetValue(capSel.Field, out var capFb)
                    && !capFb.FieldType.IsArray)
                {
                    isInlineField = true;
                }
                if (isInlineField)
                {
                    _ctx.IL.Emit(OpCodes.Ldc_I4, arrCapType!.Length);
                    _ctx.IL.Emit(OpCodes.Conv_I8);
                }
                else
                {
                    _body.EmitExpression(arg);
                    _ctx.IL.Emit(OpCodes.Ldlen);
                    _ctx.IL.Emit(OpCodes.Conv_I8);
                }
                return true;
            }

            return false;
        }

        private static TypeSymbol ResolveTypeParamConstraint(TypeSymbol type)
        {
            if (type is TypeParameterSymbol tp && tp.Constraint.TypeElements.Count > 0)
            {
                return tp.Constraint.TypeElements[0].Type;
            }
            return type;
        }

        private bool EmitBuiltinAppend(CallExpression call)
        {
            var sliceArg = call.Arguments[0];
            var resolved = ResolveTypeParamConstraint(sliceArg.Type.Resolved());
            if (resolved is TypeParameterSymbol)
            {
                resolved = ResolveTypeParamConstraint(sliceArg.Type);
            }
            TypeSymbol elemType;
            if (resolved is SliceTypeSymbol sliceType)
            {
                elemType = sliceType.ElementType;
            }
            else
            {
                var underlying = resolved.UnderlyingType;
                while (underlying != null && !(underlying is SliceTypeSymbol))
                {
                    underlying = underlying.UnderlyingType;
                }
                elemType = (underlying as SliceTypeSymbol)?.ElementType ?? BuiltinTypes.EmptyInterface;
            }
            var elemClrType = _ctx.Mapper.Map(elemType);
            var appendSliceType = typeof(Slice<>).MakeGenericType(elemClrType);

            // Spread case: append(dst, src...) — append one slice to another
            // Detected by: 2 args where second arg is also a slice (or string for []byte)
            bool isSpread = call.Arguments.Count == 2
                && (call.Arguments[1].Type.Resolved() is SliceTypeSymbol
                || (elemType.TypeKind == TypeKind.Uint8
                    && (call.Arguments[1].Type.TypeKind == TypeKind.String || call.Arguments[1].Type.TypeKind == TypeKind.UntypedString)));
            if (isSpread)
            {
                _body.EmitExpression(sliceArg);
                _body.EmitExpression(call.Arguments[1]);
                var secondArgType = call.Arguments[1].Type;
                if (secondArgType.TypeKind == TypeKind.String || secondArgType.TypeKind == TypeKind.UntypedString)
                {
                    var toBytesMethod = typeof(GoString).GetMethod("ToBytes", new[] { typeof(GoString) })!;
                    _ctx.IL.Emit(OpCodes.Call, toBytesMethod);
                }
                var appendSliceMethod = _ctx.Definitions.GetMethod(appendSliceType, "Append", new[] { appendSliceType, appendSliceType });
                _ctx.IL.Emit(OpCodes.Call, appendSliceMethod);
                return true;
            }

            _body.EmitExpression(sliceArg);

            var elemCount = call.Arguments.Count - 1;
            _ctx.IL.Emit(OpCodes.Ldc_I4, elemCount);
            _ctx.IL.Emit(OpCodes.Newarr, elemClrType);

            for (int i = 0; i < elemCount; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _body.EmitExpression(call.Arguments[i + 1]);
                if (elemType is InterfaceTypeSymbol appendIfaceElem
                    && call.Arguments[i + 1].Type.TypeKind != TypeKind.Interface)
                {
                    _body.EmitInterfaceWrapIfNeeded(call.Arguments[i + 1].Type, appendIfaceElem, elemClrType);
                }
                else
                {
                    var argClrType = _ctx.Mapper.Map(call.Arguments[i + 1].Type);
                    if (argClrType.IsValueType && !elemClrType.IsValueType)
                    {
                        _ctx.IL.Emit(OpCodes.Box, argClrType);
                    }
                }
                _body.EmitStelem(elemClrType);
            }

            var appendMethod = _ctx.Definitions.GetMethod(appendSliceType, "Append", new[] { appendSliceType, elemClrType.MakeArrayType() });
            _ctx.IL.Emit(OpCodes.Call, appendMethod);
            return true;
        }

        private bool EmitBuiltinMake(CallExpression call)
        {
            var returnType = call.Function.ReturnType;

            // Resolve type parameter constraints (S ~[]E → []E)
            if (returnType is TypeParameterSymbol tpMake && tpMake.Constraint.TypeElements.Count > 0)
            {
                returnType = tpMake.Constraint.TypeElements[0].Type;
            }

            returnType = returnType.Resolved();

            if (returnType is SliceTypeSymbol sliceType)
            {
                var elemClrType = _ctx.Mapper.Map(sliceType.ElementType);
                var sliceClrType = _ctx.Mapper.Map(sliceType);

                if (call.Arguments.Count >= 1)
                {
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                }

                if (call.Arguments.Count >= 2)
                {
                    _body.EmitExpression(call.Arguments[1]);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldc_I4_M1);
                }

                var makeMethod = _ctx.Definitions.GetMethod(sliceClrType, "Make", new[] { typeof(int), typeof(int) });
                _ctx.IL.Emit(OpCodes.Call, makeMethod);
                return true;
            }

            if (returnType is MapTypeSymbol mapType)
            {
                var mapClrType = _ctx.Mapper.Map(mapType);
                var ctor = _ctx.Definitions.GetConstructor(mapClrType, Type.EmptyTypes);
                _ctx.IL.Emit(OpCodes.Newobj, ctor);
                return true;
            }

            if (returnType is ChannelTypeSymbol chanType)
            {
                var chanClrType = _ctx.Mapper.Map(chanType);

                if (call.Arguments.Count >= 1)
                {
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    var ctor = _ctx.Definitions.GetConstructor(chanClrType, new[] { typeof(int) });
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                }
                else
                {
                    var ctor = _ctx.Definitions.GetConstructor(chanClrType, Type.EmptyTypes);
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                }

                return true;
            }

            return false;
        }

        private bool EmitBuiltinDelete(CallExpression call)
        {
            _body.EmitExpression(call.Arguments[0]);
            _body.EmitExpression(call.Arguments[1]);
            var mapClrType = _ctx.Mapper.Map(call.Arguments[0].Type);
            var deleteMethod = _ctx.Definitions.GetMethod(mapClrType, "Delete");
            _ctx.IL.Emit(OpCodes.Call, deleteMethod);
            return true;
        }

        private bool EmitBuiltinCopy(CallExpression call)
        {
            var resolvedCopy = ResolveTypeParamConstraint(call.Arguments[0].Type.Resolved());
            if (resolvedCopy is TypeParameterSymbol)
            {
                resolvedCopy = ResolveTypeParamConstraint(call.Arguments[0].Type);
            }
            TypeSymbol copyElemType;
            if (resolvedCopy is SliceTypeSymbol copySlice)
            {
                copyElemType = copySlice.ElementType;
            }
            else
            {
                var underlying = resolvedCopy.UnderlyingType;
                while (underlying != null && !(underlying is SliceTypeSymbol))
                {
                    underlying = underlying.UnderlyingType;
                }
                copyElemType = (underlying as SliceTypeSymbol)?.ElementType ?? BuiltinTypes.EmptyInterface;
            }

            var secondArgType = call.Arguments[1].Type;
            bool isStringSource = secondArgType.TypeKind == TypeKind.String
                || secondArgType.TypeKind == TypeKind.UntypedString;

            // Check if destination is a slice of an InlineArray struct field
            bool isInlineArrayDest = false;
            SelectorExpression? inlineSel = null;
            ArrayTypeSymbol? inlineArrType = null;
            Builder.IFieldBuilder? inlineFb = null;
            if (call.Arguments[0] is SliceExpression destSlice
                && destSlice.Operand is SelectorExpression sel
                && sel.Field?.Type is ArrayTypeSymbol arrT)
            {
                if (_ctx.StructFields.TryGetValue(sel.Field, out var fb) && !fb.FieldType.IsArray)
                {
                    isInlineArrayDest = true;
                    inlineSel = sel;
                    inlineArrType = arrT;
                    inlineFb = fb;
                }
                else
                {
                    // Name-based fallback
                    foreach (var kvp in _ctx.StructFields)
                    {
                        if (kvp.Key.Name == sel.Field.Name && kvp.Key.Type is ArrayTypeSymbol
                            && !kvp.Value.FieldType.IsArray)
                        {
                            isInlineArrayDest = true;
                            inlineSel = sel;
                            inlineArrType = arrT;
                            inlineFb = kvp.Value;
                            break;
                        }
                    }
                }
            }

            if (isInlineArrayDest && isStringSource && copyElemType == BuiltinTypes.Byte)
            {
                // copy(inlineArray[:], string) → convert string to bytes, write to InlineArray via Span
                _body.EmitInlineArrayFieldAddress(inlineSel!);
                _ctx.IL.Emit(OpCodes.Ldc_I4, inlineArrType!.Length);
                var bufferType = inlineFb!.FieldType;
                var spanMethod = typeof(BuiltIn).GetMethod("InlineArrayAsSpan")!
                    .MakeGenericMethod(bufferType, typeof(byte));
                _ctx.IL.Emit(OpCodes.Call, spanMethod);

                // Convert string to byte[] then copy to span
                _body.EmitExpression(call.Arguments[1]);
                var toBytesMethod = typeof(GoString).GetMethod("ToBytes", new[] { typeof(GoString) })!;
                _ctx.IL.Emit(OpCodes.Call, toBytesMethod);

                // Span<byte>.CopyFrom(Slice<byte>) — use Slice.AsSpan + CopyTo
                // Simpler: use BuiltIn.Copy(Slice<byte>, string) on a span-backed slice
                // Actually simplest: convert span to Slice, call Copy
                // Let's just convert the InlineArray span to a Slice and use the existing copy
                var spanLocal = _ctx.IL.DeclareLocal(typeof(Span<byte>));
                var srcLocal = _ctx.IL.DeclareLocal(typeof(Slice<byte>));
                _ctx.IL.Emit(OpCodes.Stloc, srcLocal);
                _ctx.IL.Emit(OpCodes.Stloc, spanLocal);

                // Create Slice from span's underlying memory — but we need a T[]
                // The span IS backed by the InlineArray. We can't create a Slice from it directly.
                // Instead: iterate and use InlineArraySet
                _ctx.IL.Emit(OpCodes.Ldloca, srcLocal);
                var srcLenProp = typeof(Slice<byte>).GetProperty("Len")!.GetGetMethod()!;
                _ctx.IL.Emit(OpCodes.Call, srcLenProp);
                var lenLocal = _ctx.IL.DeclareLocal(typeof(int));
                _ctx.IL.Emit(OpCodes.Stloc, lenLocal);

                // for (int i = 0; i < len; i++) { InlineArraySet(ref buf, i, src[i]); }
                var loopStart = _ctx.IL.DefineLabel();
                var loopEnd = _ctx.IL.DefineLabel();
                var iLocal = _ctx.IL.DeclareLocal(typeof(int));
                _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                _ctx.IL.Emit(OpCodes.Stloc, iLocal);
                _ctx.IL.MarkLabel(loopStart);
                _ctx.IL.Emit(OpCodes.Ldloc, iLocal);
                _ctx.IL.Emit(OpCodes.Ldloc, lenLocal);
                _ctx.IL.Emit(OpCodes.Bge, loopEnd);

                // InlineArraySet(ref buf, i, src[i])
                _body.EmitInlineArrayFieldAddress(inlineSel!);
                _ctx.IL.Emit(OpCodes.Ldloc, iLocal);
                _ctx.IL.Emit(OpCodes.Ldloca, srcLocal);
                _ctx.IL.Emit(OpCodes.Ldloc, iLocal);
                var indexer = typeof(Slice<byte>).GetProperty("Item")!.GetGetMethod()!;
                _ctx.IL.Emit(OpCodes.Call, indexer);
                _ctx.IL.Emit(OpCodes.Ldobj, typeof(byte));
                var setMethod = typeof(BuiltIn).GetMethod("InlineArraySet")!
                    .MakeGenericMethod(bufferType, typeof(byte));
                _ctx.IL.Emit(OpCodes.Call, setMethod);

                _ctx.IL.Emit(OpCodes.Ldloc, iLocal);
                _ctx.IL.Emit(OpCodes.Ldc_I4_1);
                _ctx.IL.Emit(OpCodes.Add);
                _ctx.IL.Emit(OpCodes.Stloc, iLocal);
                _ctx.IL.Emit(OpCodes.Br, loopStart);
                _ctx.IL.MarkLabel(loopEnd);

                _ctx.IL.Emit(OpCodes.Ldloc, lenLocal);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (isStringSource && copyElemType == BuiltinTypes.Byte)
            {
                _body.EmitExpression(call.Arguments[0]);
                _body.EmitExpression(call.Arguments[1]);

                var copyMethod = typeof(BuiltIn).GetMethod("Copy",
                    new[] { typeof(Slice<byte>), typeof(GoString) });
                _ctx.IL.Emit(OpCodes.Call, copyMethod!);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            var elemClrType = _ctx.Mapper.Map(copyElemType);

            _body.EmitExpression(call.Arguments[0]);
            _body.EmitExpression(call.Arguments[1]);

            var copySliceType = typeof(Slice<>).MakeGenericType(elemClrType);
            var copyMethod2 = _ctx.Definitions.GetMethod(copySliceType, "Copy", new[] { copySliceType, copySliceType });
            _ctx.IL.Emit(OpCodes.Call, copyMethod2);
            _ctx.IL.Emit(OpCodes.Conv_I8);
            return true;
        }

        private void EmitInlineArrayWriteBack(CallExpression call, TypeSymbol elemType)
        {
            // Not used — write-back handled at call site
        }

        private bool EmitBuiltinClose(CallExpression call)
        {
            _body.EmitExpression(call.Arguments[0]);
            var chanClrType = _ctx.Mapper.Map(call.Arguments[0].Type);
            var closeMethod = _ctx.Definitions.GetMethod(chanClrType, "Close");
            _ctx.IL.Emit(OpCodes.Call, closeMethod);
            return true;
        }

        private bool EmitBuiltinNew(CallExpression call)
        {
            if (call.Type is PointerTypeSymbol ptrType && ptrType.ElementType != null)
            {
                var elemClrType = _ctx.Mapper.Map(ptrType.ElementType);
                // Ensure we use runtime type, not TypeBuilder, for Ptr<T> creation
                if (elemClrType is System.Reflection.Emit.TypeBuilder tb && tb.IsCreated())
                {
                    elemClrType = tb.CreateType()!;
                }
                if (elemClrType == null)
                {
                    // Name-based fallback for source-compiled dependency types
                    // whose symbols differ from the registered instances
                    foreach (var kvp in _ctx.StructTypes)
                    {
                        if (kvp.Key.Name == ptrType.ElementType.Name)
                        {
                            elemClrType = kvp.Value.AsType();
                            break;
                        }
                    }
                    if (elemClrType == null)
                    {
                        throw new InvalidOperationException(
                            $"Builtin new(): TypeMapper returned null for element type '{ptrType.ElementType.Name}'");
                    }
                }

                if (IsRuntimeReferenceType(elemClrType))
                {
                    var ctor = elemClrType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        _ctx.IL.Emit(OpCodes.Newobj, ctor);
                    }
                    else
                    {
                        _ctx.IL.Emit(OpCodes.Ldnull);
                    }
                }
                else
                {
                    var ptrClrType = typeof(Ptr<>).MakeGenericType(elemClrType);
                    var ctor = _ctx.Definitions.GetConstructor(ptrClrType, Type.EmptyTypes);
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                    if (IsRuntimeReferenceType(elemClrType))
                    {
                        EmitReferenceFieldInitialization(ptrClrType, elemClrType);
                    }
                }
            }
            else if (call.Arguments.Count > 0)
            {
                // new(T) where T is the type argument — get the type from the argument
                var typeArg = call.Arguments[0];
                throw new InvalidOperationException(
                    $"Builtin new(): unsupported call type {call.Type?.GetType().Name ?? "null"} (name={call.Type?.Name})");
            }
            return true;
        }

        private bool EmitBuiltinComplex(CallExpression call)
        {
            _body.EmitExpression(call.Arguments[0]);
            _ctx.IL.Emit(OpCodes.Conv_R8);
            _body.EmitExpression(call.Arguments[1]);
            _ctx.IL.Emit(OpCodes.Conv_R8);
            var ctor = typeof(System.Numerics.Complex).GetConstructor(
                new[] { typeof(double), typeof(double) })!;
            _ctx.IL.Emit(OpCodes.Newobj, ctor);
            return true;
        }

        private bool EmitBuiltinReal(CallExpression call)
        {
            _body.EmitExpression(call.Arguments[0]);
            var complexType = typeof(System.Numerics.Complex);
            var local = _ctx.IL.DeclareLocal(complexType);
            _ctx.IL.Emit(OpCodes.Stloc, local);
            _ctx.IL.Emit(OpCodes.Ldloca, local);
            var realProp = complexType.GetProperty("Real")!;
            _ctx.IL.Emit(OpCodes.Call, realProp.GetGetMethod()!);
            return true;
        }

        private bool EmitBuiltinImag(CallExpression call)
        {
            _body.EmitExpression(call.Arguments[0]);
            var complexType = typeof(System.Numerics.Complex);
            var local = _ctx.IL.DeclareLocal(complexType);
            _ctx.IL.Emit(OpCodes.Stloc, local);
            _ctx.IL.Emit(OpCodes.Ldloca, local);
            var imagProp = complexType.GetProperty("Imaginary")!;
            _ctx.IL.Emit(OpCodes.Call, imagProp.GetGetMethod()!);
            return true;
        }

        private bool EmitMinMax(CallExpression call, bool isMin)
        {
            var argType = call.Arguments[0].Type;
            var clrType = _ctx.Mapper.Map(argType);
            bool isString = clrType == typeof(GoString);

            _body.EmitExpression(call.Arguments[0]);

            for (int i = 1; i < call.Arguments.Count; i++)
            {
                var resultLocal = _ctx.IL.DeclareLocal(clrType);
                _ctx.IL.Emit(OpCodes.Stloc, resultLocal);
                _body.EmitExpression(call.Arguments[i]);
                var candidateLocal = _ctx.IL.DeclareLocal(clrType);
                _ctx.IL.Emit(OpCodes.Stloc, candidateLocal);

                var keepCurrent = _ctx.IL.DefineLabel();
                var done = _ctx.IL.DefineLabel();

                if (isString)
                {
                    _ctx.IL.Emit(OpCodes.Ldloc, candidateLocal);
                    _ctx.IL.Emit(OpCodes.Ldloc, resultLocal);
                    // CompareTo is instance method on GoString
                    var tempCand = _ctx.IL.DeclareLocal(typeof(GoString));
                    var tempRes = _ctx.IL.DeclareLocal(typeof(GoString));
                    _ctx.IL.Emit(OpCodes.Stloc, tempRes);
                    _ctx.IL.Emit(OpCodes.Stloc, tempCand);
                    _ctx.IL.Emit(OpCodes.Ldloca, tempCand);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempRes);
                    _ctx.IL.Emit(OpCodes.Call,
                        typeof(GoString).GetMethod("CompareTo", new[] { typeof(GoString) })!);
                    _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                    if (isMin)
                        _ctx.IL.Emit(OpCodes.Bge, keepCurrent);
                    else
                        _ctx.IL.Emit(OpCodes.Ble, keepCurrent);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldloc, candidateLocal);
                    _ctx.IL.Emit(OpCodes.Ldloc, resultLocal);
                    if (isMin)
                        _ctx.IL.Emit(OpCodes.Bge, keepCurrent);
                    else
                        _ctx.IL.Emit(OpCodes.Ble, keepCurrent);
                }

                _ctx.IL.Emit(OpCodes.Ldloc, candidateLocal);
                _ctx.IL.Emit(OpCodes.Br, done);

                _ctx.IL.MarkLabel(keepCurrent);
                _ctx.IL.Emit(OpCodes.Ldloc, resultLocal);

                _ctx.IL.MarkLabel(done);
            }

            return true;
        }

        private bool EmitClear(CallExpression call)
        {
            var arg = call.Arguments[0];
            var argType = arg.Type;

            // Resolve type parameter constraints (S ~[]E → []E)
            if (argType is TypeParameterSymbol tpClear && tpClear.Constraint.TypeElements.Count > 0)
            {
                argType = tpClear.Constraint.TypeElements[0].Type;
            }

            var resolvedArgType = argType;
            while (resolvedArgType != null && resolvedArgType.GetType() == typeof(TypeSymbol)
                   && resolvedArgType.UnderlyingType != null)
            {
                resolvedArgType = resolvedArgType.UnderlyingType;
            }

            if (resolvedArgType is MapTypeSymbol || argType.TypeKind == TypeKind.Map)
            {
                _body.EmitExpression(arg);
                var mapClrType = _ctx.Mapper.Map(resolvedArgType is MapTypeSymbol ? resolvedArgType : argType);
                var clearMethod = _ctx.Definitions.GetMethod(mapClrType, "Clear");
                if (clearMethod != null)
                {
                    _ctx.IL.Emit(OpCodes.Call, clearMethod);
                }
                return true;
            }

            if (resolvedArgType is SliceTypeSymbol sliceType)
            {
                var elemClrType = _ctx.Mapper.Map(sliceType.ElementType);
                var sliceClrType = _ctx.Mapper.Map(sliceType);

                _body.EmitExpression(arg);
                var sliceLocal = _ctx.IL.DeclareLocal(sliceClrType);
                _ctx.IL.Emit(OpCodes.Stloc, sliceLocal);

                var indexLocal = _ctx.IL.DeclareLocal(typeof(int));
                _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                _ctx.IL.Emit(OpCodes.Stloc, indexLocal);

                var loopStart = _ctx.IL.DefineLabel();
                var loopEnd = _ctx.IL.DefineLabel();

                _ctx.IL.MarkLabel(loopStart);
                _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
                _ctx.IL.Emit(OpCodes.Ldloca, sliceLocal);
                _ctx.IL.Emit(OpCodes.Call, _ctx.Definitions.GetPropertyGetter(sliceClrType, "Len"));
                _ctx.IL.Emit(OpCodes.Bge, loopEnd);

                _ctx.IL.Emit(OpCodes.Ldloca, sliceLocal);
                _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                var indexerGetter = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Item");
                _ctx.IL.Emit(OpCodes.Call, indexerGetter);
                _ctx.IL.Emit(OpCodes.Initobj, elemClrType);

                _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
                _ctx.IL.Emit(OpCodes.Ldc_I4_1);
                _ctx.IL.Emit(OpCodes.Add);
                _ctx.IL.Emit(OpCodes.Stloc, indexLocal);
                _ctx.IL.Emit(OpCodes.Br, loopStart);

                _ctx.IL.MarkLabel(loopEnd);
                return true;
            }

            return false;
        }

        // --- Print/format helpers ---

        private void EmitBuiltinPrintArgs(CallExpression call)
        {
            _ctx.IL.Emit(OpCodes.Ldc_I4, call.Arguments.Count);
            _ctx.IL.Emit(OpCodes.Newarr, typeof(object));

            for (int i = 0; i < call.Arguments.Count; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _body.EmitExpression(call.Arguments[i]);
                _body.EmitBoxIfNeeded(call.Arguments[i].Type);
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitFmtFormatCall(CallExpression call, string methodName,
            Type? targetType = null)
        {
            targetType ??= typeof(Ngo.Runtime.Fmt.Package);

            if (call.Arguments.Count > 0)
            {
                _body.EmitExpression(call.Arguments[0]);
                EmitGoStringToNetString();
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Ldstr, "");
            }

            int varArgCount = call.Arguments.Count - 1;
            if (varArgCount < 0) varArgCount = 0;
            _ctx.IL.Emit(OpCodes.Ldc_I4, varArgCount);
            _ctx.IL.Emit(OpCodes.Newarr, typeof(object));

            for (int i = 1; i < call.Arguments.Count; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i - 1);
                _body.EmitExpression(call.Arguments[i]);
                _body.EmitBoxIfNeeded(call.Arguments[i].Type);
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }

            _ctx.IL.Emit(OpCodes.Call,
                targetType.GetMethod(methodName, new[] { typeof(string), typeof(object[]) })!);
        }

        private void EmitFmtFormatCallFrom(CallExpression call, string methodName,
            Type targetType, int formatArgIndex)
        {
            if (call.Arguments.Count > formatArgIndex)
            {
                _body.EmitExpression(call.Arguments[formatArgIndex]);
                EmitGoStringToNetString();
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Ldstr, "");
            }

            int firstVarArg = formatArgIndex + 1;
            int varArgCount = call.Arguments.Count - firstVarArg;
            if (varArgCount < 0) varArgCount = 0;
            _ctx.IL.Emit(OpCodes.Ldc_I4, varArgCount);
            _ctx.IL.Emit(OpCodes.Newarr, typeof(object));

            for (int i = firstVarArg; i < call.Arguments.Count; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i - firstVarArg);
                _body.EmitExpression(call.Arguments[i]);
                _body.EmitBoxIfNeeded(call.Arguments[i].Type);
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }

            _ctx.IL.Emit(OpCodes.Call,
                targetType.GetMethod(methodName,
                    new[] { typeof(IGoWriter), typeof(string), typeof(object[]) })!);
        }

        private void EmitFmtWriterPrintArgs(CallExpression call, int startIndex)
        {
            int count = call.Arguments.Count - startIndex;
            if (count < 0) count = 0;
            _ctx.IL.Emit(OpCodes.Ldc_I4, count);
            _ctx.IL.Emit(OpCodes.Newarr, typeof(object));

            for (int i = startIndex; i < call.Arguments.Count; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i - startIndex);
                _body.EmitExpression(call.Arguments[i]);
                _body.EmitBoxIfNeeded(call.Arguments[i].Type);
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitGoStringToNetString()
        {
            var tempStr = _ctx.IL.DeclareLocal(typeof(GoString));
            _ctx.IL.Emit(OpCodes.Stloc, tempStr);
            _ctx.IL.Emit(OpCodes.Ldloca, tempStr);
            _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("ToNetString")!);
        }

        /// <summary>
        /// After creating a Ptr&lt;T&gt; for a value type T via new(T), initializes
        /// any reference-type fields that need non-null zero values. Go structs
        /// mapped to C# classes (e.g. sync.Mutex, sync.Pool) default to null in
        /// a CLR value type but should be initialized to new T() for correct Go semantics.
        /// Expects the Ptr&lt;T&gt; on top of the stack and leaves it there.
        /// </summary>
        private void EmitReferenceFieldInitialization(Type ptrClrType, Type elemClrType)
        {
            var fields = elemClrType.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            var valueField = _ctx.Definitions.GetField(ptrClrType, "Value");

            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                if (fieldType.IsValueType
                    || fieldType == typeof(object) || fieldType.IsInterface
                    || fieldType.IsAbstract || fieldType.IsArray)
                {
                    continue;
                }

                var defaultCtor = fieldType.GetConstructor(Type.EmptyTypes);
                if (defaultCtor == null)
                {
                    continue;
                }

                // Stack: Ptr<T>
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldflda, valueField);
                _ctx.IL.Emit(OpCodes.Newobj, defaultCtor);
                _ctx.IL.Emit(OpCodes.Stfld, field);
            }
        }

        private static bool IsRuntimeReferenceType(Type type)
        {
            if (type.IsValueType)
            {
                return false;
            }
            return !ContainsNonRuntimeType(type);
        }

        private static bool ContainsNonRuntimeType(Type type)
        {
            if (EmitContext.IsNonRuntimeType(type))
            {
                return true;
            }
            if (type.IsGenericType && EmitContext.HasTypeBuilderArgs(type))
            {
                return true;
            }
            if (type.HasElementType)
            {
                var elementType = type.GetElementType();
                if (elementType != null && ContainsNonRuntimeType(elementType))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

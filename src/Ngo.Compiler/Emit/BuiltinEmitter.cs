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
                    try { return EmitStaticCall(call, targetType, name); }
                    catch { /* method not found — fall through to builtins */ }
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
                    // Unqualified function name — try annotation lookup by name alone
                    // This handles cases like fmt.Println imported as just "Println"
                    if (pkg == null)
                    {
                        foreach (var kv in _packageTypes)
                        {
                            var method = kv.Value.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
                            if (method != null)
                            {
                                try { return EmitStaticCall(call, kv.Value, name); }
                                catch { continue; }
                            }
                        }
                    }
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
                        EmitVariadicStringArray(call, 0);
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Ngo.Runtime.Filepath.Package).GetMethod("Join")!);
                        return true;
                    }
                    return false;

                case "log":
                    return TryEmitLogSpecial(call, name);

                case "strings":
                    return TryEmitStringsSpecial(call, name);

                case "dotnet":
                    return EmitDotnetCall(call, name);

                default:
                    return false;
            }
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
                case "NewScanner":
                    return EmitStaticCall(call, typeof(Ngo.Runtime.Bufio.Package), "NewScanner");
                case "NewReader":
                    return EmitStaticCall(call, typeof(Ngo.Runtime.Bufio.Package), "NewReader");
                case "NewWriter":
                    return EmitStaticCall(call, typeof(Ngo.Runtime.Bufio.Package), "NewWriter");
                default:
                    return false;
            }
        }

        private bool TryEmitCsvSpecial(CallExpression call, string name)
        {
            switch (name)
            {
                case "NewReader":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Call,
                        typeof(Ngo.Runtime.Csv.Package).GetMethod("NewReader",
                            new[] { typeof(object) })!);
                    return true;
                case "NewWriter":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Call,
                        typeof(Ngo.Runtime.Csv.Package).GetMethod("NewWriter",
                            new[] { typeof(object) })!);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryEmitLogSpecial(CallExpression call, string name)
        {
            var logType = typeof(Ngo.Runtime.Log.Package);
            switch (name)
            {
                case "Println":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, logType.GetMethod("Println")!);
                    return true;
                case "Print":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, logType.GetMethod("Print")!);
                    return true;
                case "Printf":
                    EmitFmtFormatCall(call, "Printf", logType);
                    return true;
                case "Fatal":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, logType.GetMethod("Fatal")!);
                    return true;
                case "Fatalf":
                    EmitFmtFormatCall(call, "Fatalf", logType);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryEmitStringsSpecial(CallExpression call, string name)
        {
            switch (name)
            {
                case "NewReader":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Newobj,
                        typeof(Ngo.Runtime.Io.StringReader).GetConstructor(new[] { typeof(string) })!);
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
                _ctx.IL.Emit(OpCodes.Castclass, ifaceType);
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }
        }

        // --- dotnet interop ---

        private bool EmitDotnetCall(CallExpression call, string name)
        {
            var method = typeof(Ngo.Runtime.GoDotnet).GetMethod(name);
            if (method == null)
                throw new NotSupportedException($"dotnet.{name} not found");

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
                    if (argType != paramType)
                    {
                        if (paramType == typeof(object) && argType.IsValueType)
                            _ctx.IL.Emit(OpCodes.Box, argType);
                        else
                            EmitImplicitConv(argType, paramType);
                    }
                }
                _ctx.IL.Emit(OpCodes.Call, method);
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
                if (argType.IsValueType)
                    _ctx.IL.Emit(OpCodes.Box, argType);
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }

            _ctx.IL.Emit(OpCodes.Call, method);
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
                foreach (var c in candidates)
                {
                    if (c.Name == methodName && c.GetParameters().Length == call.Arguments.Count)
                    {
                        method = c;
                        break;
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
            var writebacks = new List<(Symbol symbol, LocalBuilder ptrLocal, Type innerType)>();

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
                        var ctor = EmitContext.GetConstructorSafe(ptrType, new[] { innerType });
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
                    EmitImplicitConv(paramTypes[i], methodParams[i].ParameterType);
                }
            }

            _ctx.IL.Emit(OpCodes.Call, method);

            // Writeback pointer modifications to local variables
            foreach (var (symbol, ptrLocal, innerType) in writebacks)
            {
                var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                var valueField = EmitContext.GetFieldSafe(ptrType, "Value");
                _ctx.IL.Emit(OpCodes.Ldloc, ptrLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, valueField);
                _body.EmitStore(symbol);
            }

            return true;
        }

        private void EmitImplicitConv(Type source, Type target)
        {
            if (source == target) return;
            if (target == typeof(object) && source.IsValueType)
                _ctx.IL.Emit(OpCodes.Box, source);
            else if (target == typeof(byte)) _ctx.IL.Emit(OpCodes.Conv_U1);
            else if (target == typeof(short)) _ctx.IL.Emit(OpCodes.Conv_I2);
            else if (target == typeof(int)) _ctx.IL.Emit(OpCodes.Conv_I4);
            else if (target == typeof(long)) _ctx.IL.Emit(OpCodes.Conv_I8);
            else if (target == typeof(float)) _ctx.IL.Emit(OpCodes.Conv_R4);
            else if (target == typeof(double)) _ctx.IL.Emit(OpCodes.Conv_R8);
            else if (target.IsInterface && !target.IsAssignableFrom(source))
                _ctx.IL.Emit(OpCodes.Castclass, target);
        }

        // --- True builtins ---

        private bool EmitBuiltinLen(CallExpression call)
        {
            var arg = call.Arguments[0];
            var argType = arg.Type;

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
                _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("Len")!);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (resolvedArgType is SliceTypeSymbol || argType.TypeKind == TypeKind.Slice)
            {
                var sliceClrType = _ctx.Mapper.Map(resolvedArgType is SliceTypeSymbol ? resolvedArgType : argType);
                _body.EmitExpressionAddress(arg, sliceClrType);
                var lenGetter = EmitContext.GetPropertyGetterSafe(sliceClrType, "Len");
                _ctx.IL.Emit(OpCodes.Call, lenGetter);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (resolvedArgType is MapTypeSymbol || argType.TypeKind == TypeKind.Map)
            {
                _body.EmitExpression(arg);
                var mapClrType = _ctx.Mapper.Map(resolvedArgType is MapTypeSymbol ? resolvedArgType : argType);
                var lenGetter = EmitContext.GetPropertyGetterSafe(mapClrType, "Len");
                _ctx.IL.Emit(OpCodes.Call, lenGetter);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (resolvedArgType is ArrayTypeSymbol || argType.TypeKind == TypeKind.Array)
            {
                _body.EmitExpression(arg);
                _ctx.IL.Emit(OpCodes.Ldlen);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            return false;
        }

        private bool EmitBuiltinCap(CallExpression call)
        {
            var arg = call.Arguments[0];

            if (arg.Type is SliceTypeSymbol)
            {
                var sliceClrType = _ctx.Mapper.Map(arg.Type);
                _body.EmitExpressionAddress(arg, sliceClrType);
                var capGetter = EmitContext.GetPropertyGetterSafe(sliceClrType, "Cap");
                _ctx.IL.Emit(OpCodes.Call, capGetter);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (arg.Type is ChannelTypeSymbol)
            {
                _body.EmitExpression(arg);
                var chanClrType = _ctx.Mapper.Map(arg.Type);
                var capGetter = EmitContext.GetPropertyGetterSafe(chanClrType, "Capacity");
                _ctx.IL.Emit(OpCodes.Call, capGetter);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            return false;
        }

        private bool EmitBuiltinAppend(CallExpression call)
        {
            var sliceArg = call.Arguments[0];
            var sliceType = (SliceTypeSymbol)sliceArg.Type;
            var elemClrType = _ctx.Mapper.Map(sliceType.ElementType);
            var sliceClrType = _ctx.Mapper.Map(sliceType);

            _body.EmitExpression(sliceArg);

            var elemCount = call.Arguments.Count - 1;
            _ctx.IL.Emit(OpCodes.Ldc_I4, elemCount);
            _ctx.IL.Emit(OpCodes.Newarr, elemClrType);

            for (int i = 0; i < elemCount; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _body.EmitExpression(call.Arguments[i + 1]);
                if (sliceType.ElementType is InterfaceTypeSymbol appendIfaceElem
                    && call.Arguments[i + 1].Type.TypeKind != TypeKind.Interface)
                {
                    _body.EmitInterfaceWrapIfNeeded(call.Arguments[i + 1].Type, appendIfaceElem, elemClrType);
                }
                else
                {
                    var argClrType = _ctx.Mapper.Map(call.Arguments[i + 1].Type);
                    if (argClrType.IsValueType && !elemClrType.IsValueType)
                        _ctx.IL.Emit(OpCodes.Box, argClrType);
                }
                _body.EmitStelem(elemClrType);
            }

            var appendSliceType = typeof(Slice<>).MakeGenericType(elemClrType);
            var appendMethod = EmitContext.GetMethodSafe(appendSliceType, "Append", new[] { sliceClrType, elemClrType.MakeArrayType() });
            _ctx.IL.Emit(OpCodes.Call, appendMethod);
            return true;
        }

        private bool EmitBuiltinMake(CallExpression call)
        {
            var returnType = call.Function.ReturnType;

            // Unwrap named types to find underlying slice/map/channel
            var resolved = returnType;
            while (resolved != null && resolved.GetType() == typeof(TypeSymbol)
                   && resolved.UnderlyingType != null)
            {
                resolved = resolved.UnderlyingType;
            }
            if (resolved != returnType && resolved != null)
            {
                returnType = resolved;
            }

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

                var makeMethod = EmitContext.GetMethodSafe(sliceClrType, "Make", new[] { typeof(int), typeof(int) });
                _ctx.IL.Emit(OpCodes.Call, makeMethod);
                return true;
            }

            if (returnType is MapTypeSymbol mapType)
            {
                var mapClrType = _ctx.Mapper.Map(mapType);
                var ctor = EmitContext.GetConstructorSafe(mapClrType, Type.EmptyTypes);
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
                    var ctor = EmitContext.GetConstructorSafe(chanClrType, new[] { typeof(int) });
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                }
                else
                {
                    var ctor = EmitContext.GetConstructorSafe(chanClrType, Type.EmptyTypes);
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
            var deleteMethod = EmitContext.GetMethodSafe(mapClrType, "Delete");
            _ctx.IL.Emit(OpCodes.Call, deleteMethod);
            return true;
        }

        private bool EmitBuiltinCopy(CallExpression call)
        {
            var sliceType = (SliceTypeSymbol)call.Arguments[0].Type;
            var elemClrType = _ctx.Mapper.Map(sliceType.ElementType);
            var sliceClrType = _ctx.Mapper.Map(sliceType);

            _body.EmitExpression(call.Arguments[0]);
            _body.EmitExpression(call.Arguments[1]);

            var copySliceType = typeof(Slice<>).MakeGenericType(elemClrType);
            var copyMethod = EmitContext.GetMethodSafe(copySliceType, "Copy", new[] { sliceClrType, sliceClrType });
            _ctx.IL.Emit(OpCodes.Call, copyMethod);
            _ctx.IL.Emit(OpCodes.Conv_I8);
            return true;
        }

        private bool EmitBuiltinClose(CallExpression call)
        {
            _body.EmitExpression(call.Arguments[0]);
            var chanClrType = _ctx.Mapper.Map(call.Arguments[0].Type);
            var closeMethod = EmitContext.GetMethodSafe(chanClrType, "Close");
            _ctx.IL.Emit(OpCodes.Call, closeMethod);
            return true;
        }

        private bool EmitBuiltinNew(CallExpression call)
        {
            if (call.Type is PointerTypeSymbol ptrType)
            {
                var elemClrType = _ctx.Mapper.Map(ptrType.ElementType);

                if (!elemClrType.IsValueType && elemClrType is not TypeBuilder && elemClrType is not GenericTypeParameterBuilder)
                {
                    var ctor = elemClrType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                        _ctx.IL.Emit(OpCodes.Newobj, ctor);
                    else
                        _ctx.IL.Emit(OpCodes.Ldnull);
                }
                else
                {
                    var ptrClrType = typeof(Ptr<>).MakeGenericType(elemClrType);
                    var ctor = EmitContext.GetConstructorSafe(ptrClrType, Type.EmptyTypes);
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                }
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
            bool isString = clrType == typeof(string);

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
                    _ctx.IL.Emit(OpCodes.Call,
                        typeof(string).GetMethod("Compare", new[] { typeof(string), typeof(string) })!);
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
            if (arg.Type is MapTypeSymbol)
            {
                _body.EmitExpression(arg);
                var mapClrType = _ctx.Mapper.Map(arg.Type);
                var clearMethod = EmitContext.GetMethodSafe(mapClrType, "Clear");
                if (clearMethod != null)
                {
                    _ctx.IL.Emit(OpCodes.Call, clearMethod);
                }
                return true;
            }

            if (arg.Type is SliceTypeSymbol sliceType)
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
                _ctx.IL.Emit(OpCodes.Call, EmitContext.GetPropertyGetterSafe(sliceClrType, "Len"));
                _ctx.IL.Emit(OpCodes.Bge, loopEnd);

                _ctx.IL.Emit(OpCodes.Ldloca, sliceLocal);
                _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                var indexerGetter = EmitContext.GetPropertyGetterSafe(sliceClrType, "Item");
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
    }
}

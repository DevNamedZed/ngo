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
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Ast;
using Ngo.Compiler.Symbols;
using Ngo.Runtime;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Emits IL for builtin function calls and standard library dispatch.
    /// </summary>
    internal sealed class BuiltinEmitter
    {
        private readonly EmitContext _ctx;
        private readonly MethodBodyEmitter _body;

        public BuiltinEmitter(EmitContext ctx, MethodBodyEmitter body)
        {
            _ctx = ctx;
            _body = body;
        }

        public bool EmitBuiltinCall(CallExpression call)
        {
            var name = call.Function.Name;

            // Package-qualified dispatch for functions with conflicting names
            if (call.Function.PackageName == "os")
            {
                return EmitStaticCall(call, typeof(GoOs), name);
            }

            if (call.Function.PackageName == "regexp")
            {
                return EmitStaticCall(call, typeof(GoRegexp), name);
            }

            if (call.Function.PackageName == "unicode")
            {
                return EmitStaticCall(call, typeof(GoUnicode), name);
            }

            if (call.Function.PackageName == "bytes")
            {
                return EmitStaticCall(call, typeof(GoBytes), name);
            }

            if (call.Function.PackageName == "path")
            {
                return EmitStaticCall(call, typeof(GoPath), name);
            }

            if (call.Function.PackageName == "utf8")
            {
                return EmitStaticCall(call, typeof(GoUtf8), name);
            }

            if (call.Function.PackageName == "filepath")
            {
                switch (name)
                {
                    case "Join":
                    {
                        // Pack args into string[]
                        var argCount = call.Arguments.Count;
                        _ctx.IL.Emit(OpCodes.Ldc_I4, argCount);
                        _ctx.IL.Emit(OpCodes.Newarr, typeof(string));
                        for (int i = 0; i < argCount; i++)
                        {
                            _ctx.IL.Emit(OpCodes.Dup);
                            _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                            _body.EmitExpression(call.Arguments[i]);
                            _ctx.IL.Emit(OpCodes.Stelem_Ref);
                        }
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(GoFilepath).GetMethod("Join")!);
                        return true;
                    }
                    default:
                        return EmitStaticCall(call, typeof(GoFilepath), name);
                }
            }

            if (call.Function.PackageName == "bufio")
            {
                switch (name)
                {
                    case "NewScanner":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                        _ctx.IL.Emit(OpCodes.Newobj,
                            typeof(GoScanner).GetConstructor(new[] { typeof(IGoReader) })!);
                        return true;
                    case "NewReader":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                        _ctx.IL.Emit(OpCodes.Newobj,
                            typeof(GoBufferedReader).GetConstructor(new[] { typeof(IGoReader) })!);
                        return true;
                    case "NewWriter":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                        _ctx.IL.Emit(OpCodes.Newobj,
                            typeof(GoBufferedWriter).GetConstructor(new[] { typeof(IGoWriter) })!);
                        return true;
                }
            }

            if (call.Function.PackageName == "io")
            {
                switch (name)
                {
                    case "Copy":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                        _body.EmitExpression(call.Arguments[1]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoIo).GetMethod("Copy")!);
                        return true;
                    case "ReadAll":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoIo).GetMethod("ReadAll")!);
                        return true;
                    case "WriteString":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                        _body.EmitExpression(call.Arguments[1]);
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoIo).GetMethod("WriteString")!);
                        return true;
                }
            }

            if (call.Function.PackageName == "fmt")
            {
                switch (name)
                {
                    case "Fprintf":
                        // First arg is IGoWriter, second is format string, rest are varargs
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                        EmitFmtFormatCallFrom(call, "Fprintf", typeof(Fmt), 1);
                        return true;
                    case "Fprintln":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                        EmitFmtWriterPrintArgs(call, 1);
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Fmt).GetMethod("Fprintln",
                                new[] { typeof(IGoWriter), typeof(object[]) })!);
                        return true;
                    case "Fprint":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                        EmitFmtWriterPrintArgs(call, 1);
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Fmt).GetMethod("Fprint",
                                new[] { typeof(IGoWriter), typeof(object[]) })!);
                        return true;
                }
            }

            if (call.Function.PackageName == "log")
            {
                switch (name)
                {
                    case "Println":
                        EmitBuiltinPrintArgs(call);
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoLog).GetMethod("Println")!);
                        return true;
                    case "Print":
                        EmitBuiltinPrintArgs(call);
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoLog).GetMethod("Print")!);
                        return true;
                    case "Printf":
                        EmitFmtFormatCall(call, "Printf", typeof(GoLog));
                        return true;
                    case "Fatal":
                        EmitBuiltinPrintArgs(call);
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoLog).GetMethod("Fatal")!);
                        return true;
                    case "Fatalf":
                        EmitFmtFormatCall(call, "Fatalf", typeof(GoLog));
                        return true;
                }
            }

            switch (name)
            {
                case "println":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, typeof(BuiltIn).GetMethod("Println")!);
                    return true;

                case "Println":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, typeof(Fmt).GetMethod("Println")!);
                    return true;

                case "print":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, typeof(BuiltIn).GetMethod("Print")!);
                    return true;

                case "Print":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, typeof(Fmt).GetMethod("Print")!);
                    return true;

                case "Printf":
                    EmitFmtFormatCall(call, "Printf");
                    return true;

                case "Sprintf":
                    EmitFmtFormatCall(call, "Sprintf");
                    return true;

                case "Errorf":
                    // Errorf returns a string for now (simplified)
                    EmitFmtFormatCall(call, "Sprintf");
                    return true;

                case "Sprint":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, typeof(Fmt).GetMethod("Sprint")!);
                    return true;

                case "Sprintln":
                    EmitBuiltinPrintArgs(call);
                    _ctx.IL.Emit(OpCodes.Call, typeof(Fmt).GetMethod("Sprintln")!);
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

                case "new":
                    return EmitBuiltinNew(call);

                case "complex":
                    return EmitBuiltinComplex(call);
                case "real":
                    return EmitBuiltinReal(call);
                case "imag":
                    return EmitBuiltinImag(call);

                // --- strconv ---
                case "Itoa":
                    return EmitStaticCall(call, typeof(GoStrconv), "Itoa");
                case "Atoi":
                    return EmitStaticCall(call, typeof(GoStrconv), "Atoi");
                case "FormatInt":
                    return EmitStaticCall(call, typeof(GoStrconv), "FormatInt");
                case "FormatBool":
                    return EmitStaticCall(call, typeof(GoStrconv), "FormatBool");
                case "ParseInt":
                    return EmitStaticCall(call, typeof(GoStrconv), "ParseInt");
                case "ParseFloat":
                    return EmitStaticCall(call, typeof(GoStrconv), "ParseFloat");
                case "FormatFloat":
                    return EmitStaticCall(call, typeof(GoStrconv), "FormatFloat");
                case "ParseBool":
                    return EmitStaticCall(call, typeof(GoStrconv), "ParseBool");

                // --- strings ---
                case "Contains":
                    return EmitStaticCall(call, typeof(GoStrings), "Contains");
                case "HasPrefix":
                    return EmitStaticCall(call, typeof(GoStrings), "HasPrefix");
                case "HasSuffix":
                    return EmitStaticCall(call, typeof(GoStrings), "HasSuffix");
                case "Join":
                    return EmitStaticCall(call, typeof(GoStrings), "Join");
                case "Split":
                    return EmitStaticCall(call, typeof(GoStrings), "Split");
                case "Replace":
                    return EmitStaticCall(call, typeof(GoStrings), "Replace");
                case "TrimSpace":
                    return EmitStaticCall(call, typeof(GoStrings), "TrimSpace");
                case "ToUpper":
                    return EmitStaticCall(call, typeof(GoStrings), "ToUpper");
                case "ToLower":
                    return EmitStaticCall(call, typeof(GoStrings), "ToLower");
                case "Index":
                    return EmitStaticCall(call, typeof(GoStrings), "Index");
                case "Repeat":
                    return EmitStaticCall(call, typeof(GoStrings), "Repeat");
                case "ReplaceAll":
                    return EmitStaticCall(call, typeof(GoStrings), "ReplaceAll");
                case "Trim":
                    return EmitStaticCall(call, typeof(GoStrings), "Trim");
                case "NewReader":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Newobj,
                        typeof(StringReader).GetConstructor(new[] { typeof(string) })!);
                    return true;

                // --- errors ---
                case "New":
                    return EmitStaticCall(call, typeof(GoErrors), "New");

                // --- math ---
                case "Abs":
                    return EmitStaticCall(call, typeof(GoMath), "Abs");
                case "Max":
                    return EmitStaticCall(call, typeof(GoMath), "Max");
                case "Min":
                    return EmitStaticCall(call, typeof(GoMath), "Min");
                case "Sqrt":
                    return EmitStaticCall(call, typeof(GoMath), "Sqrt");
                case "Floor":
                    return EmitStaticCall(call, typeof(GoMath), "Floor");
                case "Ceil":
                    return EmitStaticCall(call, typeof(GoMath), "Ceil");
                case "Round":
                    return EmitStaticCall(call, typeof(GoMath), "Round");
                case "Pow":
                    return EmitStaticCall(call, typeof(GoMath), "Pow");
                case "Log":
                    return EmitStaticCall(call, typeof(GoMath), "Log");
                case "Mod":
                    return EmitStaticCall(call, typeof(GoMath), "Mod");

                // --- os ---
                case "Exit":
                    return EmitStaticCall(call, typeof(GoOs), "Exit");
                case "Getenv":
                    return EmitStaticCall(call, typeof(GoOs), "Getenv");

                // --- time ---
                case "Sleep":
                    return EmitStaticCall(call, typeof(GoTime), "Sleep");

                // --- math/rand ---
                case "Intn":
                    return EmitStaticCall(call, typeof(GoRand), "Intn");
                case "Float64":
                    return EmitStaticCall(call, typeof(GoRand), "Float64");
                case "Seed":
                    return EmitStaticCall(call, typeof(GoRand), "Seed");

                // --- sort ---
                case "Ints":
                    return EmitStaticCall(call, typeof(GoSort), "Ints");
                case "Strings":
                    return EmitStaticCall(call, typeof(GoSort), "Strings");
                case "Float64s":
                    return EmitStaticCall(call, typeof(GoSort), "Float64s");
                case "IntsAreSorted":
                    return EmitStaticCall(call, typeof(GoSort), "IntsAreSorted");
                case "StringsAreSorted":
                    return EmitStaticCall(call, typeof(GoSort), "StringsAreSorted");
                case "Float64sAreSorted":
                    return EmitStaticCall(call, typeof(GoSort), "Float64sAreSorted");
                case "SearchInts":
                    return EmitStaticCall(call, typeof(GoSort), "SearchInts");
                case "SearchStrings":
                    return EmitStaticCall(call, typeof(GoSort), "SearchStrings");

                default:
                    return false;
            }
        }

        public bool EmitStaticCall(CallExpression call, Type targetType, string methodName)
        {
            var paramTypes = new Type[call.Arguments.Count];
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                paramTypes[i] = _ctx.Mapper.Map(call.Arguments[i].Type);
            }

            var method = targetType.GetMethod(methodName, paramTypes);

            // If exact match fails, find by name + param count and emit conversions
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
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                _body.EmitExpression(call.Arguments[i]);
                EmitImplicitConv(paramTypes[i], methodParams[i].ParameterType);
            }

            _ctx.IL.Emit(OpCodes.Call, method);
            return true;
        }

        private void EmitImplicitConv(Type source, Type target)
        {
            if (source == target) return;
            if (target == typeof(byte)) _ctx.IL.Emit(OpCodes.Conv_U1);
            else if (target == typeof(short)) _ctx.IL.Emit(OpCodes.Conv_I2);
            else if (target == typeof(int)) _ctx.IL.Emit(OpCodes.Conv_I4);
            else if (target == typeof(long)) _ctx.IL.Emit(OpCodes.Conv_I8);
            else if (target == typeof(float)) _ctx.IL.Emit(OpCodes.Conv_R4);
            else if (target == typeof(double)) _ctx.IL.Emit(OpCodes.Conv_R8);
        }

        private bool EmitBuiltinLen(CallExpression call)
        {
            var arg = call.Arguments[0];
            var argType = arg.Type;

            if (argType.TypeKind == TypeKind.String || argType.TypeKind == TypeKind.UntypedString)
            {
                _body.EmitExpression(arg);
                _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("Len")!);
                // GoString.Len returns int (Int32), convert to int64
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (argType is SliceTypeSymbol sliceTs)
            {
                var sliceClrType = _ctx.Mapper.Map(argType);
                _body.EmitExpressionAddress(arg, sliceClrType);
                var lenProp = sliceClrType.GetProperty("Len")!;
                _ctx.IL.Emit(OpCodes.Call, lenProp.GetGetMethod()!);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (argType is MapTypeSymbol)
            {
                _body.EmitExpression(arg);
                var mapClrType = _ctx.Mapper.Map(argType);
                var lenProp = mapClrType.GetProperty("Len")!;
                _ctx.IL.Emit(OpCodes.Call, lenProp.GetGetMethod()!);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (argType is ArrayTypeSymbol)
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
                var capProp = sliceClrType.GetProperty("Cap")!;
                _ctx.IL.Emit(OpCodes.Call, capProp.GetGetMethod()!);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            if (arg.Type is ChannelTypeSymbol)
            {
                _body.EmitExpression(arg);
                var chanClrType = _ctx.Mapper.Map(arg.Type);
                var capProp = chanClrType.GetProperty("Capacity")!;
                _ctx.IL.Emit(OpCodes.Call, capProp.GetGetMethod()!);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                return true;
            }

            return false;
        }

        private bool EmitBuiltinAppend(CallExpression call)
        {
            // append(slice, elems...)
            var sliceArg = call.Arguments[0];
            var sliceType = (SliceTypeSymbol)sliceArg.Type;
            var elemClrType = _ctx.Mapper.Map(sliceType.ElementType);
            var sliceClrType = _ctx.Mapper.Map(sliceType);

            _body.EmitExpression(sliceArg);

            // Remaining args go into a params T[] array
            var elemCount = call.Arguments.Count - 1;
            _ctx.IL.Emit(OpCodes.Ldc_I4, elemCount);
            _ctx.IL.Emit(OpCodes.Newarr, elemClrType);

            for (int i = 0; i < elemCount; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _body.EmitExpression(call.Arguments[i + 1]);
                _body.EmitStelem(elemClrType);
            }

            // Call Slice<T>.Append(slice, T[])
            var appendMethod = typeof(Slice<>).MakeGenericType(elemClrType)
                .GetMethod("Append", new[] { sliceClrType, elemClrType.MakeArrayType() })!;
            _ctx.IL.Emit(OpCodes.Call, appendMethod);
            return true;
        }

        private bool EmitBuiltinMake(CallExpression call)
        {
            var returnType = call.Function.ReturnType;

            if (returnType is SliceTypeSymbol sliceType)
            {
                var elemClrType = _ctx.Mapper.Map(sliceType.ElementType);
                var sliceClrType = _ctx.Mapper.Map(sliceType);

                // make([]T, len) or make([]T, len, cap)
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

                var makeMethod = sliceClrType.GetMethod("Make", new[] { typeof(int), typeof(int) })!;
                _ctx.IL.Emit(OpCodes.Call, makeMethod);
                return true;
            }

            if (returnType is MapTypeSymbol mapType)
            {
                var mapClrType = _ctx.Mapper.Map(mapType);
                var ctor = mapClrType.GetConstructor(Type.EmptyTypes)!;
                _ctx.IL.Emit(OpCodes.Newobj, ctor);
                return true;
            }

            if (returnType is ChannelTypeSymbol chanType)
            {
                var chanClrType = _ctx.Mapper.Map(chanType);

                if (call.Arguments.Count >= 1)
                {
                    // make(chan T, n) — buffered with capacity
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    var ctor = chanClrType.GetConstructor(new[] { typeof(int) })!;
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                }
                else
                {
                    // make(chan T) — unbuffered
                    var ctor = chanClrType.GetConstructor(Type.EmptyTypes)!;
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
            var deleteMethod = mapClrType.GetMethod("Delete")!;
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

            var copyMethod = typeof(Slice<>).MakeGenericType(elemClrType)
                .GetMethod("Copy", new[] { sliceClrType, sliceClrType })!;
            _ctx.IL.Emit(OpCodes.Call, copyMethod);
            _ctx.IL.Emit(OpCodes.Conv_I8);
            return true;
        }

        private bool EmitBuiltinClose(CallExpression call)
        {
            _body.EmitExpression(call.Arguments[0]);
            var chanClrType = _ctx.Mapper.Map(call.Arguments[0].Type);
            var closeMethod = chanClrType.GetMethod("Close")!;
            _ctx.IL.Emit(OpCodes.Call, closeMethod);
            return true;
        }

        private bool EmitBuiltinNew(CallExpression call)
        {
            // new(T) returns *T → Ptr<T>
            var innerType = _ctx.Mapper.Map(call.Type);
            // call.Type is PointerTypeSymbol, inner is the pointed-to type
            if (call.Type is PointerTypeSymbol ptrType)
            {
                var elemClrType = _ctx.Mapper.Map(ptrType.ElementType);
                var ptrClrType = typeof(Ptr<>).MakeGenericType(elemClrType);
                var ctor = ptrClrType.GetConstructor(Type.EmptyTypes)!;
                _ctx.IL.Emit(OpCodes.Newobj, ctor);
            }
            return true;
        }

        private bool EmitBuiltinComplex(CallExpression call)
        {
            // complex(real, imag) → new Complex(real, imag)
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
            // real(c) → c.Real
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
            // imag(c) → c.Imaginary
            _body.EmitExpression(call.Arguments[0]);
            var complexType = typeof(System.Numerics.Complex);
            var local = _ctx.IL.DeclareLocal(complexType);
            _ctx.IL.Emit(OpCodes.Stloc, local);
            _ctx.IL.Emit(OpCodes.Ldloca, local);
            var imagProp = complexType.GetProperty("Imaginary")!;
            _ctx.IL.Emit(OpCodes.Call, imagProp.GetGetMethod()!);
            return true;
        }

        private void EmitBuiltinPrintArgs(CallExpression call)
        {
            // Create object[] array with all arguments
            _ctx.IL.Emit(OpCodes.Ldc_I4, call.Arguments.Count);
            _ctx.IL.Emit(OpCodes.Newarr, typeof(object));

            for (int i = 0; i < call.Arguments.Count; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup); // array ref
                _ctx.IL.Emit(OpCodes.Ldc_I4, i); // index
                _body.EmitExpression(call.Arguments[i]);
                _body.EmitBoxIfNeeded(call.Arguments[i].Type);
                _ctx.IL.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitFmtFormatCall(CallExpression call, string methodName,
            Type? targetType = null)
        {
            targetType ??= typeof(Fmt);

            // First arg is the format string
            if (call.Arguments.Count > 0)
            {
                _body.EmitExpression(call.Arguments[0]);
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Ldstr, "");
            }

            // Remaining args packed into object[]
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
            // Format string is at formatArgIndex
            if (call.Arguments.Count > formatArgIndex)
            {
                _body.EmitExpression(call.Arguments[formatArgIndex]);
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Ldstr, "");
            }

            // Remaining args packed into object[]
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
            // Pack args from startIndex onward into object[]
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

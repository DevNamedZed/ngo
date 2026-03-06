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

            if (call.Function.PackageName == "time")
            {
                return EmitStaticCall(call, typeof(GoTime), name);
            }

            if (call.Function.PackageName == "regexp")
            {
                return EmitStaticCall(call, typeof(GoRegexp), name);
            }

            if (call.Function.PackageName == "dotnet")
            {
                return EmitDotnetCall(call, name);
            }

            if (call.Function.PackageName == "context")
            {
                return EmitStaticCall(call, typeof(GoContext), name);
            }

            if (call.Function.PackageName == "unicode")
            {
                return EmitStaticCall(call, typeof(GoUnicode), name);
            }

            if (call.Function.PackageName == "bytes")
            {
                return EmitStaticCall(call, typeof(GoBytes), name);
            }

            if (call.Function.PackageName == "json")
            {
                return EmitStaticCall(call, typeof(GoJson), name);
            }

            if (call.Function.PackageName == "hex")
            {
                return EmitStaticCall(call, typeof(GoHex), name);
            }

            if (call.Function.PackageName == "sha256")
            {
                return EmitStaticCall(call, typeof(GoSha256), name);
            }

            if (call.Function.PackageName == "crand")
            {
                return EmitStaticCall(call, typeof(GoCryptoRand), name);
            }

            if (call.Function.PackageName == "flag")
            {
                return EmitStaticCall(call, typeof(GoFlag), name);
            }

            if (call.Function.PackageName == "http")
            {
                return EmitStaticCall(call, typeof(GoHttp), name);
            }

            if (call.Function.PackageName == "reflect")
            {
                return EmitStaticCall(call, typeof(GoReflect), name);
            }

            if (call.Function.PackageName == "csv")
            {
                switch (name)
                {
                    case "NewReader":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(GoCsv).GetMethod("NewReader",
                                new[] { typeof(object) })!);
                        return true;
                    case "NewWriter":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(GoCsv).GetMethod("NewWriter",
                                new[] { typeof(object) })!);
                        return true;
                }
            }

            if (call.Function.PackageName == "ioutil")
            {
                switch (name)
                {
                    case "ReadAll":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoIo).GetMethod("ReadAll")!);
                        return true;
                    case "ReadFile":
                        return EmitStaticCall(call, typeof(GoOs), "ReadFile");
                    case "WriteFile":
                        return EmitStaticCall(call, typeof(GoOs), "WriteFile");
                    case "NopCloser":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoIo).GetMethod("NopCloser")!);
                        return true;
                }
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
                    case "NopCloser":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoIo).GetMethod("NopCloser")!);
                        return true;
                    case "LimitReader":
                        _body.EmitExpression(call.Arguments[0]);
                        _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                        _body.EmitExpression(call.Arguments[1]);
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoIo).GetMethod("LimitReader")!);
                        return true;
                    case "MultiReader":
                    {
                        // Pack args into IGoReader[]
                        _ctx.IL.Emit(OpCodes.Ldc_I4, call.Arguments.Count);
                        _ctx.IL.Emit(OpCodes.Newarr, typeof(IGoReader));
                        for (int i = 0; i < call.Arguments.Count; i++)
                        {
                            _ctx.IL.Emit(OpCodes.Dup);
                            _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                            _body.EmitExpression(call.Arguments[i]);
                            _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoReader));
                            _ctx.IL.Emit(OpCodes.Stelem_Ref);
                        }
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoIo).GetMethod("MultiReader")!);
                        return true;
                    }
                    case "MultiWriter":
                    {
                        // Pack args into IGoWriter[]
                        _ctx.IL.Emit(OpCodes.Ldc_I4, call.Arguments.Count);
                        _ctx.IL.Emit(OpCodes.Newarr, typeof(IGoWriter));
                        for (int i = 0; i < call.Arguments.Count; i++)
                        {
                            _ctx.IL.Emit(OpCodes.Dup);
                            _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                            _body.EmitExpression(call.Arguments[i]);
                            _ctx.IL.Emit(OpCodes.Castclass, typeof(IGoWriter));
                            _ctx.IL.Emit(OpCodes.Stelem_Ref);
                        }
                        _ctx.IL.Emit(OpCodes.Call, typeof(GoIo).GetMethod("MultiWriter")!);
                        return true;
                    }
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
                    case "Scan":
                        EmitFmtWriterPrintArgs(call, 0); // all args as object[]
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Fmt).GetMethod("Scan",
                                new[] { typeof(object[]) })!);
                        return true;
                    case "Scanf":
                        _body.EmitExpression(call.Arguments[0]); // format
                        EmitFmtWriterPrintArgs(call, 1); // remaining args as object[]
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Fmt).GetMethod("Scanf",
                                new[] { typeof(string), typeof(object[]) })!);
                        return true;
                    case "Scanln":
                        EmitFmtWriterPrintArgs(call, 0); // all args as object[]
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Fmt).GetMethod("Scanln",
                                new[] { typeof(object[]) })!);
                        return true;
                    case "Sscan":
                        _body.EmitExpression(call.Arguments[0]); // str
                        EmitFmtWriterPrintArgs(call, 1); // remaining args as object[]
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Fmt).GetMethod("Sscan",
                                new[] { typeof(string), typeof(object[]) })!);
                        return true;
                    case "Sscanf":
                        _body.EmitExpression(call.Arguments[0]); // str
                        _body.EmitExpression(call.Arguments[1]); // format
                        EmitFmtWriterPrintArgs(call, 2); // remaining args as object[]
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Fmt).GetMethod("Sscanf",
                                new[] { typeof(string), typeof(string), typeof(object[]) })!);
                        return true;
                    case "Sscanln":
                        _body.EmitExpression(call.Arguments[0]); // str
                        EmitFmtWriterPrintArgs(call, 1); // remaining args as object[]
                        _ctx.IL.Emit(OpCodes.Call,
                            typeof(Fmt).GetMethod("Sscanln",
                                new[] { typeof(string), typeof(object[]) })!);
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
                    EmitFmtFormatCall(call, "Errorf");
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
                case "ParseUint":
                    return EmitStaticCall(call, typeof(GoStrconv), "ParseUint");
                case "FormatUint":
                    return EmitStaticCall(call, typeof(GoStrconv), "FormatUint");
                case "Quote":
                    return EmitStaticCall(call, typeof(GoStrconv), "Quote");
                case "Unquote":
                    return EmitStaticCall(call, typeof(GoStrconv), "Unquote");

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
                case "TrimPrefix":
                    return EmitStaticCall(call, typeof(GoStrings), "TrimPrefix");
                case "TrimSuffix":
                    return EmitStaticCall(call, typeof(GoStrings), "TrimSuffix");
                case "TrimLeft":
                    return EmitStaticCall(call, typeof(GoStrings), "TrimLeft");
                case "TrimRight":
                    return EmitStaticCall(call, typeof(GoStrings), "TrimRight");
                case "Count":
                    return EmitStaticCall(call, typeof(GoStrings), "Count");
                case "EqualFold":
                    return EmitStaticCall(call, typeof(GoStrings), "EqualFold");
                case "Fields":
                    return EmitStaticCall(call, typeof(GoStrings), "Fields");
                case "LastIndex":
                    return EmitStaticCall(call, typeof(GoStrings), "LastIndex");
                case "ContainsRune":
                    return EmitStaticCall(call, typeof(GoStrings), "ContainsRune");
                case "ContainsAny":
                    return EmitStaticCall(call, typeof(GoStrings), "ContainsAny");
                case "Cut":
                    return EmitStaticCall(call, typeof(GoStrings), "Cut");
                case "SplitN":
                    return EmitStaticCall(call, typeof(GoStrings), "SplitN");
                case "SplitAfter":
                    return EmitStaticCall(call, typeof(GoStrings), "SplitAfter");
                case "SplitAfterN":
                    return EmitStaticCall(call, typeof(GoStrings), "SplitAfterN");
                case "Title":
                    return EmitStaticCall(call, typeof(GoStrings), "Title");
                case "IndexByte":
                    return EmitStaticCall(call, typeof(GoStrings), "IndexByte");
                case "IndexRune":
                    return EmitStaticCall(call, typeof(GoStrings), "IndexRune");
                case "IndexAny":
                    return EmitStaticCall(call, typeof(GoStrings), "IndexAny");
                case "NewReader":
                    _body.EmitExpression(call.Arguments[0]);
                    _ctx.IL.Emit(OpCodes.Newobj,
                        typeof(StringReader).GetConstructor(new[] { typeof(string) })!);
                    return true;

                case "NewReplacer":
                    // Pack variadic string args into string[]
                    _ctx.IL.Emit(OpCodes.Ldc_I4, call.Arguments.Count);
                    _ctx.IL.Emit(OpCodes.Newarr, typeof(string));
                    for (int i = 0; i < call.Arguments.Count; i++)
                    {
                        _ctx.IL.Emit(OpCodes.Dup);
                        _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                        _body.EmitExpression(call.Arguments[i]);
                        _ctx.IL.Emit(OpCodes.Stelem_Ref);
                    }
                    _ctx.IL.Emit(OpCodes.Newobj,
                        typeof(GoReplacer).GetConstructor(new[] { typeof(string[]) })!);
                    return true;

                // --- errors ---
                case "New":
                    return EmitStaticCall(call, typeof(GoErrors), "New");
                case "Unwrap":
                    return EmitStaticCall(call, typeof(GoErrors), "Unwrap");
                case "Is":
                    return EmitStaticCall(call, typeof(GoErrors), "Is");
                case "As":
                    return EmitStaticCall(call, typeof(GoErrors), "As");

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
                case "Log2":
                    return EmitStaticCall(call, typeof(GoMath), "Log2");
                case "Log10":
                    return EmitStaticCall(call, typeof(GoMath), "Log10");
                case "Exp":
                    return EmitStaticCall(call, typeof(GoMath), "Exp");
                case "Mod":
                    return EmitStaticCall(call, typeof(GoMath), "Mod");
                case "Sin":
                    return EmitStaticCall(call, typeof(GoMath), "Sin");
                case "Cos":
                    return EmitStaticCall(call, typeof(GoMath), "Cos");
                case "Tan":
                    return EmitStaticCall(call, typeof(GoMath), "Tan");
                case "Atan":
                    return EmitStaticCall(call, typeof(GoMath), "Atan");
                case "Atan2":
                    return EmitStaticCall(call, typeof(GoMath), "Atan2");
                case "Inf":
                    return EmitStaticCall(call, typeof(GoMath), "Inf");
                case "IsNaN":
                    return EmitStaticCall(call, typeof(GoMath), "IsNaN");
                case "IsInf":
                    return EmitStaticCall(call, typeof(GoMath), "IsInf");
                case "NaN":
                    return EmitStaticCall(call, typeof(GoMath), "NaN");
                case "Remainder":
                    return EmitStaticCall(call, typeof(GoMath), "Remainder");
                case "Trunc":
                    return EmitStaticCall(call, typeof(GoMath), "Trunc");
                case "Pow10":
                    return EmitStaticCall(call, typeof(GoMath), "Pow10");
                case "Asin":
                    return EmitStaticCall(call, typeof(GoMath), "Asin");
                case "Acos":
                    return EmitStaticCall(call, typeof(GoMath), "Acos");
                case "Sinh":
                    return EmitStaticCall(call, typeof(GoMath), "Sinh");
                case "Cosh":
                    return EmitStaticCall(call, typeof(GoMath), "Cosh");
                case "Tanh":
                    return EmitStaticCall(call, typeof(GoMath), "Tanh");
                case "Cbrt":
                    return EmitStaticCall(call, typeof(GoMath), "Cbrt");
                case "Hypot":
                    return EmitStaticCall(call, typeof(GoMath), "Hypot");
                case "Dim":
                    return EmitStaticCall(call, typeof(GoMath), "Dim");
                case "Copysign":
                    return EmitStaticCall(call, typeof(GoMath), "Copysign");
                case "Ldexp":
                    return EmitStaticCall(call, typeof(GoMath), "Ldexp");
                case "Logb":
                    return EmitStaticCall(call, typeof(GoMath), "Logb");
                case "Ilogb":
                    return EmitStaticCall(call, typeof(GoMath), "Ilogb");

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
                case "Slice":
                    return EmitStaticCall(call, typeof(GoSort), "Slice");
                case "SliceStable":
                    return EmitStaticCall(call, typeof(GoSort), "SliceStable");
                case "SliceIsSorted":
                    return EmitStaticCall(call, typeof(GoSort), "SliceIsSorted");

                default:
                    return false;
            }
        }

        private bool EmitDotnetCall(CallExpression call, string name)
        {
            var method = typeof(GoDotnet).GetMethod(name);
            if (method == null)
                throw new NotSupportedException($"dotnet.{name} not found");

            var methodParams = method.GetParameters();
            bool isVariadic = methodParams.Length > 0 &&
                methodParams[methodParams.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);

            if (!isVariadic)
            {
                // Non-variadic: emit all args directly
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

            // Variadic: emit fixed args, pack remaining into object[]
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
            if (target == typeof(object) && source.IsValueType)
                _ctx.IL.Emit(OpCodes.Box, source);
            else if (target == typeof(byte)) _ctx.IL.Emit(OpCodes.Conv_U1);
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

        private bool EmitMinMax(CallExpression call, bool isMin)
        {
            var argType = call.Arguments[0].Type;
            var clrType = _ctx.Mapper.Map(argType);
            bool isFloat = clrType == typeof(double) || clrType == typeof(float);
            bool isString = clrType == typeof(string);

            // Emit first argument
            _body.EmitExpression(call.Arguments[0]);

            // For each subsequent argument, compare and keep the min/max
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
                    // Use string.Compare, result is int: <0, 0, >0
                    _ctx.IL.Emit(OpCodes.Ldloc, candidateLocal);
                    _ctx.IL.Emit(OpCodes.Ldloc, resultLocal);
                    _ctx.IL.Emit(OpCodes.Call,
                        typeof(string).GetMethod("Compare", new[] { typeof(string), typeof(string) })!);
                    _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                    if (isMin)
                        _ctx.IL.Emit(OpCodes.Bge, keepCurrent); // compare >= 0 → candidate >= current → keep current
                    else
                        _ctx.IL.Emit(OpCodes.Ble, keepCurrent); // compare <= 0 → candidate <= current → keep current
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

                // Use candidate
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
                var clearMethod = mapClrType.GetMethod("Clear");
                if (clearMethod != null)
                {
                    _ctx.IL.Emit(OpCodes.Call, clearMethod);
                }
                return true;
            }

            if (arg.Type is SliceTypeSymbol sliceType)
            {
                // Clear slice: set all elements to zero value
                var elemClrType = _ctx.Mapper.Map(sliceType.ElementType);
                var sliceClrType = _ctx.Mapper.Map(sliceType);

                _body.EmitExpression(arg);
                var sliceLocal = _ctx.IL.DeclareLocal(sliceClrType);
                _ctx.IL.Emit(OpCodes.Stloc, sliceLocal);

                // for i := 0; i < len(s); i++ { s[i] = zero }
                var indexLocal = _ctx.IL.DeclareLocal(typeof(int));
                _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                _ctx.IL.Emit(OpCodes.Stloc, indexLocal);

                var loopStart = _ctx.IL.DefineLabel();
                var loopEnd = _ctx.IL.DefineLabel();

                _ctx.IL.MarkLabel(loopStart);
                _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
                _ctx.IL.Emit(OpCodes.Ldloca, sliceLocal);
                _ctx.IL.Emit(OpCodes.Call, sliceClrType.GetProperty("Len")!.GetGetMethod()!);
                _ctx.IL.Emit(OpCodes.Bge, loopEnd);

                // s[i] = default — indexer returns ref T, so use initobj
                _ctx.IL.Emit(OpCodes.Ldloca, sliceLocal);
                _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                var indexerGetter = sliceClrType.GetProperty("Item")!.GetGetMethod()!;
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

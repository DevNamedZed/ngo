// -----------------------------------------------------------------------
// <copyright file="ClosureEmitter.cs" company="Ziad">
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
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Ast;
using Ngo.Compiler.Archive;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Symbols;
using Ngo.Runtime;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Emits IL for function literals, closures, and method values.
    /// </summary>
    internal sealed class ClosureEmitter
    {
        private readonly EmitContext _ctx;
        private readonly MethodBodyEmitter _body;

        public ClosureEmitter(EmitContext ctx, MethodBodyEmitter body)
        {
            _ctx = ctx;
            _body = body;
        }

        public void EmitFunctionLiteral(FunctionLiteralExpression funcLit)
        {
            // Detect captured variables from the enclosing scope
            var captures = FindCaptures(funcLit.Body);

            if (captures.Count > 0)
            {
                EmitClosureLiteral(funcLit, captures);
                return;
            }

            // No captures — emit as a static method
            var lambdaName = $"__lambda_{_body.LambdaCounter++}";

            IMethodBuilder lambdaMethod;
            Type[]? lambdaGenericParams = null;
            if (_ctx.EnclosingGenericParamNames.Length > 0)
            {
                lambdaMethod = _ctx.PackageType.DefineMethod(
                    lambdaName,
                    MethodAttributes.Private | MethodAttributes.Static);
                lambdaGenericParams = lambdaMethod.DefineGenericParameters(_ctx.EnclosingGenericParamNames);

                var paramTypes = new Type[funcLit.Parameters.Count];
                for (int i = 0; i < funcLit.Parameters.Count; i++)
                {
                    paramTypes[i] = _ctx.Mapper.Map(funcLit.Parameters[i].Type);
                }
                var returnType = _ctx.Mapper.MapReturnType(funcLit.ReturnTypes);
                lambdaMethod.SetReturnType(returnType);
                lambdaMethod.SetParameters(paramTypes);
            }
            else
            {
                var paramTypes = new Type[funcLit.Parameters.Count];
                for (int i = 0; i < funcLit.Parameters.Count; i++)
                {
                    paramTypes[i] = _ctx.Mapper.Map(funcLit.Parameters[i].Type);
                }
                var returnType = _ctx.Mapper.MapReturnType(funcLit.ReturnTypes);
                lambdaMethod = _ctx.PackageType.DefineMethod(
                    lambdaName,
                    MethodAttributes.Private | MethodAttributes.Static,
                    returnType,
                    paramTypes);
            }

            for (int i = 0; i < funcLit.Parameters.Count; i++)
            {
                lambdaMethod.DefineParameter(i + 1, ParameterAttributes.None, funcLit.Parameters[i].Name);
            }

            // Save current method state
            var savedIL = _ctx.IL;
            var savedLocals = new Dictionary<Symbol, LocalSlot>(_ctx.Locals);
            var savedParams = new Dictionary<Symbol, int>(_ctx.Parameters);
            var savedCaptured = new HashSet<Symbol>(_ctx.CapturedSymbols);
            var savedReturnTypes = _body.CurrentReturnTypes;
            var savedNamedReturns = _body.NamedReturns;

            _ctx.IL = lambdaMethod.GetILWriter();
            _ctx.Locals.Clear();
            _ctx.Parameters.Clear();
            _ctx.CapturedSymbols.Clear();
            _body.CurrentReturnTypes = funcLit.ReturnTypes;
            _body.NamedReturns = funcLit.NamedReturns;

            for (int i = 0; i < funcLit.Parameters.Count; i++)
            {
                _ctx.Parameters[funcLit.Parameters[i]] = i;
            }

            // Pre-scan for nested closures that capture this lambda's parameters
            var innerCaptures = CollectAllCaptures(funcLit.Body);
            foreach (var sym in innerCaptures)
                _ctx.CapturedSymbols.Add(sym);
            _body.BoxCapturedParameters(funcLit.Parameters);

            var savedDeferStack = _ctx.DeferStack;
            var savedDeferReturnLocal = _ctx.DeferReturnLocal;
            _ctx.DeferStack = null;
            _ctx.DeferReturnLocal = null;

            if (MethodBodyEmitter.ContainsDefer(funcLit.Body))
            {
                var litIsVoid = funcLit.ReturnTypes.Count == 0;
                var litRetType = litIsVoid ? null : _ctx.Mapper.MapReturnType(funcLit.ReturnTypes);
                _body.EmitDeferWrappedBody(funcLit.Body, litIsVoid, litRetType);
            }
            else
            {
                _body.EmitBlock(funcLit.Body);
                EmitClosureTrailingReturn(funcLit);
            }

            _ctx.DeferStack = savedDeferStack;
            _ctx.DeferReturnLocal = savedDeferReturnLocal;

            // Restore method state
            _ctx.IL = savedIL;
            _ctx.Locals.Clear();
            foreach (var kvp in savedLocals) _ctx.Locals[kvp.Key] = kvp.Value;
            _ctx.Parameters.Clear();
            foreach (var kvp in savedParams) _ctx.Parameters[kvp.Key] = kvp.Value;
            _ctx.CapturedSymbols.Clear();
            foreach (var sym in savedCaptured) _ctx.CapturedSymbols.Add(sym);
            _body.CurrentReturnTypes = savedReturnTypes;
            _body.NamedReturns = savedNamedReturns;

            // Create delegate from the static method
            var delegateType = _ctx.Mapper.Map(funcLit.FunctionType);
            if (delegateType == typeof(Delegate) || delegateType == typeof(object))
            {
                // Circular type fallback — use Action for void, Func<object> for non-void
                delegateType = funcLit.FunctionType.ReturnTypes.Count == 0
                    ? typeof(Action)
                    : typeof(Func<object>);
            }
            _ctx.IL.Emit(OpCodes.Ldnull);
            _ctx.IL.Emit(OpCodes.Ldftn, lambdaMethod.AsMethodRef());
            var delegateCtor = _ctx.Definitions.GetConstructor(delegateType, new[] { typeof(object), typeof(IntPtr) });
            _ctx.IL.Emit(OpCodes.Newobj, delegateCtor);
        }

        private void EmitClosureLiteral(FunctionLiteralExpression funcLit, List<Symbol> captures)
        {
            var closureName = _ctx.QualifyWithPackage($"__closure_{_body.LambdaCounter++}");

            var closureBuilder = _ctx.Module.DefineType(
                closureName,
                TypeAttributes.Public | TypeAttributes.Sealed);
            _ctx.Definitions.RegisterType(closureName, closureBuilder);

            var closureConstructor = closureBuilder.DefineConstructor(
                MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            _ctx.Definitions.RegisterConstructor(closureName, Type.EmptyTypes, closureConstructor);
            var constructorIl = closureConstructor.GetILWriter();
            constructorIl.Emit(OpCodes.Ldarg_0);
            constructorIl.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
            constructorIl.Emit(OpCodes.Ret);

            // Propagate generic parameters from the enclosing function to the closure type.
            // Without this, parameter types like Func<E, bool> serialize as references to 'E'
            // which can't be resolved at link time since the closure type isn't generic.
            Type[]? closureGenericParams = null;
            Type[]? savedEnclosingParamMappings = null;
            if (_ctx.EnclosingGenericParamNames.Length > 0)
            {
                closureGenericParams = closureBuilder.DefineGenericParameters(_ctx.EnclosingGenericParamNames);

                // BUG-3 Shape B: rebind the enclosing generic-param symbols to the closure type's OWN
                // generic params, so the closure's signature and body map E to the closure type's `!k`
                // (resolvable on the generic closure type) instead of the enclosing method's `!!k` (which
                // the closure type can't resolve — the cause of the old degrade-to-Object). The original
                // mappings are saved and restored once the closure body is fully emitted below.
                var enclosingSymbols = _ctx.EnclosingGenericParamSymbols;
                savedEnclosingParamMappings = new Type[closureGenericParams.Length];
                for (int k = 0; k < closureGenericParams.Length && k < enclosingSymbols.Length; k++)
                {
                    savedEnclosingParamMappings[k] = _ctx.Mapper.Map(enclosingSymbols[k]);
                    _ctx.Mapper.Register(enclosingSymbols[k], closureGenericParams[k]);
                }
            }

            // Fields for captured variables — use Box<T> for shared mutation
            var captureFields = new Dictionary<Symbol, IFieldBuilder>();
            foreach (var sym in captures)
            {
                TypeSymbol symType;
                if (sym is LocalSymbol ls) symType = ls.Type;
                else if (sym is ParameterSymbol ps) symType = ps.Type;
                else continue;

                var innerType = _ctx.Mapper.Map(symType);
                var fieldType = typeof(Box<>).MakeGenericType(innerType);
                var captureField = closureBuilder.DefineField(sym.Name, fieldType, FieldAttributes.Public);
                _ctx.Definitions.RegisterField(closureName, sym.Name, captureField);
                captureFields[sym] = captureField;
            }

            // Instance method for the lambda body
            var paramTypes = new Type[funcLit.Parameters.Count];
            for (int i = 0; i < funcLit.Parameters.Count; i++)
            {
                paramTypes[i] = _ctx.Mapper.Map(funcLit.Parameters[i].Type);
            }

            var returnType = _ctx.Mapper.MapReturnType(funcLit.ReturnTypes);

            var invokeMethod = closureBuilder.DefineMethod(
                "Invoke",
                MethodAttributes.Public,
                returnType,
                paramTypes);
            _ctx.Definitions.RegisterMethod(closureName, "Invoke", paramTypes, invokeMethod);

            for (int i = 0; i < funcLit.Parameters.Count; i++)
                invokeMethod.DefineParameter(i + 1, ParameterAttributes.None, funcLit.Parameters[i].Name);

            // Save current method state
            var savedIL = _ctx.IL;
            var savedLocals = new Dictionary<Symbol, LocalSlot>(_ctx.Locals);
            var savedParams = new Dictionary<Symbol, int>(_ctx.Parameters);
            var savedCaptured2 = new HashSet<Symbol>(_ctx.CapturedSymbols);
            var savedReturnTypes = _body.CurrentReturnTypes;
            var savedNamedReturns = _body.NamedReturns;

            // Emit the invoke method body
            _ctx.IL = invokeMethod.GetILWriter();
            _ctx.Locals.Clear();
            _ctx.Parameters.Clear();
            _ctx.CapturedSymbols.Clear();
            _body.CurrentReturnTypes = funcLit.ReturnTypes;

            // Lambda params start at arg 1 (arg 0 is 'this')
            for (int i = 0; i < funcLit.Parameters.Count; i++)
            {
                _ctx.Parameters[funcLit.Parameters[i]] = i + 1;
            }

            // Load captured Box<T> references from closure fields into locals
            foreach (var (sym, field) in captureFields)
            {
                var local = _ctx.IL.DeclareLocal(field.FieldType); // Box<T>
                _ctx.Locals[sym] = local;
                _ctx.CapturedSymbols.Add(sym); // Enable .Value access in EmitLoad/EmitStore
                _ctx.IL.Emit(OpCodes.Ldarg_0);
                _ctx.IL.Emit(OpCodes.Ldfld, field.AsFieldRef());
                _ctx.IL.Emit(OpCodes.Stloc, local);
            }

            // Pre-scan for nested closures that capture this closure's parameters
            var innerCaptures = CollectAllCaptures(funcLit.Body);
            foreach (var sym in innerCaptures)
            {
                _ctx.CapturedSymbols.Add(sym);
            }
            _body.BoxCapturedParameters(funcLit.Parameters);

            // Declare named return locals for the closure
            _body.NamedReturns = funcLit.NamedReturns;
            foreach (var namedReturn in funcLit.NamedReturns)
            {
                var clrType = _ctx.Mapper.Map(namedReturn.Type);
                if (_ctx.CapturedSymbols.Contains(namedReturn))
                {
                    var boxType = typeof(Box<>).MakeGenericType(clrType);
                    var local = _ctx.IL.DeclareLocal(boxType);
                    var boxCtor = _ctx.Definitions.GetConstructor(boxType, Type.EmptyTypes);
                    _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                    _ctx.Locals[namedReturn] = local;
                }
                else
                {
                    var local = _ctx.IL.DeclareLocal(clrType);
                    _ctx.Locals[namedReturn] = local;
                }
            }

            var savedDeferStack2 = _ctx.DeferStack;
            var savedDeferReturnLocal2 = _ctx.DeferReturnLocal;
            _ctx.DeferStack = null;
            _ctx.DeferReturnLocal = null;

            if (MethodBodyEmitter.ContainsDefer(funcLit.Body))
            {
                var closureIsVoid = funcLit.ReturnTypes.Count == 0;
                var closureRetType = closureIsVoid ? null : _ctx.Mapper.MapReturnType(funcLit.ReturnTypes);
                _body.EmitDeferWrappedBody(funcLit.Body, closureIsVoid, closureRetType, _body.NamedReturns);
            }
            else
            {
                _body.EmitBlock(funcLit.Body);
                EmitClosureTrailingReturn(funcLit);
            }

            _ctx.DeferStack = savedDeferStack2;
            _ctx.DeferReturnLocal = savedDeferReturnLocal2;

            // Restore method state
            _ctx.IL = savedIL;
            _ctx.Locals.Clear();
            foreach (var kvp in savedLocals)
            {
                _ctx.Locals[kvp.Key] = kvp.Value;
            }
            _ctx.Parameters.Clear();
            foreach (var kvp in savedParams)
            {
                _ctx.Parameters[kvp.Key] = kvp.Value;
            }
            _ctx.CapturedSymbols.Clear();
            foreach (var sym in savedCaptured2)
            {
                _ctx.CapturedSymbols.Add(sym);
            }
            _body.CurrentReturnTypes = savedReturnTypes;
            _body.NamedReturns = savedNamedReturns;

            // BUG-3 Shape B: restore the enclosing method's `E -> !!k` mapping now that the closure body
            // (which mapped `E` to the closure type's `!k`) is fully emitted.
            if (savedEnclosingParamMappings != null)
            {
                var enclosingSymbols = _ctx.EnclosingGenericParamSymbols;
                for (int k = 0; k < savedEnclosingParamMappings.Length && k < enclosingSymbols.Length; k++)
                {
                    _ctx.Mapper.Register(enclosingSymbols[k], savedEnclosingParamMappings[k]);
                }
            }

            // Finalize closure type
            closureBuilder.CreateType();

            // If the closure is generic, instantiate it with the enclosing method's type params
            Type closureType;
            if (closureGenericParams != null && _ctx.EnclosingGenericParamTypes.Length > 0)
            {
                closureType = closureBuilder.AsType().MakeGenericType(_ctx.EnclosingGenericParamTypes);
            }
            else
            {
                closureType = closureBuilder.AsType();
            }

            var closureLocal = _ctx.IL.DeclareLocal(closureType);
            var resolvedConstructor = _ctx.Definitions.GetConstructor(closureType, Type.EmptyTypes)!;
            _ctx.IL.Emit(OpCodes.Newobj, resolvedConstructor);
            _ctx.IL.Emit(OpCodes.Stloc, closureLocal);

            foreach (var sym in captures)
            {
                _ctx.IL.Emit(OpCodes.Ldloc, closureLocal);
                var captureLocal = ResolveLocal(sym);
                _ctx.IL.Emit(OpCodes.Ldloc, captureLocal);
                var capturedField = _ctx.Definitions.GetField(closureType, sym.Name)!;
                _ctx.IL.Emit(OpCodes.Stfld, capturedField);
            }

            var delegateType = _ctx.Mapper.Map(funcLit.FunctionType);
            if (delegateType == typeof(Delegate) || delegateType == typeof(object))
            {
                delegateType = funcLit.FunctionType.ReturnTypes.Count == 0
                    ? typeof(Action) : typeof(Func<object>);
            }
            _ctx.IL.Emit(OpCodes.Ldloc, closureLocal);
            var closureInvokeMethod = _ctx.Definitions.GetMethod(closureType, "Invoke")!;
            _ctx.IL.Emit(OpCodes.Ldftn, closureInvokeMethod);
            var delegateCtor = _ctx.Definitions.GetConstructor(delegateType, new[] { typeof(object), typeof(IntPtr) })!;
            _ctx.IL.Emit(OpCodes.Newobj, delegateCtor);
        }

        public void EmitMethodValue(MethodValueExpression mv)
        {
            if (!_ctx.Methods.TryGetValue(mv.Method, out var targetMethod))
                return;

            if (mv.IsMethodExpression)
            {
                EmitMethodExpression(mv, targetMethod);
                return;
            }

            // Create a closure class that captures the receiver and forwards calls
            var closureName = _ctx.QualifyWithPackage($"__methodval_{_body.LambdaCounter++}");
            var receiverClrType = _ctx.Mapper.Map(mv.Receiver.Type);

            var closureBuilder = _ctx.Module.DefineType(
                closureName,
                TypeAttributes.Public | TypeAttributes.Sealed);
            _ctx.Definitions.RegisterType(closureName, closureBuilder);

            var methodValConstructor = closureBuilder.DefineConstructor(
                MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            _ctx.Definitions.RegisterConstructor(closureName, Type.EmptyTypes, methodValConstructor);
            var methodValCtorIl = methodValConstructor.GetILWriter();
            methodValCtorIl.Emit(OpCodes.Ldarg_0);
            methodValCtorIl.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
            methodValCtorIl.Emit(OpCodes.Ret);

            var receiverField = closureBuilder.DefineField(
                "_receiver", receiverClrType, FieldAttributes.Public);
            _ctx.Definitions.RegisterField(closureName, "_receiver", receiverField);

            // Build Invoke method: same params as the method (without receiver), same return
            var paramTypes = new Type[mv.Method.Parameters.Count];
            for (int i = 0; i < mv.Method.Parameters.Count; i++)
            {
                paramTypes[i] = _ctx.Mapper.Map(mv.Method.Parameters[i].Type);
            }

            var returnType = _ctx.Mapper.MapReturnType(mv.Method.ReturnTypes);

            var invokeMethod = closureBuilder.DefineMethod(
                "Invoke",
                MethodAttributes.Public,
                returnType,
                paramTypes);
            _ctx.Definitions.RegisterMethod(closureName, "Invoke", paramTypes, invokeMethod);

            // Emit Invoke body: load receiver from field, load params, call target method
            var invokeIL = invokeMethod.GetILWriter();
            invokeIL.Emit(OpCodes.Ldarg_0);
            invokeIL.Emit(OpCodes.Ldfld, receiverField.AsFieldRef());
            for (int i = 0; i < paramTypes.Length; i++)
            {
                invokeIL.Emit(OpCodes.Ldarg, i + 1);
            }
            invokeIL.Emit(OpCodes.Call, targetMethod.AsMethodRef());
            invokeIL.Emit(OpCodes.Ret);

            closureBuilder.CreateType();
            var closureType = closureBuilder.AsType();

            // Create closure instance, set receiver field
            var closureLocal = _ctx.IL.DeclareLocal(closureType);
            _ctx.IL.Emit(OpCodes.Newobj, methodValConstructor.AsCtorRef());
            _ctx.IL.Emit(OpCodes.Stloc, closureLocal);

            _ctx.IL.Emit(OpCodes.Ldloc, closureLocal);
            _body.EmitExpression(mv.Receiver);
            _ctx.IL.Emit(OpCodes.Stfld, receiverField.AsFieldRef());

            // Create delegate from closure + Invoke
            var delegateType = _ctx.Mapper.Map(mv.FunctionType);
            _ctx.IL.Emit(OpCodes.Ldloc, closureLocal);
            _ctx.IL.Emit(OpCodes.Ldftn, invokeMethod.AsMethodRef());
            var delegateCtor = _ctx.Definitions.GetConstructor(delegateType, new[] { typeof(object), typeof(IntPtr) });
            _ctx.IL.Emit(OpCodes.Newobj, delegateCtor);
        }

        private void EmitMethodExpression(MethodValueExpression mv, IMethodBuilder targetMethod)
        {
            // Method expression: Type.Method → static wrapper func(receiver, args...) returns
            // The target method IS already static (receiver as first param), so we can use it directly.
            var delegateType = _ctx.Mapper.Map(mv.FunctionType);
            _ctx.IL.Emit(OpCodes.Ldnull);
            _ctx.IL.Emit(OpCodes.Ldftn, targetMethod.AsMethodRef());
            var delegateCtor = _ctx.Definitions.GetConstructor(delegateType, new[] { typeof(object), typeof(IntPtr) });
            if (delegateCtor == null)
            {
                throw new InvalidOperationException(
                    $"Cannot find delegate constructor on '{delegateType.FullName ?? delegateType.Name}' " +
                    $"for method expression '{mv.Method.Name}' (funcType={mv.FunctionType?.Name})");
            }
            _ctx.IL.Emit(OpCodes.Newobj, delegateCtor);
        }

        public List<Symbol> FindCaptures(BlockStatement body)
        {
            var allRefs = new HashSet<Symbol>();
            CollectReferencedSymbols(body, allRefs);

            var captures = new List<Symbol>();
            foreach (var sym in allRefs)
            {
                if (_ctx.Locals.ContainsKey(sym) || _ctx.Parameters.ContainsKey(sym))
                    captures.Add(sym);
            }

            return captures;
        }

        private LocalSlot ResolveLocal(Symbol sym)
        {
            if (_ctx.Locals.TryGetValue(sym, out var local))
            {
                return local;
            }
            foreach (var kvp in _ctx.Locals)
            {
                if (kvp.Key.Name == sym.Name && kvp.Key.Kind == sym.Kind)
                {
                    return kvp.Value;
                }
            }
            throw new InvalidOperationException(
                $"Cannot find local for captured symbol '{sym.Name}' ({sym.Kind})");
        }

        private void EmitClosureTrailingReturn(FunctionLiteralExpression funcLit)
        {
            if (funcLit.ReturnTypes.Count == 0)
            {
                _ctx.IL.Emit(OpCodes.Ret);
                return;
            }

            // Non-void closure: emit trailing return as safety net.
            // If all code paths return, this is unreachable, but it
            // keeps the IL valid so the verifier sees balanced stacks.
            if (funcLit.NamedReturns.Count > 0)
            {
                _body.EmitNamedReturnValues();
            }
            else
            {
                var closureRetType = _ctx.Mapper.MapReturnType(funcLit.ReturnTypes);
                if (closureRetType.IsValueType)
                {
                    var defaultLocal = _ctx.IL.DeclareLocal(closureRetType);
                    _ctx.IL.Emit(OpCodes.Ldloca, defaultLocal);
                    _ctx.IL.Emit(OpCodes.Initobj, closureRetType);
                    _ctx.IL.Emit(OpCodes.Ldloc, defaultLocal);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldnull);
                }
            }
            _ctx.IL.Emit(OpCodes.Ret);
        }

        public HashSet<Symbol> CollectAllCaptures(BlockStatement body)
        {
            var literals = new List<FunctionLiteralExpression>();
            FindFunctionLiterals(body, literals);

            var captured = new HashSet<Symbol>();
            foreach (var lit in literals)
            {
                var refs = new HashSet<Symbol>();
                CollectReferencedSymbols(lit.Body, refs);
                var ownParams = new HashSet<Symbol>(lit.Parameters.Cast<Symbol>());
                foreach (var sym in refs)
                {
                    if ((sym is LocalSymbol || sym is ParameterSymbol) && !ownParams.Contains(sym))
                        captured.Add(sym);
                }
            }

            return captured;
        }

        private static void FindFunctionLiterals(AstNode node, List<FunctionLiteralExpression> result)
        {
            switch (node)
            {
                case FunctionLiteralExpression funcLit:
                    result.Add(funcLit);
                    return; // Don't recurse into nested closures
                case BlockStatement block:
                    foreach (var s in block.Statements) FindFunctionLiterals(s, result);
                    return;
                case ExpressionStatement es:
                    FindFunctionLiterals(es.Expression, result);
                    return;
                case ReturnStatement ret:
                    foreach (var v in ret.Values) FindFunctionLiterals(v, result);
                    return;
                case BinaryExpression bin:
                    FindFunctionLiterals(bin.Left, result);
                    FindFunctionLiterals(bin.Right, result);
                    return;
                case UnaryExpression un:
                    FindFunctionLiterals(un.Operand, result);
                    return;
                case CallExpression call:
                    if (call.CallTarget != null) FindFunctionLiterals(call.CallTarget, result);
                    foreach (var a in call.Arguments) FindFunctionLiterals(a, result);
                    return;
                case MethodCallExpression mc:
                    FindFunctionLiterals(mc.Receiver, result);
                    foreach (var a in mc.Arguments) FindFunctionLiterals(a, result);
                    return;
                case VarDeclaration vd:
                    if (vd.Initializer != null) FindFunctionLiterals(vd.Initializer, result);
                    return;
                case MultiVarDeclaration mvd:
                    FindFunctionLiterals(mvd.Initializer, result);
                    return;
                case AssignmentStatement assign:
                    FindFunctionLiterals(assign.Target, result);
                    FindFunctionLiterals(assign.Value, result);
                    return;
                case ParallelAssignmentStatement parallel:
                    foreach (var t in parallel.Targets)
                        if (t != null) FindFunctionLiterals(t, result);
                    foreach (var v in parallel.Values) FindFunctionLiterals(v, result);
                    return;
                case IfStatement ifStmt:
                    if (ifStmt.Init != null) FindFunctionLiterals(ifStmt.Init, result);
                    FindFunctionLiterals(ifStmt.Condition, result);
                    FindFunctionLiterals(ifStmt.Body, result);
                    if (ifStmt.ElseBody != null) FindFunctionLiterals(ifStmt.ElseBody, result);
                    return;
                case ForStatement fs:
                    if (fs.Init != null) FindFunctionLiterals(fs.Init, result);
                    if (fs.Condition != null) FindFunctionLiterals(fs.Condition, result);
                    if (fs.Post != null) FindFunctionLiterals(fs.Post, result);
                    FindFunctionLiterals(fs.Body, result);
                    return;
                case ForRangeStatement fr:
                    FindFunctionLiterals(fr.Iterable, result);
                    FindFunctionLiterals(fr.Body, result);
                    return;
                case IncDecStatement inc:
                    FindFunctionLiterals(inc.Operand, result);
                    return;
                case SelectorExpression sel:
                    FindFunctionLiterals(sel.Target, result);
                    return;
                case IndexExpression idx:
                    FindFunctionLiterals(idx.Target, result);
                    FindFunctionLiterals(idx.Index, result);
                    return;
                case ConversionExpression conv:
                    FindFunctionLiterals(conv.Operand, result);
                    return;
                case AddressOfExpression addr:
                    FindFunctionLiterals(addr.Operand, result);
                    return;
                case DerefExpression deref:
                    FindFunctionLiterals(deref.Operand, result);
                    return;
                case TypeAssertExpression ta:
                    FindFunctionLiterals(ta.Expression, result);
                    return;
                case CompositeLiteralExpression lit:
                    if (lit.Initializers != null)
                    {
                        foreach (var init in lit.Initializers)
                        {
                            FindFunctionLiterals(init.Value, result);
                        }
                    }
                    if (lit.Elements != null)
                    {
                        foreach (var element in lit.Elements)
                        {
                            if (element.Key != null)
                            {
                                FindFunctionLiterals(element.Key, result);
                            }
                            FindFunctionLiterals(element.Value, result);
                        }
                    }
                    return;
                case SwitchStatement sw:
                    if (sw.Init != null) FindFunctionLiterals(sw.Init, result);
                    if (sw.Tag != null) FindFunctionLiterals(sw.Tag, result);
                    foreach (var c in sw.Cases)
                    {
                        if (c.Expressions != null)
                            foreach (var e in c.Expressions) FindFunctionLiterals(e, result);
                        foreach (var s in c.Body) FindFunctionLiterals(s, result);
                    }
                    return;
                case DeferStatement defer:
                    FindFunctionLiterals(defer.Call, result);
                    return;
                case GoStatement go:
                    FindFunctionLiterals(go.Call, result);
                    return;
                case SendStatement send:
                    FindFunctionLiterals(send.Channel, result);
                    FindFunctionLiterals(send.Value, result);
                    return;
                case ReceiveExpression recv:
                    FindFunctionLiterals(recv.Channel, result);
                    return;
                case SliceExpression slice:
                    FindFunctionLiterals(slice.Operand, result);
                    if (slice.Low != null)
                    {
                        FindFunctionLiterals(slice.Low, result);
                    }
                    if (slice.High != null)
                    {
                        FindFunctionLiterals(slice.High, result);
                    }
                    if (slice.Max != null)
                    {
                        FindFunctionLiterals(slice.Max, result);
                    }
                    return;
                case MultiAssignmentStatement multi:
                    foreach (var target in multi.Targets)
                    {
                        if (target != null)
                        {
                            FindFunctionLiterals(target, result);
                        }
                    }
                    FindFunctionLiterals(multi.Value, result);
                    return;
                case LabeledStatement labeled:
                    FindFunctionLiterals(labeled.InnerStatement, result);
                    return;
                case TypeSwitchStatement typeSwitch:
                    if (typeSwitch.Init != null)
                    {
                        FindFunctionLiterals(typeSwitch.Init, result);
                    }
                    FindFunctionLiterals(typeSwitch.GuardExpression, result);
                    foreach (var typeCase in typeSwitch.Cases)
                    {
                        foreach (var statement in typeCase.Body)
                        {
                            FindFunctionLiterals(statement, result);
                        }
                    }
                    return;
                case SelectStatement select:
                    foreach (var selectCase in select.Cases)
                    {
                        if (selectCase.Channel != null)
                        {
                            FindFunctionLiterals(selectCase.Channel, result);
                        }
                        if (selectCase.SendValue != null)
                        {
                            FindFunctionLiterals(selectCase.SendValue, result);
                        }
                        foreach (var statement in selectCase.Body)
                        {
                            FindFunctionLiterals(statement, result);
                        }
                    }
                    return;
            }
        }

        private static void CollectReferencedSymbols(AstNode node, HashSet<Symbol> result)
        {
            switch (node)
            {
                case IdentifierExpression id:
                    result.Add(id.Symbol);
                    return;
                case BlockStatement block:
                    foreach (var s in block.Statements) CollectReferencedSymbols(s, result);
                    return;
                case ExpressionStatement es:
                    CollectReferencedSymbols(es.Expression, result);
                    return;
                case ReturnStatement ret:
                    foreach (var v in ret.Values) CollectReferencedSymbols(v, result);
                    return;
                case BinaryExpression bin:
                    CollectReferencedSymbols(bin.Left, result);
                    CollectReferencedSymbols(bin.Right, result);
                    return;
                case UnaryExpression un:
                    CollectReferencedSymbols(un.Operand, result);
                    return;
                case CallExpression call:
                    if (call.CallTarget != null) CollectReferencedSymbols(call.CallTarget, result);
                    foreach (var a in call.Arguments) CollectReferencedSymbols(a, result);
                    return;
                case MethodCallExpression mc:
                    CollectReferencedSymbols(mc.Receiver, result);
                    foreach (var a in mc.Arguments) CollectReferencedSymbols(a, result);
                    return;
                case VarDeclaration vd:
                    if (vd.Initializer != null) CollectReferencedSymbols(vd.Initializer, result);
                    return;
                case MultiVarDeclaration mvd2:
                    CollectReferencedSymbols(mvd2.Initializer, result);
                    return;
                case AssignmentStatement assign:
                    CollectReferencedSymbols(assign.Target, result);
                    CollectReferencedSymbols(assign.Value, result);
                    return;
                case ParallelAssignmentStatement parallel:
                    foreach (var t in parallel.Targets)
                        if (t != null) CollectReferencedSymbols(t, result);
                    foreach (var v in parallel.Values) CollectReferencedSymbols(v, result);
                    return;
                case IfStatement ifStmt:
                    if (ifStmt.Init != null) CollectReferencedSymbols(ifStmt.Init, result);
                    CollectReferencedSymbols(ifStmt.Condition, result);
                    CollectReferencedSymbols(ifStmt.Body, result);
                    if (ifStmt.ElseBody != null) CollectReferencedSymbols(ifStmt.ElseBody, result);
                    return;
                case ForStatement fs:
                    if (fs.Init != null) CollectReferencedSymbols(fs.Init, result);
                    if (fs.Condition != null) CollectReferencedSymbols(fs.Condition, result);
                    if (fs.Post != null) CollectReferencedSymbols(fs.Post, result);
                    CollectReferencedSymbols(fs.Body, result);
                    return;
                case IncDecStatement inc:
                    CollectReferencedSymbols(inc.Operand, result);
                    return;
                case SelectorExpression sel:
                    CollectReferencedSymbols(sel.Target, result);
                    return;
                case IndexExpression idx:
                    CollectReferencedSymbols(idx.Target, result);
                    CollectReferencedSymbols(idx.Index, result);
                    return;
                case ConversionExpression conv:
                    CollectReferencedSymbols(conv.Operand, result);
                    return;
                case AddressOfExpression addr:
                    CollectReferencedSymbols(addr.Operand, result);
                    return;
                case DerefExpression deref:
                    CollectReferencedSymbols(deref.Operand, result);
                    return;
                case TypeAssertExpression ta:
                    CollectReferencedSymbols(ta.Expression, result);
                    return;
                case CompositeLiteralExpression lit:
                    if (lit.Initializers != null)
                    {
                        foreach (var init in lit.Initializers)
                        {
                            CollectReferencedSymbols(init.Value, result);
                        }
                    }
                    if (lit.Elements != null)
                    {
                        foreach (var element in lit.Elements)
                        {
                            if (element.Key != null)
                            {
                                CollectReferencedSymbols(element.Key, result);
                            }
                            CollectReferencedSymbols(element.Value, result);
                        }
                    }
                    return;
                case SwitchStatement sw:
                    if (sw.Init != null) CollectReferencedSymbols(sw.Init, result);
                    if (sw.Tag != null) CollectReferencedSymbols(sw.Tag, result);
                    foreach (var c in sw.Cases)
                    {
                        if (c.Expressions != null)
                            foreach (var e in c.Expressions) CollectReferencedSymbols(e, result);
                        foreach (var s in c.Body) CollectReferencedSymbols(s, result);
                    }
                    return;
                case DeferStatement defer:
                    CollectReferencedSymbols(defer.Call, result);
                    return;
                case GoStatement go:
                    CollectReferencedSymbols(go.Call, result);
                    return;
                case SendStatement send:
                    CollectReferencedSymbols(send.Channel, result);
                    CollectReferencedSymbols(send.Value, result);
                    return;
                case ReceiveExpression recv:
                    CollectReferencedSymbols(recv.Channel, result);
                    return;
                case SliceExpression slice:
                    CollectReferencedSymbols(slice.Operand, result);
                    if (slice.Low != null) CollectReferencedSymbols(slice.Low, result);
                    if (slice.High != null) CollectReferencedSymbols(slice.High, result);
                    if (slice.Max != null) CollectReferencedSymbols(slice.Max, result);
                    return;
                case ForRangeStatement range:
                    CollectReferencedSymbols(range.Iterable, result);
                    CollectReferencedSymbols(range.Body, result);
                    return;
                case MultiAssignmentStatement multi:
                    foreach (var target in multi.Targets)
                    {
                        if (target != null)
                        {
                            CollectReferencedSymbols(target, result);
                        }
                    }
                    CollectReferencedSymbols(multi.Value, result);
                    return;
                case LabeledStatement labeled:
                    CollectReferencedSymbols(labeled.InnerStatement, result);
                    return;
                case TypeSwitchStatement typeSwitch:
                    if (typeSwitch.Init != null)
                    {
                        CollectReferencedSymbols(typeSwitch.Init, result);
                    }
                    CollectReferencedSymbols(typeSwitch.GuardExpression, result);
                    foreach (var typeCase in typeSwitch.Cases)
                    {
                        foreach (var statement in typeCase.Body)
                        {
                            CollectReferencedSymbols(statement, result);
                        }
                    }
                    return;
                case SelectStatement select:
                    foreach (var selectCase in select.Cases)
                    {
                        if (selectCase.Channel != null)
                        {
                            CollectReferencedSymbols(selectCase.Channel, result);
                        }
                        if (selectCase.SendValue != null)
                        {
                            CollectReferencedSymbols(selectCase.SendValue, result);
                        }
                        foreach (var statement in selectCase.Body)
                        {
                            CollectReferencedSymbols(statement, result);
                        }
                    }
                    return;
                case FunctionLiteralExpression funcLit:
                    CollectReferencedSymbols(funcLit.Body, result);
                    return;
            }
        }
    }
}

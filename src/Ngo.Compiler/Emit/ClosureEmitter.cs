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

            var paramTypes = new Type[funcLit.Parameters.Count];
            for (int i = 0; i < funcLit.Parameters.Count; i++)
            {
                paramTypes[i] = _ctx.Mapper.Map(funcLit.Parameters[i].Type);
            }

            var returnType = _ctx.Mapper.MapReturnType(funcLit.ReturnTypes);

            var lambdaMethod = _ctx.PackageType.DefineMethod(
                lambdaName,
                MethodAttributes.Private | MethodAttributes.Static,
                returnType,
                paramTypes);

            for (int i = 0; i < funcLit.Parameters.Count; i++)
            {
                lambdaMethod.DefineParameter(i + 1, ParameterAttributes.None, funcLit.Parameters[i].Name);
            }

            // Save current method state
            var savedIL = _ctx.IL;
            var savedLocals = new Dictionary<Symbol, LocalBuilder>(_ctx.Locals);
            var savedParams = new Dictionary<Symbol, int>(_ctx.Parameters);
            var savedCaptured = new HashSet<Symbol>(_ctx.CapturedSymbols);
            var savedReturnTypes = _body.CurrentReturnTypes;

            // Emit the lambda body
            _ctx.IL = lambdaMethod.GetILWriter();
            _ctx.Locals.Clear();
            _ctx.Parameters.Clear();
            _ctx.CapturedSymbols.Clear();
            _body.CurrentReturnTypes = funcLit.ReturnTypes;

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
                if (funcLit.ReturnTypes.Count == 0)
                {
                    _ctx.IL.Emit(OpCodes.Ret);
                }
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
            _ctx.IL.Emit(OpCodes.Ldftn, lambdaMethod.AsMethodInfo());
            var delegateCtor = EmitContext.GetConstructorSafe(delegateType, new[] { typeof(object), typeof(IntPtr) });
            _ctx.IL.Emit(OpCodes.Newobj, delegateCtor);
        }

        private void EmitClosureLiteral(FunctionLiteralExpression funcLit, List<Symbol> captures)
        {
            var closureName = _ctx.QualifyWithPackage($"__closure_{_body.LambdaCounter++}");

            var closureBuilder = _ctx.Module.DefineType(
                closureName,
                TypeAttributes.Public | TypeAttributes.Sealed);

            // Propagate generic parameters from the enclosing function to the closure type.
            // Without this, parameter types like Func<E, bool> serialize as references to 'E'
            // which can't be resolved at link time since the closure type isn't generic.
            if (_ctx.EnclosingGenericParamNames.Length > 0)
            {
                closureBuilder.DefineGenericParameters(_ctx.EnclosingGenericParamNames);
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
                captureFields[sym] = closureBuilder.DefineField(
                    sym.Name, fieldType, FieldAttributes.Public);
            }

            // Instance method for the lambda body
            var paramTypes = new Type[funcLit.Parameters.Count];
            for (int i = 0; i < funcLit.Parameters.Count; i++)
                paramTypes[i] = _ctx.Mapper.Map(funcLit.Parameters[i].Type);

            var returnType = _ctx.Mapper.MapReturnType(funcLit.ReturnTypes);

            var invokeMethod = closureBuilder.DefineMethod(
                "Invoke",
                MethodAttributes.Public,
                returnType,
                paramTypes);

            for (int i = 0; i < funcLit.Parameters.Count; i++)
                invokeMethod.DefineParameter(i + 1, ParameterAttributes.None, funcLit.Parameters[i].Name);

            // Save current method state
            var savedIL = _ctx.IL;
            var savedLocals = new Dictionary<Symbol, LocalBuilder>(_ctx.Locals);
            var savedParams = new Dictionary<Symbol, int>(_ctx.Parameters);
            var savedCaptured2 = new HashSet<Symbol>(_ctx.CapturedSymbols);
            var savedReturnTypes = _body.CurrentReturnTypes;

            // Emit the invoke method body
            _ctx.IL = invokeMethod.GetILWriter();
            _ctx.Locals.Clear();
            _ctx.Parameters.Clear();
            _ctx.CapturedSymbols.Clear();
            _body.CurrentReturnTypes = funcLit.ReturnTypes;

            // Lambda params start at arg 1 (arg 0 is 'this')
            for (int i = 0; i < funcLit.Parameters.Count; i++)
                _ctx.Parameters[funcLit.Parameters[i]] = i + 1;

            // Load captured Box<T> references from closure fields into locals
            foreach (var (sym, field) in captureFields)
            {
                var local = _ctx.IL.DeclareLocal(field.AsFieldInfo().FieldType); // Box<T>
                _ctx.Locals[sym] = local;
                _ctx.CapturedSymbols.Add(sym); // Enable .Value access in EmitLoad/EmitStore
                _ctx.IL.Emit(OpCodes.Ldarg_0);
                _ctx.IL.Emit(OpCodes.Ldfld, field.AsFieldInfo());
                _ctx.IL.Emit(OpCodes.Stloc, local);
            }

            // Pre-scan for nested closures that capture this closure's parameters
            var innerCaptures = CollectAllCaptures(funcLit.Body);
            foreach (var sym in innerCaptures)
                _ctx.CapturedSymbols.Add(sym);
            _body.BoxCapturedParameters(funcLit.Parameters);

            var savedDeferStack2 = _ctx.DeferStack;
            var savedDeferReturnLocal2 = _ctx.DeferReturnLocal;
            _ctx.DeferStack = null;
            _ctx.DeferReturnLocal = null;

            if (MethodBodyEmitter.ContainsDefer(funcLit.Body))
            {
                var closureIsVoid = funcLit.ReturnTypes.Count == 0;
                var closureRetType = closureIsVoid ? null : _ctx.Mapper.MapReturnType(funcLit.ReturnTypes);
                _body.EmitDeferWrappedBody(funcLit.Body, closureIsVoid, closureRetType);
            }
            else
            {
                _body.EmitBlock(funcLit.Body);
                if (funcLit.ReturnTypes.Count == 0)
                    _ctx.IL.Emit(OpCodes.Ret);
            }

            _ctx.DeferStack = savedDeferStack2;
            _ctx.DeferReturnLocal = savedDeferReturnLocal2;

            // Restore method state
            _ctx.IL = savedIL;
            _ctx.Locals.Clear();
            foreach (var kvp in savedLocals) _ctx.Locals[kvp.Key] = kvp.Value;
            _ctx.Parameters.Clear();
            foreach (var kvp in savedParams) _ctx.Parameters[kvp.Key] = kvp.Value;
            _ctx.CapturedSymbols.Clear();
            foreach (var sym in savedCaptured2) _ctx.CapturedSymbols.Add(sym);
            _body.CurrentReturnTypes = savedReturnTypes;

            // Finalize closure type
            var closureType = closureBuilder.CreateType()!;

            // Create closure instance and populate captured fields
            var closureLocal = _ctx.IL.DeclareLocal(closureType);
            var ctor = closureType.GetConstructor(Type.EmptyTypes)!;
            _ctx.IL.Emit(OpCodes.Newobj, ctor);
            _ctx.IL.Emit(OpCodes.Stloc, closureLocal);

            foreach (var sym in captures)
            {
                var runtimeField = closureType.GetField(sym.Name)!;
                _ctx.IL.Emit(OpCodes.Ldloc, closureLocal);
                // Load Box<T> reference directly (bypass EmitLoad which would unwrap .Value)
                var captureLocal = ResolveLocal(sym);
                _ctx.IL.Emit(OpCodes.Ldloc, captureLocal);
                _ctx.IL.Emit(OpCodes.Stfld, runtimeField);
            }

            // Create delegate from closure instance + invoke method
            var delegateType = _ctx.Mapper.Map(funcLit.FunctionType);
            if (delegateType == typeof(Delegate) || delegateType == typeof(object))
            {
                delegateType = funcLit.FunctionType.ReturnTypes.Count == 0
                    ? typeof(Action) : typeof(Func<object>);
            }
            MethodInfo runtimeMethod;
            if (closureType is TypeBuilder)
            {
                runtimeMethod = new Builder.NgoProxyMethodInfo(closureType, "Invoke");
            }
            else
            {
                runtimeMethod = closureType.GetMethod("Invoke")
                    ?? new Builder.NgoProxyMethodInfo(closureType, "Invoke");
            }
            _ctx.IL.Emit(OpCodes.Ldloc, closureLocal);
            _ctx.IL.Emit(OpCodes.Ldftn, runtimeMethod);
            var delegateCtor = EmitContext.GetConstructorSafe(delegateType, new[] { typeof(object), typeof(IntPtr) });
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

            var receiverField = closureBuilder.DefineField(
                "_receiver", receiverClrType, FieldAttributes.Public);

            // Build Invoke method: same params as the method (without receiver), same return
            var paramTypes = new Type[mv.Method.Parameters.Count];
            for (int i = 0; i < mv.Method.Parameters.Count; i++)
                paramTypes[i] = _ctx.Mapper.Map(mv.Method.Parameters[i].Type);

            var returnType = _ctx.Mapper.MapReturnType(mv.Method.ReturnTypes);

            var invokeMethod = closureBuilder.DefineMethod(
                "Invoke",
                MethodAttributes.Public,
                returnType,
                paramTypes);

            // Emit Invoke body: load receiver from field, load params, call target method
            var invokeIL = invokeMethod.GetILWriter();
            invokeIL.Emit(OpCodes.Ldarg_0);
            invokeIL.Emit(OpCodes.Ldfld, receiverField.AsFieldInfo());
            for (int i = 0; i < paramTypes.Length; i++)
                invokeIL.Emit(OpCodes.Ldarg, i + 1);
            invokeIL.Emit(OpCodes.Call, targetMethod.AsMethodInfo());
            invokeIL.Emit(OpCodes.Ret);

            var closureType = closureBuilder.CreateType()!;

            // Create closure instance, set receiver field
            var closureLocal = _ctx.IL.DeclareLocal(closureType);
            var ctor = closureType.GetConstructor(Type.EmptyTypes)!;
            _ctx.IL.Emit(OpCodes.Newobj, ctor);
            _ctx.IL.Emit(OpCodes.Stloc, closureLocal);

            _ctx.IL.Emit(OpCodes.Ldloc, closureLocal);
            _body.EmitExpression(mv.Receiver);
            var runtimeField = closureType.GetField("_receiver")!;
            _ctx.IL.Emit(OpCodes.Stfld, runtimeField);

            // Create delegate from closure + Invoke
            var delegateType = _ctx.Mapper.Map(mv.FunctionType);
            var runtimeMethod = closureType.GetMethod("Invoke")!;
            _ctx.IL.Emit(OpCodes.Ldloc, closureLocal);
            _ctx.IL.Emit(OpCodes.Ldftn, runtimeMethod);
            var delegateCtor = EmitContext.GetConstructorSafe(delegateType, new[] { typeof(object), typeof(IntPtr) });
            _ctx.IL.Emit(OpCodes.Newobj, delegateCtor);
        }

        private void EmitMethodExpression(MethodValueExpression mv, IMethodBuilder targetMethod)
        {
            // Method expression: Type.Method → static wrapper func(receiver, args...) returns
            // The target method IS already static (receiver as first param), so we can use it directly.
            var delegateType = _ctx.Mapper.Map(mv.FunctionType);
            _ctx.IL.Emit(OpCodes.Ldnull);
            _ctx.IL.Emit(OpCodes.Ldftn, targetMethod.AsMethodInfo());
            var delegateCtor = EmitContext.GetConstructorSafe(delegateType, new[] { typeof(object), typeof(IntPtr) });
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

        private LocalBuilder ResolveLocal(Symbol sym)
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
                        foreach (var init in lit.Initializers) FindFunctionLiterals(init.Value, result);
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
                        foreach (var init in lit.Initializers) CollectReferencedSymbols(init.Value, result);
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
                    CollectReferencedSymbols(multi.Value, result);
                    return;
                case LabeledStatement labeled:
                    CollectReferencedSymbols(labeled.InnerStatement, result);
                    return;
                // Recurse into nested function literals so transitive captures are visible
                case FunctionLiteralExpression funcLit:
                    CollectReferencedSymbols(funcLit.Body, result);
                    return;
            }
        }
    }
}

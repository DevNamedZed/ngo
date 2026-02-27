// -----------------------------------------------------------------------
// <copyright file="DeferGoEmitter.cs" company="Ziad">
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

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Emits IL for defer, go, send, receive, and select statements.
    /// </summary>
    internal sealed class DeferGoEmitter
    {
        private readonly EmitContext _ctx;
        private readonly MethodBodyEmitter _body;

        public DeferGoEmitter(EmitContext ctx, MethodBodyEmitter body)
        {
            _ctx = ctx;
            _body = body;
        }

        public void EmitDeferWrappedBody(BlockStatement body, bool isVoid, Type? returnType = null,
            IReadOnlyList<LocalSymbol>? namedReturns = null)
        {
            // Create DeferStack local
            var deferStackLocal = _ctx.IL.DeclareLocal(typeof(DeferStack));
            _ctx.IL.Emit(OpCodes.Newobj, typeof(DeferStack).GetConstructor(Type.EmptyTypes)!);
            _ctx.IL.Emit(OpCodes.Stloc, deferStackLocal);
            _ctx.DeferStack = deferStackLocal;

            // For non-void functions: create a local to hold the return value,
            // since ret inside a try-catch block is not valid IL.
            var exitLabel = _ctx.IL.DefineLabel();
            if (!isVoid && returnType != null)
            {
                _ctx.DeferReturnLocal = _ctx.IL.DeclareLocal(returnType);
                _ctx.DeferExitLabel = exitLabel;
            }

            // try { body }
            // catch (GoPanicException ex) {
            //     if (deferStack.ExecuteWithRecover(ex) == null) throw;
            // }
            _ctx.IL.BeginExceptionBlock();
            _body.EmitBlock(body);

            // catch (GoPanicException) — enables recover() in deferred functions
            _ctx.IL.BeginCatchBlock(typeof(GoPanicException));
            var panicExLocal = _ctx.IL.DeclareLocal(typeof(GoPanicException));
            _ctx.IL.Emit(OpCodes.Stloc, panicExLocal);
            _ctx.IL.Emit(OpCodes.Ldloc, deferStackLocal);
            _ctx.IL.Emit(OpCodes.Ldloc, panicExLocal);
            _ctx.IL.Emit(OpCodes.Callvirt,
                typeof(DeferStack).GetMethod("ExecuteWithRecover")!);
            var recoveredLocal = _ctx.IL.DeclareLocal(typeof(object));
            _ctx.IL.Emit(OpCodes.Stloc, recoveredLocal);
            _ctx.IL.Emit(OpCodes.Ldloc, recoveredLocal);
            var recoveredLabel = _ctx.IL.DefineLabel();
            _ctx.IL.Emit(OpCodes.Brtrue, recoveredLabel);
            _ctx.IL.Emit(OpCodes.Rethrow);
            _ctx.IL.MarkLabel(recoveredLabel);

            // catch (DivideByZeroException) — wrap as GoPanicException for recover()
            _ctx.IL.BeginCatchBlock(typeof(DivideByZeroException));
            _ctx.IL.Emit(OpCodes.Pop); // discard the DivideByZeroException
            _ctx.IL.Emit(OpCodes.Ldstr, "runtime error: integer divide by zero");
            _ctx.IL.Emit(OpCodes.Newobj,
                typeof(GoPanicException).GetConstructor(new[] { typeof(object) })!);
            var divZeroExLocal = _ctx.IL.DeclareLocal(typeof(GoPanicException));
            _ctx.IL.Emit(OpCodes.Stloc, divZeroExLocal);
            _ctx.IL.Emit(OpCodes.Ldloc, deferStackLocal);
            _ctx.IL.Emit(OpCodes.Ldloc, divZeroExLocal);
            _ctx.IL.Emit(OpCodes.Callvirt,
                typeof(DeferStack).GetMethod("ExecuteWithRecover")!);
            var recoveredLocal2 = _ctx.IL.DeclareLocal(typeof(object));
            _ctx.IL.Emit(OpCodes.Stloc, recoveredLocal2);
            _ctx.IL.Emit(OpCodes.Ldloc, recoveredLocal2);
            var recoveredLabel2 = _ctx.IL.DefineLabel();
            _ctx.IL.Emit(OpCodes.Brtrue, recoveredLabel2);
            _ctx.IL.Emit(OpCodes.Rethrow);
            _ctx.IL.MarkLabel(recoveredLabel2);

            _ctx.IL.EndExceptionBlock();

            _ctx.IL.MarkLabel(exitLabel);

            // After exception block: run remaining defers for the normal (non-panic) path.
            // If the catch path already ran ExecuteWithRecover, the stack is empty (no-op).
            _ctx.IL.Emit(OpCodes.Ldloc, deferStackLocal);
            _ctx.IL.Emit(OpCodes.Callvirt, typeof(DeferStack).GetMethod("Execute")!);

            if (!isVoid && namedReturns != null && namedReturns.Count > 0)
            {
                // Reload from named return locals (may have been modified by deferred functions)
                _body.EmitNamedReturnValues();
                _ctx.IL.Emit(OpCodes.Ret);
            }
            else if (!isVoid && _ctx.DeferReturnLocal != null)
            {
                _ctx.IL.Emit(OpCodes.Ldloc, _ctx.DeferReturnLocal);
                _ctx.IL.Emit(OpCodes.Ret);
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Ret);
            }

            _ctx.DeferReturnLocal = null;
        }

        public void EmitDefer(DeferStatement defer)
        {
            if (_ctx.DeferStack == null)
                throw new InvalidOperationException("Defer statement outside of defer-wrapped function");

            // defer f(args) → deferStack.Push(Action wrapping the call)
            // Generate a lambda method for the deferred call
            EmitDeferOrGoCall(defer.Call, isDeferPush: true);
        }

        public void EmitGo(GoStatement goStmt)
        {
            // go f(args) → Goroutine.Go(Action wrapping the call)
            EmitDeferOrGoCall(goStmt.Call, isDeferPush: false);
        }

        public void EmitSend(SendStatement send)
        {
            var chanType = (ChannelTypeSymbol)send.Channel.Type;
            var elemClrType = _ctx.Mapper.Map(chanType.ElementType);
            var chanClrType = _ctx.Mapper.Map(chanType);

            _body.EmitExpression(send.Channel);
            _body.EmitExpression(send.Value);

            var sendMethod = chanClrType.GetMethod("Send", new[] { elemClrType })!;
            _ctx.IL.Emit(OpCodes.Call, sendMethod);
        }

        public void EmitReceive(ReceiveExpression recv)
        {
            var chanType = (ChannelTypeSymbol)recv.Channel.Type;
            var elemClrType = _ctx.Mapper.Map(chanType.ElementType);
            var chanClrType = _ctx.Mapper.Map(chanType);

            _body.EmitExpression(recv.Channel);

            // Channel<T>.Receive() returns (T value, bool ok)
            var receiveMethod = chanClrType.GetMethod("Receive")!;
            _ctx.IL.Emit(OpCodes.Call, receiveMethod);

            if (recv.IsCommaOk)
            {
                // val, ok := <-ch — keep the full (T, bool) tuple on stack
                return;
            }

            // Extract Item1 (the value)
            var tupleType = typeof(ValueTuple<,>).MakeGenericType(elemClrType, typeof(bool));
            var tupleLocal = _ctx.IL.DeclareLocal(tupleType);
            _ctx.IL.Emit(OpCodes.Stloc, tupleLocal);
            _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
            _ctx.IL.Emit(OpCodes.Ldfld, tupleType.GetField("Item1")!);
        }

        public void EmitSelectStatement(SelectStatement select)
        {
            var endLabel = _ctx.IL.DefineLabel();
            int caseCount = select.Cases.Count;

            // Separate default from comm cases
            int defaultIndex = -1;
            for (int i = 0; i < caseCount; i++)
            {
                if (select.Cases[i].IsDefault)
                {
                    defaultIndex = i;
                    break;
                }
            }

            // Phase 1: Evaluate all channel expressions and send values, store in locals
            var chanLocals = new LocalBuilder?[caseCount];
            var sendValLocals = new LocalBuilder?[caseCount];

            for (int i = 0; i < caseCount; i++)
            {
                var sc = select.Cases[i];
                if (sc.IsDefault) continue;

                var chanType = (ChannelTypeSymbol)sc.Channel!.Type;
                var chanClrType = _ctx.Mapper.Map(chanType);

                _body.EmitExpression(sc.Channel);
                chanLocals[i] = _ctx.IL.DeclareLocal(chanClrType);
                _ctx.IL.Emit(OpCodes.Stloc, chanLocals[i]!);

                if (sc.Kind == SelectCaseKind.Send)
                {
                    var elemClrType = _ctx.Mapper.Map(chanType.ElementType);
                    _body.EmitExpression(sc.SendValue!);
                    sendValLocals[i] = _ctx.IL.DeclareLocal(elemClrType);
                    _ctx.IL.Emit(OpCodes.Stloc, sendValLocals[i]!);
                }
            }

            // Phase 2: Polling loop
            var loopLabel = _ctx.IL.DefineLabel();
            var bodyLabels = new Label[caseCount];
            for (int i = 0; i < caseCount; i++)
            {
                bodyLabels[i] = _ctx.IL.DefineLabel();
            }

            _ctx.IL.MarkLabel(loopLabel);

            // Try each non-default case
            for (int i = 0; i < caseCount; i++)
            {
                var sc = select.Cases[i];
                if (sc.IsDefault) continue;

                var chanType = (ChannelTypeSymbol)sc.Channel!.Type;
                var elemClrType = _ctx.Mapper.Map(chanType.ElementType);
                var chanClrType = _ctx.Mapper.Map(chanType);

                if (sc.Kind == SelectCaseKind.Send)
                {
                    // TrySend(value) → bool
                    _ctx.IL.Emit(OpCodes.Ldloc, chanLocals[i]!);
                    _ctx.IL.Emit(OpCodes.Ldloc, sendValLocals[i]!);
                    var trySendMethod = chanClrType.GetMethod("TrySend", new[] { elemClrType })!;
                    _ctx.IL.Emit(OpCodes.Call, trySendMethod);
                    _ctx.IL.Emit(OpCodes.Brtrue, bodyLabels[i]);
                }
                else
                {
                    // TryReceive() → (T value, bool ok, bool completed)
                    _ctx.IL.Emit(OpCodes.Ldloc, chanLocals[i]!);
                    var tryRecvMethod = chanClrType.GetMethod("TryReceive", Type.EmptyTypes)!;
                    _ctx.IL.Emit(OpCodes.Call, tryRecvMethod);

                    var tripleType = typeof(ValueTuple<,,>).MakeGenericType(elemClrType, typeof(bool), typeof(bool));
                    var tripleLocal = _ctx.IL.DeclareLocal(tripleType);
                    _ctx.IL.Emit(OpCodes.Stloc, tripleLocal);

                    // Check Item3 (completed)
                    _ctx.IL.Emit(OpCodes.Ldloca, tripleLocal);
                    _ctx.IL.Emit(OpCodes.Ldfld, tripleType.GetField("Item3")!);
                    var notCompletedLabel = _ctx.IL.DefineLabel();
                    _ctx.IL.Emit(OpCodes.Brfalse, notCompletedLabel);

                    // Completed — store value and ok if locals exist
                    if (sc.ValueLocal != null)
                    {
                        var valueIlLocal = _ctx.IL.DeclareLocal(elemClrType);
                        _ctx.Locals[sc.ValueLocal] = valueIlLocal;
                        _ctx.IL.Emit(OpCodes.Ldloca, tripleLocal);
                        _ctx.IL.Emit(OpCodes.Ldfld, tripleType.GetField("Item1")!);
                        _ctx.IL.Emit(OpCodes.Stloc, valueIlLocal);
                    }

                    if (sc.OkLocal != null)
                    {
                        var okIlLocal = _ctx.IL.DeclareLocal(typeof(bool));
                        _ctx.Locals[sc.OkLocal] = okIlLocal;
                        _ctx.IL.Emit(OpCodes.Ldloca, tripleLocal);
                        _ctx.IL.Emit(OpCodes.Ldfld, tripleType.GetField("Item2")!);
                        _ctx.IL.Emit(OpCodes.Stloc, okIlLocal);
                    }

                    _ctx.IL.Emit(OpCodes.Br, bodyLabels[i]);
                    _ctx.IL.MarkLabel(notCompletedLabel);
                }
            }

            // If there's a default case, jump to it
            if (defaultIndex >= 0)
            {
                _ctx.IL.Emit(OpCodes.Br, bodyLabels[defaultIndex]);
            }
            else
            {
                // No default — yield and retry
                _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                _ctx.IL.Emit(OpCodes.Call, typeof(System.Threading.Thread).GetMethod("Sleep", new[] { typeof(int) })!);
                _ctx.IL.Emit(OpCodes.Br, loopLabel);
            }

            // Phase 3: Case bodies
            for (int i = 0; i < caseCount; i++)
            {
                _ctx.IL.MarkLabel(bodyLabels[i]);

                foreach (var stmt in select.Cases[i].Body)
                {
                    _body.EmitStatement(stmt);
                }

                _ctx.IL.Emit(OpCodes.Br, endLabel);
            }

            _ctx.IL.MarkLabel(endLabel);
        }

        public static bool ContainsDefer(BlockStatement block)
        {
            foreach (var stmt in block.Statements)
            {
                if (stmt.NodeType == NodeType.DeferStatement)
                    return true;
                if (stmt is BlockStatement inner && ContainsDefer(inner))
                    return true;
                if (stmt is IfStatement ifStmt)
                {
                    if (ContainsDefer(ifStmt.Body))
                        return true;
                    if (ifStmt.ElseBody is BlockStatement elseBlock && ContainsDefer(elseBlock))
                        return true;
                }
                if (stmt is ForStatement forStmt && ContainsDefer(forStmt.Body))
                    return true;
            }
            return false;
        }

        private void EmitDeferOrGoCall(Expression callExpr, bool isDeferPush)
        {
            // Generate a lambda for the call, capturing evaluated arguments
            var lambdaName = isDeferPush ? $"__defer_{_body.LambdaCounter++}" : $"__go_{_body.LambdaCounter++}";

            // Determine call info and evaluate args eagerly
            var argLocals = new List<(LocalBuilder local, Type type)>();

            if (callExpr is CallExpression call)
            {
                // Handle go/defer func(...) { ... }(...) — function literal call
                if (call.CallTarget is FunctionLiteralExpression funcLit)
                {
                    if (funcLit.Parameters.Count == 0 && call.Arguments.Count == 0)
                    {
                        // No args — emit literal directly, produces Action/Func delegate
                        _body.Closures.EmitFunctionLiteral(funcLit);

                        // For go/defer we need Action. If it has a return type, wrap it.
                        if (funcLit.ReturnTypes.Count > 0)
                        {
                            var funcType = _ctx.Mapper.Map(funcLit.FunctionType);
                            var funcLocal = _ctx.IL.DeclareLocal(funcType);
                            _ctx.IL.Emit(OpCodes.Stloc, funcLocal);

                            var wrapperName = $"__wrap_go_{_body.LambdaCounter++}";
                            var wrapperMethod = _ctx.PackageType.DefineMethod(
                                wrapperName,
                                MethodAttributes.Private | MethodAttributes.Static,
                                typeof(void),
                                new[] { funcType });
                            var wIL = wrapperMethod.GetILGenerator();
                            wIL.Emit(OpCodes.Ldarg_0);
                            wIL.Emit(OpCodes.Callvirt, funcType.GetMethod("Invoke")!);
                            wIL.Emit(OpCodes.Pop);
                            wIL.Emit(OpCodes.Ret);

                            _ctx.IL.Emit(OpCodes.Ldloc, funcLocal);
                            _ctx.IL.Emit(OpCodes.Ldftn, wrapperMethod);
                            var actionCtor = typeof(Action).GetConstructor(new[] { typeof(object), typeof(IntPtr) })!;
                            _ctx.IL.Emit(OpCodes.Newobj, actionCtor);
                        }
                    }
                    else
                    {
                        // With args — eagerly evaluate, then wrap delegate + args
                        EmitGoFuncLiteralWithArgs(funcLit, call);
                    }

                    goto emitPushOrGo;
                }

                // Evaluate arguments eagerly into locals
                foreach (var arg in call.Arguments)
                {
                    _body.EmitExpression(arg);
                    var argType = _ctx.Mapper.Map(arg.Type);
                    var argLocal = _ctx.IL.DeclareLocal(argType);
                    _ctx.IL.Emit(OpCodes.Stloc, argLocal);
                    argLocals.Add((argLocal, argType));
                }

                // Build the lambda method that calls f with the captured args
                var paramTypes = new Type[argLocals.Count];
                for (int i = 0; i < argLocals.Count; i++)
                    paramTypes[i] = argLocals[i].type;
                var lambdaMethod = _ctx.PackageType.DefineMethod(
                    lambdaName,
                    MethodAttributes.Private | MethodAttributes.Static,
                    typeof(void),
                    paramTypes);

                // Emit the lambda body
                var lambdaIL = lambdaMethod.GetILGenerator();

                if (_ctx.Methods.TryGetValue(call.Function, out var targetMethod))
                {
                    // User function: load args then call
                    for (int i = 0; i < paramTypes.Length; i++)
                        lambdaIL.Emit(OpCodes.Ldarg, i);
                    lambdaIL.Emit(OpCodes.Call, targetMethod);
                    if (call.Function.ReturnType != BuiltinTypes.Void)
                        lambdaIL.Emit(OpCodes.Pop);
                }
                else
                {
                    // Builtin: handler loads args itself
                    EmitBuiltinInLambda(lambdaIL, call, paramTypes);
                }

                lambdaIL.Emit(OpCodes.Ret);

                // Create delegate: if no args, use simple Action; otherwise use closure pattern
                if (argLocals.Count == 0)
                {
                    // Simple: new Action(null, ldftn lambda)
                    _ctx.IL.Emit(OpCodes.Ldnull);
                    _ctx.IL.Emit(OpCodes.Ldftn, lambdaMethod);
                    var actionCtor = typeof(Action).GetConstructor(new[] { typeof(object), typeof(IntPtr) })!;
                    _ctx.IL.Emit(OpCodes.Newobj, actionCtor);
                }
                else
                {
                    // With captured args: create a closure object (Tuple) holding args,
                    // but for simplicity, we emit a static method and use a local lambda.
                    // Use a simpler approach: emit inline code that creates an Action.
                    EmitClosureAction(lambdaMethod, argLocals);
                }
            }
            else if (callExpr is MethodCallExpression methodCall)
            {
                // Evaluate receiver + arguments eagerly
                _body.EmitExpression(methodCall.Receiver);
                var recvType = _ctx.Mapper.Map(methodCall.Receiver.Type);
                var recvLocal = _ctx.IL.DeclareLocal(recvType);
                _ctx.IL.Emit(OpCodes.Stloc, recvLocal);
                argLocals.Add((recvLocal, recvType));

                foreach (var arg in methodCall.Arguments)
                {
                    _body.EmitExpression(arg);
                    var argType = _ctx.Mapper.Map(arg.Type);
                    var argLocal = _ctx.IL.DeclareLocal(argType);
                    _ctx.IL.Emit(OpCodes.Stloc, argLocal);
                    argLocals.Add((argLocal, argType));
                }

                var paramTypes = new Type[argLocals.Count];
                for (int i = 0; i < argLocals.Count; i++)
                    paramTypes[i] = argLocals[i].type;
                var lambdaMethod = _ctx.PackageType.DefineMethod(
                    lambdaName,
                    MethodAttributes.Public | MethodAttributes.Static,
                    typeof(void),
                    paramTypes);

                var lambdaIL = lambdaMethod.GetILGenerator();
                for (int i = 0; i < paramTypes.Length; i++)
                {
                    lambdaIL.Emit(OpCodes.Ldarg, i);
                }

                if (_ctx.Methods.TryGetValue(methodCall.Method, out var targetMethod))
                {
                    lambdaIL.Emit(OpCodes.Call, targetMethod);
                }
                else if (!recvType.IsValueType)
                {
                    // Runtime type method call (e.g. sync.WaitGroup.Done)
                    var methodArgTypes = new Type[methodCall.Method.Parameters.Count];
                    for (int i = 0; i < methodArgTypes.Length; i++)
                        methodArgTypes[i] = _ctx.Mapper.Map(methodCall.Method.Parameters[i].Type);
                    var clrMethod = recvType.GetMethod(methodCall.Method.Name, methodArgTypes);
                    if (clrMethod != null)
                    {
                        lambdaIL.Emit(OpCodes.Callvirt, clrMethod);
                    }
                }

                if (methodCall.Method.ReturnType != BuiltinTypes.Void)
                    lambdaIL.Emit(OpCodes.Pop);
                lambdaIL.Emit(OpCodes.Ret);

                EmitClosureAction(lambdaMethod, argLocals);
            }
            else
            {
                throw new NotSupportedException($"Cannot defer/go expression of type {callExpr.NodeType}");
            }

            emitPushOrGo:
            // Push or Go
            if (isDeferPush)
            {
                // Action is on the stack; push it onto the defer stack
                var actionLocal = _ctx.IL.DeclareLocal(typeof(Action));
                _ctx.IL.Emit(OpCodes.Stloc, actionLocal);
                _ctx.IL.Emit(OpCodes.Ldloc, _ctx.DeferStack!);
                _ctx.IL.Emit(OpCodes.Ldloc, actionLocal);
                _ctx.IL.Emit(OpCodes.Callvirt, typeof(DeferStack).GetMethod("Push")!);
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Call, typeof(Goroutine).GetMethod("Go", new[] { typeof(Action) })!);
            }
        }

        private void EmitClosureAction(MethodBuilder lambdaMethod, List<(LocalBuilder local, Type type)> argLocals)
        {
            if (argLocals.Count == 1)
            {
                // Fall through to universal approach below
            }

            var fieldNames = new FieldBuilder[argLocals.Count];
            for (int i = 0; i < argLocals.Count; i++)
            {
                var captureFieldName = $"__capture_{_body.LambdaCounter}_{i}";
                fieldNames[i] = _ctx.PackageType.DefineField(
                    captureFieldName,
                    argLocals[i].type,
                    FieldAttributes.Public | FieldAttributes.Static);
            }

            // Store captured values into static fields
            for (int i = 0; i < argLocals.Count; i++)
            {
                _ctx.IL.Emit(OpCodes.Ldloc, argLocals[i].local);
                _ctx.IL.Emit(OpCodes.Stsfld, fieldNames[i]);
            }

            // Create a no-arg wrapper that loads from the static fields and calls the lambda
            var wrapperName = $"__wrap_{_body.LambdaCounter++}";
            var wrapperMethod = _ctx.PackageType.DefineMethod(
                wrapperName,
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(void),
                Type.EmptyTypes);

            var wrapperIL = wrapperMethod.GetILGenerator();
            for (int i = 0; i < argLocals.Count; i++)
            {
                wrapperIL.Emit(OpCodes.Ldsfld, fieldNames[i]);
            }
            wrapperIL.Emit(OpCodes.Call, lambdaMethod);
            wrapperIL.Emit(OpCodes.Ret);

            // Create Action from the wrapper
            _ctx.IL.Emit(OpCodes.Ldnull);
            _ctx.IL.Emit(OpCodes.Ldftn, wrapperMethod);
            var actionCtor = typeof(Action).GetConstructor(new[] { typeof(object), typeof(IntPtr) })!;
            _ctx.IL.Emit(OpCodes.Newobj, actionCtor);
        }

        private void EmitGoFuncLiteralWithArgs(
            FunctionLiteralExpression funcLit,
            CallExpression call)
        {
            // Eagerly evaluate call arguments into locals
            var eagerLocals = new List<(LocalBuilder local, Type type)>();
            foreach (var arg in call.Arguments)
            {
                _body.EmitExpression(arg);
                var argType = _ctx.Mapper.Map(arg.Type);
                var argLocal = _ctx.IL.DeclareLocal(argType);
                _ctx.IL.Emit(OpCodes.Stloc, argLocal);
                eagerLocals.Add((argLocal, argType));
            }

            // Emit the function literal → produces a delegate (handles captures too)
            _body.Closures.EmitFunctionLiteral(funcLit);
            var delegateType = _ctx.Mapper.Map(funcLit.FunctionType);
            var delegateLocal = _ctx.IL.DeclareLocal(delegateType);
            _ctx.IL.Emit(OpCodes.Stloc, delegateLocal);

            // Build a wrapper closure class holding the delegate + eager arg values
            var wrapperName = $"__go_wrap_{_body.LambdaCounter++}";
            var wrapperBuilder = _ctx.Module.DefineType(
                wrapperName,
                TypeAttributes.Public | TypeAttributes.Sealed);

            var fnField = wrapperBuilder.DefineField("_fn", delegateType, FieldAttributes.Public);
            var argFields = new List<FieldBuilder>();
            for (int i = 0; i < eagerLocals.Count; i++)
            {
                argFields.Add(wrapperBuilder.DefineField(
                    $"_a{i}", eagerLocals[i].type, FieldAttributes.Public));
            }

            // Invoke(): calls _fn.Invoke(_a0, _a1, ...)
            var invokeMethod = wrapperBuilder.DefineMethod(
                "Invoke",
                MethodAttributes.Public,
                typeof(void),
                Type.EmptyTypes);
            var wIL = invokeMethod.GetILGenerator();
            wIL.Emit(OpCodes.Ldarg_0);
            wIL.Emit(OpCodes.Ldfld, fnField);
            for (int i = 0; i < argFields.Count; i++)
            {
                wIL.Emit(OpCodes.Ldarg_0);
                wIL.Emit(OpCodes.Ldfld, argFields[i]);
            }
            wIL.Emit(OpCodes.Callvirt, delegateType.GetMethod("Invoke")!);
            if (funcLit.ReturnTypes.Count > 0)
            {
                wIL.Emit(OpCodes.Pop);
            }
            wIL.Emit(OpCodes.Ret);

            var wrapperType = wrapperBuilder.CreateType()!;

            // Create and populate wrapper instance
            var wrapperLocal = _ctx.IL.DeclareLocal(wrapperType);
            _ctx.IL.Emit(OpCodes.Newobj, wrapperType.GetConstructor(Type.EmptyTypes)!);
            _ctx.IL.Emit(OpCodes.Stloc, wrapperLocal);

            _ctx.IL.Emit(OpCodes.Ldloc, wrapperLocal);
            _ctx.IL.Emit(OpCodes.Ldloc, delegateLocal);
            _ctx.IL.Emit(OpCodes.Stfld, wrapperType.GetField("_fn")!);

            for (int i = 0; i < eagerLocals.Count; i++)
            {
                _ctx.IL.Emit(OpCodes.Ldloc, wrapperLocal);
                _ctx.IL.Emit(OpCodes.Ldloc, eagerLocals[i].local);
                _ctx.IL.Emit(OpCodes.Stfld, wrapperType.GetField($"_a{i}")!);
            }

            // Create Action from wrapper.Invoke
            var runtimeInvoke = wrapperType.GetMethod("Invoke")!;
            _ctx.IL.Emit(OpCodes.Ldloc, wrapperLocal);
            _ctx.IL.Emit(OpCodes.Ldftn, runtimeInvoke);
            var actionCtor = typeof(Action).GetConstructor(new[] { typeof(object), typeof(IntPtr) })!;
            _ctx.IL.Emit(OpCodes.Newobj, actionCtor);
        }

        private void EmitBuiltinInLambda(ILGenerator il, CallExpression call, Type[] paramTypes)
        {
            var name = call.Function.Name;
            var pkg = call.Function.PackageName;

            // Pattern 1: Print-family — pack all args into object[], call static method
            if (TryEmitLambdaPrintFamily(il, name, pkg, paramTypes))
                return;

            // Pattern 2: Format-family — first arg is format string, rest packed into object[]
            if (TryEmitLambdaFormatFamily(il, name, pkg, paramTypes))
                return;

            // Pattern 3: Simple builtins
            if (name == "close" && paramTypes.Length == 1)
            {
                il.Emit(OpCodes.Ldarg_0);
                var closeMethod = paramTypes[0].GetMethod("Close");
                if (closeMethod != null)
                    il.Emit(OpCodes.Call, closeMethod);
                return;
            }

            if (name == "panic" && paramTypes.Length == 1)
            {
                il.Emit(OpCodes.Ldarg_0);
                if (paramTypes[0].IsValueType)
                    il.Emit(OpCodes.Box, paramTypes[0]);
                il.Emit(OpCodes.Call, typeof(BuiltIn).GetMethod("Panic")!);
                return;
            }

            // Pattern 4: Generic static call fallback — resolve runtime type from package name
            var targetType = MapPackageToRuntimeType(pkg);
            if (targetType != null)
            {
                EmitLambdaStaticCall(il, targetType, name, paramTypes);
                return;
            }

            throw new NotSupportedException($"Builtin '{name}' not supported in defer/go context");
        }

        private bool TryEmitLambdaPrintFamily(ILGenerator il, string name, string? pkg, Type[] paramTypes)
        {
            Type? targetType = null;

            if (name == "println" && pkg == null)
                targetType = typeof(BuiltIn);
            else if (name == "print" && pkg == null)
                targetType = typeof(BuiltIn);
            else if (pkg == "fmt" || (pkg == null && (name == "Println" || name == "Print" || name == "Sprint" || name == "Sprintln")))
            {
                if (name == "Println" || name == "Print" || name == "Sprint" || name == "Sprintln")
                    targetType = typeof(Fmt);
            }
            else if (pkg == "log" && (name == "Println" || name == "Print" || name == "Fatal"))
                targetType = typeof(GoLog);

            if (targetType == null) return false;

            // Map "println"/"print" to runtime method names "Println"/"Print"
            var methodName = name;
            if (name == "println") methodName = "Println";
            if (name == "print") methodName = "Print";

            EmitLambdaPrintArgs(il, paramTypes);
            il.Emit(OpCodes.Call, targetType.GetMethod(methodName, new[] { typeof(object[]) })!);
            return true;
        }

        private bool TryEmitLambdaFormatFamily(ILGenerator il, string name, string? pkg, Type[] paramTypes)
        {
            Type? targetType = null;

            if (pkg == "fmt" || pkg == null)
            {
                if (name == "Printf" || name == "Sprintf" || name == "Errorf")
                    targetType = typeof(Fmt);
            }

            if (pkg == "log")
            {
                if (name == "Printf" || name == "Fatalf")
                    targetType = typeof(GoLog);
            }

            if (targetType == null || paramTypes.Length < 1) return false;

            EmitLambdaFormatArgs(il, paramTypes);
            il.Emit(OpCodes.Call,
                targetType.GetMethod(name, new[] { typeof(string), typeof(object[]) })!);
            return true;
        }

        private static void EmitLambdaPrintArgs(ILGenerator il, Type[] paramTypes)
        {
            il.Emit(OpCodes.Ldc_I4, paramTypes.Length);
            il.Emit(OpCodes.Newarr, typeof(object));
            for (int i = 0; i < paramTypes.Length; i++)
            {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i);
                if (paramTypes[i].IsValueType)
                    il.Emit(OpCodes.Box, paramTypes[i]);
                il.Emit(OpCodes.Stelem_Ref);
            }
        }

        private static void EmitLambdaFormatArgs(ILGenerator il, Type[] paramTypes)
        {
            // Arg 0 is the format string
            il.Emit(OpCodes.Ldarg_0);

            // Pack remaining args into object[]
            int restCount = paramTypes.Length - 1;
            il.Emit(OpCodes.Ldc_I4, restCount);
            il.Emit(OpCodes.Newarr, typeof(object));
            for (int i = 0; i < restCount; i++)
            {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);
                if (paramTypes[i + 1].IsValueType)
                    il.Emit(OpCodes.Box, paramTypes[i + 1]);
                il.Emit(OpCodes.Stelem_Ref);
            }
        }

        private static void EmitLambdaStaticCall(ILGenerator il, Type targetType, string methodName, Type[] paramTypes)
        {
            // Find method by name + param count
            MethodInfo? method = targetType.GetMethod(methodName, paramTypes);
            if (method == null)
            {
                var candidates = targetType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var c in candidates)
                {
                    if (c.Name == methodName && c.GetParameters().Length == paramTypes.Length)
                    {
                        method = c;
                        break;
                    }
                }
            }

            if (method == null)
                throw new NotSupportedException(
                    $"Method {targetType.Name}.{methodName} with {paramTypes.Length} params not found");

            var methodParams = method.GetParameters();
            for (int i = 0; i < paramTypes.Length; i++)
            {
                il.Emit(OpCodes.Ldarg, i);

                // Implicit conversions
                var from = paramTypes[i];
                var to = methodParams[i].ParameterType;
                if (from != to)
                {
                    if (from == typeof(int) && to == typeof(long))
                        il.Emit(OpCodes.Conv_I8);
                    else if (from == typeof(int) && to == typeof(double))
                        il.Emit(OpCodes.Conv_R8);
                    else if (from == typeof(long) && to == typeof(double))
                        il.Emit(OpCodes.Conv_R8);
                    else if (from == typeof(float) && to == typeof(double))
                        il.Emit(OpCodes.Conv_R8);
                    else if (from.IsValueType && to == typeof(object))
                        il.Emit(OpCodes.Box, from);
                }
            }

            il.Emit(OpCodes.Call, method);
        }

        private static Type? MapPackageToRuntimeType(string? pkg)
        {
            return pkg switch
            {
                "os" => typeof(GoOs),
                "time" => typeof(GoTime),
                "sort" => typeof(GoSort),
                "rand" => typeof(GoRand),
                "strconv" => typeof(GoStrconv),
                "strings" => typeof(GoStrings),
                "errors" => typeof(GoErrors),
                "math" => typeof(GoMath),
                "regexp" => typeof(GoRegexp),
                "unicode" => typeof(GoUnicode),
                "utf8" => typeof(GoUtf8),
                "bytes" => typeof(GoBytes),
                "path" => typeof(GoPath),
                "filepath" => typeof(GoFilepath),
                "io" => typeof(GoIo),
                "log" => typeof(GoLog),
                "fmt" => typeof(Fmt),
                _ => null,
            };
        }
    }
}

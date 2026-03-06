// -----------------------------------------------------------------------
// <copyright file="ForRangeEmitter.cs" company="Ziad">
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
using System.Reflection.Emit;
using Ngo.Compiler.Ast;
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;
using Ngo.Runtime;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Emits IL for for-range statements over slices, arrays, strings, maps, and channels.
    /// </summary>
    internal sealed class ForRangeEmitter
    {
        private readonly EmitContext _ctx;
        private readonly MethodBodyEmitter _body;

        public ForRangeEmitter(EmitContext ctx, MethodBodyEmitter body)
        {
            _ctx = ctx;
            _body = body;
        }

        public void EmitForRange(ForRangeStatement forRange)
        {
            var iterableType = forRange.Iterable.Type;

            if (iterableType is SliceTypeSymbol || iterableType is ArrayTypeSymbol)
            {
                EmitForRangeSlice(forRange);
            }
            else if (iterableType.TypeKind == TypeKind.String || iterableType.TypeKind == TypeKind.UntypedString)
            {
                EmitForRangeString(forRange);
            }
            else if (iterableType is MapTypeSymbol)
            {
                EmitForRangeMap(forRange);
            }
            else if (iterableType is ChannelTypeSymbol)
            {
                EmitForRangeChannel(forRange);
            }
            else if (TypeChecker.IsInteger(iterableType))
            {
                EmitForRangeInt(forRange);
            }
            else
            {
                throw new NotSupportedException($"for-range over {iterableType.TypeKind} not supported");
            }
        }

        private void EmitForRangeSlice(ForRangeStatement forRange)
        {
            // Lower: for i := 0; i < len(slice); i++ { body using slice[i] }
            var condLabel = _ctx.IL.DefineLabel();
            var endLabel = _ctx.IL.DefineLabel();
            var continueLabel = _ctx.IL.DefineLabel();

            bool isArray = forRange.Iterable.Type is ArrayTypeSymbol;

            // Emit iterable into a local
            var sliceClrType = _ctx.Mapper.Map(forRange.Iterable.Type);
            var sliceLocal = _ctx.IL.DeclareLocal(sliceClrType);
            _body.EmitExpression(forRange.Iterable);
            _ctx.IL.Emit(OpCodes.Stloc, sliceLocal);

            // Index local
            var indexLocal = _ctx.IL.DeclareLocal(typeof(int));
            _ctx.IL.Emit(OpCodes.Ldc_I4_0);
            _ctx.IL.Emit(OpCodes.Stloc, indexLocal);

            if (forRange.Key != null && !_ctx.CapturedSymbols.Contains(forRange.Key))
                _ctx.Locals[forRange.Key] = indexLocal;

            // Condition: i < len
            _ctx.IL.MarkLabel(condLabel);
            _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
            if (isArray)
            {
                _ctx.IL.Emit(OpCodes.Ldloc, sliceLocal);
                _ctx.IL.Emit(OpCodes.Ldlen);
                _ctx.IL.Emit(OpCodes.Conv_I4);
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Ldloca, sliceLocal);
                var lenProp = sliceClrType.GetProperty("Len")!;
                _ctx.IL.Emit(OpCodes.Call, lenProp.GetGetMethod()!);
            }
            _ctx.IL.Emit(OpCodes.Clt);
            _ctx.IL.Emit(OpCodes.Brfalse, endLabel);

            // Value local: slice[i]
            if (forRange.Value != null)
            {
                TypeSymbol elemTypeSymbol;
                if (forRange.Iterable.Type is SliceTypeSymbol sts)
                    elemTypeSymbol = sts.ElementType;
                else
                    elemTypeSymbol = ((ArrayTypeSymbol)forRange.Iterable.Type).ElementType;

                var elemClrType = _ctx.Mapper.Map(elemTypeSymbol);

                if (isArray)
                {
                    _ctx.IL.Emit(OpCodes.Ldloc, sliceLocal);
                    _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
                    if (elemClrType.IsValueType)
                        _ctx.IL.Emit(OpCodes.Ldelem, elemClrType);
                    else
                        _ctx.IL.Emit(OpCodes.Ldelem_Ref);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldloca, sliceLocal);
                    _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
                    var indexer = sliceClrType.GetProperty("Item")!;
                    _ctx.IL.Emit(OpCodes.Call, indexer.GetGetMethod()!);
                    if (indexer.PropertyType.IsByRef)
                    {
                        if (elemClrType.IsValueType)
                            _ctx.IL.Emit(OpCodes.Ldobj, elemClrType);
                        else
                            _ctx.IL.Emit(OpCodes.Ldind_Ref);
                    }
                }

                // Go 1.22: if captured, wrap in new Box per iteration
                EmitStoreRangeVar(forRange.Value, elemClrType);
            }

            // Go 1.22: if key is captured, wrap in new Box per iteration
            if (forRange.Key != null && _ctx.CapturedSymbols.Contains(forRange.Key))
            {
                // indexLocal has the raw int; wrap in Box<long> for the captured local
                _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                EmitStoreRangeVar(forRange.Key, typeof(long));
            }

            // Body
            _body.PushLoopLabels(endLabel, continueLabel);
            _body.EmitBlock(forRange.Body);
            _ctx.LoopLabels.Pop();

            // Post: i++
            _ctx.IL.MarkLabel(continueLabel);
            _ctx.IL.Emit(OpCodes.Ldloc, indexLocal);
            _ctx.IL.Emit(OpCodes.Ldc_I4_1);
            _ctx.IL.Emit(OpCodes.Add);
            _ctx.IL.Emit(OpCodes.Stloc, indexLocal);
            _ctx.IL.Emit(OpCodes.Br, condLabel);

            _ctx.IL.MarkLabel(endLabel);
        }

        private void EmitForRangeString(ForRangeStatement forRange)
        {
            // Use GoString.RangeRunes(s) which yields (byteIndex, rune) pairs
            var condLabel = _ctx.IL.DefineLabel();
            var endLabel = _ctx.IL.DefineLabel();
            var continueLabel = _ctx.IL.DefineLabel();

            _body.EmitExpression(forRange.Iterable);
            var rangeRunes = typeof(GoString).GetMethod("RangeRunes")!;
            _ctx.IL.Emit(OpCodes.Call, rangeRunes);

            var enumerableType = typeof(System.Collections.Generic.IEnumerable<>)
                .MakeGenericType(typeof(ValueTuple<int, int>));
            var getEnumerator = enumerableType.GetMethod("GetEnumerator")!;
            _ctx.IL.Emit(OpCodes.Callvirt, getEnumerator);

            var enumeratorType = typeof(System.Collections.Generic.IEnumerator<>)
                .MakeGenericType(typeof(ValueTuple<int, int>));
            var enumLocal = _ctx.IL.DeclareLocal(enumeratorType);
            _ctx.IL.Emit(OpCodes.Stloc, enumLocal);

            _ctx.IL.MarkLabel(condLabel);
            _ctx.IL.Emit(OpCodes.Ldloc, enumLocal);
            var moveNext = typeof(System.Collections.IEnumerator).GetMethod("MoveNext")!;
            _ctx.IL.Emit(OpCodes.Callvirt, moveNext);
            _ctx.IL.Emit(OpCodes.Brfalse, endLabel);

            // Get current tuple
            _ctx.IL.Emit(OpCodes.Ldloc, enumLocal);
            var getCurrent = enumeratorType.GetProperty("Current")!.GetGetMethod()!;
            _ctx.IL.Emit(OpCodes.Callvirt, getCurrent);
            var tupleLocal = _ctx.IL.DeclareLocal(typeof(ValueTuple<int, int>));
            _ctx.IL.Emit(OpCodes.Stloc, tupleLocal);

            if (forRange.Key != null)
            {
                var keyLocal = _ctx.IL.DeclareLocal(typeof(long));
                _ctx.Locals[forRange.Key] = keyLocal;
                _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, typeof(ValueTuple<int, int>).GetField("Item1")!);
                _ctx.IL.Emit(OpCodes.Conv_I8);
                _ctx.IL.Emit(OpCodes.Stloc, keyLocal);
            }

            if (forRange.Value != null)
            {
                var valueLocal = _ctx.IL.DeclareLocal(typeof(int));
                _ctx.Locals[forRange.Value] = valueLocal;
                _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, typeof(ValueTuple<int, int>).GetField("Item2")!);
                _ctx.IL.Emit(OpCodes.Stloc, valueLocal);
            }

            _body.PushLoopLabels(endLabel, continueLabel);
            _body.EmitBlock(forRange.Body);
            _ctx.LoopLabels.Pop();

            _ctx.IL.MarkLabel(continueLabel);
            _ctx.IL.Emit(OpCodes.Br, condLabel);
            _ctx.IL.MarkLabel(endLabel);
        }

        private void EmitForRangeMap(ForRangeStatement forRange)
        {
            var mapType = (MapTypeSymbol)forRange.Iterable.Type;
            var keyClrType = _ctx.Mapper.Map(mapType.KeyType);
            var valClrType = _ctx.Mapper.Map(mapType.ValueType);
            var tupleType = typeof(ValueTuple<,>).MakeGenericType(keyClrType, valClrType);

            var condLabel = _ctx.IL.DefineLabel();
            var endLabel = _ctx.IL.DefineLabel();
            var continueLabel = _ctx.IL.DefineLabel();

            _body.EmitExpression(forRange.Iterable);
            var mapClrType = _ctx.Mapper.Map(forRange.Iterable.Type);
            var rangeMethod = mapClrType.GetMethod("Range")!;
            _ctx.IL.Emit(OpCodes.Call, rangeMethod);

            var enumerableType = typeof(System.Collections.Generic.IEnumerable<>).MakeGenericType(tupleType);
            var getEnumerator = enumerableType.GetMethod("GetEnumerator")!;
            _ctx.IL.Emit(OpCodes.Callvirt, getEnumerator);

            var enumeratorType = typeof(System.Collections.Generic.IEnumerator<>).MakeGenericType(tupleType);
            var enumLocal = _ctx.IL.DeclareLocal(enumeratorType);
            _ctx.IL.Emit(OpCodes.Stloc, enumLocal);

            _ctx.IL.MarkLabel(condLabel);
            _ctx.IL.Emit(OpCodes.Ldloc, enumLocal);
            var moveNext = typeof(System.Collections.IEnumerator).GetMethod("MoveNext")!;
            _ctx.IL.Emit(OpCodes.Callvirt, moveNext);
            _ctx.IL.Emit(OpCodes.Brfalse, endLabel);

            _ctx.IL.Emit(OpCodes.Ldloc, enumLocal);
            var getCurrent = enumeratorType.GetProperty("Current")!.GetGetMethod()!;
            _ctx.IL.Emit(OpCodes.Callvirt, getCurrent);
            var tupleLocal = _ctx.IL.DeclareLocal(tupleType);
            _ctx.IL.Emit(OpCodes.Stloc, tupleLocal);

            if (forRange.Key != null)
            {
                var keyLocal = _ctx.IL.DeclareLocal(keyClrType);
                _ctx.Locals[forRange.Key] = keyLocal;
                _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, tupleType.GetField("Item1")!);
                _ctx.IL.Emit(OpCodes.Stloc, keyLocal);
            }

            if (forRange.Value != null)
            {
                var valueLocal = _ctx.IL.DeclareLocal(valClrType);
                _ctx.Locals[forRange.Value] = valueLocal;
                _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, tupleType.GetField("Item2")!);
                _ctx.IL.Emit(OpCodes.Stloc, valueLocal);
            }

            _body.PushLoopLabels(endLabel, continueLabel);
            _body.EmitBlock(forRange.Body);
            _ctx.LoopLabels.Pop();

            _ctx.IL.MarkLabel(continueLabel);
            _ctx.IL.Emit(OpCodes.Br, condLabel);
            _ctx.IL.MarkLabel(endLabel);
        }

        private void EmitForRangeChannel(ForRangeStatement forRange)
        {
            // for v := range ch → receive until closed
            // Lower to:
            //   chanLocal = ch
            //   loop:
            //     (value, ok) = chanLocal.Receive()
            //     if !ok: goto end
            //     v = value
            //     body
            //     goto loop
            //   end:
            var chanType = (ChannelTypeSymbol)forRange.Iterable.Type;
            var elemClrType = _ctx.Mapper.Map(chanType.ElementType);
            var chanClrType = _ctx.Mapper.Map(chanType);

            var loopLabel = _ctx.IL.DefineLabel();
            var endLabel = _ctx.IL.DefineLabel();
            var continueLabel = _ctx.IL.DefineLabel();

            // Emit channel expression into local
            _body.EmitExpression(forRange.Iterable);
            var chanLocal = _ctx.IL.DeclareLocal(chanClrType);
            _ctx.IL.Emit(OpCodes.Stloc, chanLocal);

            // Receive returns ValueTuple<T, bool>
            var tupleType = typeof(ValueTuple<,>).MakeGenericType(elemClrType, typeof(bool));
            var tupleLocal = _ctx.IL.DeclareLocal(tupleType);
            var receiveMethod = chanClrType.GetMethod("Receive")!;

            _ctx.IL.MarkLabel(loopLabel);

            // (value, ok) = channel.Receive()
            _ctx.IL.Emit(OpCodes.Ldloc, chanLocal);
            _ctx.IL.Emit(OpCodes.Callvirt, receiveMethod);
            _ctx.IL.Emit(OpCodes.Stloc, tupleLocal);

            // if !ok → break
            _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
            _ctx.IL.Emit(OpCodes.Ldfld, tupleType.GetField("Item2")!);
            _ctx.IL.Emit(OpCodes.Brfalse, endLabel);

            // Store received value in the iteration variable (Key holds the value for channels)
            if (forRange.Key != null)
            {
                var valueLocal = _ctx.IL.DeclareLocal(elemClrType);
                _ctx.Locals[forRange.Key] = valueLocal;
                _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, tupleType.GetField("Item1")!);
                _ctx.IL.Emit(OpCodes.Stloc, valueLocal);
            }

            _body.PushLoopLabels(endLabel, continueLabel);
            _body.EmitBlock(forRange.Body);
            _ctx.LoopLabels.Pop();

            _ctx.IL.MarkLabel(continueLabel);
            _ctx.IL.Emit(OpCodes.Br, loopLabel);
            _ctx.IL.MarkLabel(endLabel);
        }
        private void EmitForRangeInt(ForRangeStatement forRange)
        {
            // for i := range N → for i := 0; i < N; i++
            var condLabel = _ctx.IL.DefineLabel();
            var bodyLabel = _ctx.IL.DefineLabel();
            var continueLabel = _ctx.IL.DefineLabel();
            var endLabel = _ctx.IL.DefineLabel();

            // Evaluate N and store as limit
            _body.EmitExpression(forRange.Iterable);
            var limitLocal = _ctx.IL.DeclareLocal(typeof(long));
            _ctx.IL.Emit(OpCodes.Stloc, limitLocal);

            // i := 0
            LocalBuilder? keyLocal = null;
            if (forRange.Key != null)
            {
                keyLocal = _ctx.IL.DeclareLocal(typeof(long));
                _ctx.Locals[forRange.Key] = keyLocal;
                _ctx.IL.Emit(OpCodes.Ldc_I8, 0L);
                _ctx.IL.Emit(OpCodes.Stloc, keyLocal);
            }
            else
            {
                // Even without a key variable, we need a counter
                keyLocal = _ctx.IL.DeclareLocal(typeof(long));
                _ctx.IL.Emit(OpCodes.Ldc_I8, 0L);
                _ctx.IL.Emit(OpCodes.Stloc, keyLocal);
            }

            _ctx.IL.Emit(OpCodes.Br, condLabel);

            // Body
            _ctx.IL.MarkLabel(bodyLabel);

            _body.PushLoopLabels(endLabel, continueLabel);
            _body.EmitBlock(forRange.Body);
            _ctx.LoopLabels.Pop();

            // i++
            _ctx.IL.MarkLabel(continueLabel);
            _ctx.IL.Emit(OpCodes.Ldloc, keyLocal);
            _ctx.IL.Emit(OpCodes.Ldc_I8, 1L);
            _ctx.IL.Emit(OpCodes.Add);
            _ctx.IL.Emit(OpCodes.Stloc, keyLocal);

            // i < N
            _ctx.IL.MarkLabel(condLabel);
            _ctx.IL.Emit(OpCodes.Ldloc, keyLocal);
            _ctx.IL.Emit(OpCodes.Ldloc, limitLocal);
            _ctx.IL.Emit(OpCodes.Clt);
            _ctx.IL.Emit(OpCodes.Brtrue, bodyLabel);

            _ctx.IL.MarkLabel(endLabel);
        }

        /// <summary>
        /// Go 1.22: Store a range variable value. If the variable is captured by a closure,
        /// creates a new Box per iteration so each closure gets its own copy.
        /// Expects the value to be on the evaluation stack.
        /// </summary>
        private void EmitStoreRangeVar(LocalSymbol sym, Type clrType)
        {
            if (_ctx.CapturedSymbols.Contains(sym))
            {
                // Wrap in a new Box<T> each iteration
                var boxType = typeof(Box<>).MakeGenericType(clrType);
                var boxCtor = boxType.GetConstructor(new[] { clrType })!;
                _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                var boxLocal = _ctx.IL.DeclareLocal(boxType);
                _ctx.IL.Emit(OpCodes.Stloc, boxLocal);
                _ctx.Locals[sym] = boxLocal;
            }
            else
            {
                if (!_ctx.Locals.TryGetValue(sym, out var local))
                {
                    local = _ctx.IL.DeclareLocal(clrType);
                    _ctx.Locals[sym] = local;
                }
                _ctx.IL.Emit(OpCodes.Stloc, local);
            }
        }
    }
}

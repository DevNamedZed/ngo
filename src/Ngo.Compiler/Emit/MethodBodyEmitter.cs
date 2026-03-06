// -----------------------------------------------------------------------
// <copyright file="MethodBodyEmitter.cs" company="Ziad">
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
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;
using Ngo.Runtime;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Emits IL for method bodies — expressions and statements.
    /// </summary>
    internal sealed class MethodBodyEmitter
    {
        private readonly EmitContext _ctx;
        private readonly BuiltinEmitter _builtins;
        private readonly Dictionary<CallExpression, LocalBuilder> _spreadLocals = new();
        internal readonly ClosureEmitter Closures;
        private readonly ForRangeEmitter _ranges;
        private readonly DeferGoEmitter _deferGo;
        private IReadOnlyList<LocalSymbol>? _namedReturns;
        private string? _pendingLabel;

        internal IReadOnlyList<TypeSymbol> CurrentReturnTypes
        {
            get => _currentReturnTypes;
            set => _currentReturnTypes = value;
        }

        private IReadOnlyList<TypeSymbol> _currentReturnTypes = Array.Empty<TypeSymbol>();

        internal int LambdaCounter;

        public MethodBodyEmitter(EmitContext ctx)
        {
            _ctx = ctx;
            _builtins = new BuiltinEmitter(ctx, this);
            Closures = new ClosureEmitter(ctx, this);
            _ranges = new ForRangeEmitter(ctx, this);
            _deferGo = new DeferGoEmitter(ctx, this);
        }

        public void EmitFunctionBody(FunctionDeclaration func)
        {
            var method = _ctx.Methods[func.Symbol];
            _ctx.IL = method.GetILGenerator();
            _ctx.ResetMethodState();
            _currentReturnTypes = func.Symbol.ReturnTypes;

            // Register parameters
            for (int i = 0; i < func.Symbol.Parameters.Count; i++)
            {
                _ctx.Parameters[func.Symbol.Parameters[i]] = i;
            }

            // Pre-scan for variables captured by closures
            var capturedSymbols = Closures.CollectAllCaptures(func.Body);
            foreach (var sym in capturedSymbols)
                _ctx.CapturedSymbols.Add(sym);

            // Box captured parameters
            BoxCapturedParameters(func.Symbol.Parameters);

            // Declare named return locals (initialized to zero by CLR)
            _namedReturns = func.NamedReturns;
            foreach (var nr in func.NamedReturns)
            {
                var clrType = _ctx.Mapper.Map(nr.Type);
                if (_ctx.CapturedSymbols.Contains(nr))
                {
                    // Captured by a closure — wrap in Box<T> for shared mutation
                    var boxType = typeof(Box<>).MakeGenericType(clrType);
                    var local = _ctx.IL.DeclareLocal(boxType);
                    var boxCtor = boxType.GetConstructor(Type.EmptyTypes)!;
                    _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                    _ctx.Locals[nr] = local;
                }
                else
                {
                    var local = _ctx.IL.DeclareLocal(clrType);
                    _ctx.Locals[nr] = local;
                }
            }

            var hasDefer = ContainsDefer(func.Body);
            if (hasDefer)
            {
                var funcIsVoid = func.Symbol.ReturnType == BuiltinTypes.Void;
                var funcRetType = funcIsVoid ? null : _ctx.Mapper.MapReturnType(func.Symbol.ReturnTypes);
                EmitDeferWrappedBody(func.Body, funcIsVoid, funcRetType, _namedReturns);
            }
            else
            {
                EmitBlock(func.Body);
                EmitTrailingReturn(func.Symbol.ReturnType, func.Symbol.ReturnTypes);
            }

            _namedReturns = null;
        }

        public void EmitMethodBody(MethodDeclaration decl)
        {
            var method = _ctx.Methods[decl.Symbol];
            _ctx.IL = method.GetILGenerator();
            _ctx.ResetMethodState();
            _currentReturnTypes = decl.Symbol.ReturnTypes;

            // Receiver is param 0
            _ctx.Parameters[decl.Receiver] = 0;

            // Regular parameters start at 1
            for (int i = 0; i < decl.Symbol.Parameters.Count; i++)
            {
                _ctx.Parameters[decl.Symbol.Parameters[i]] = i + 1;
            }

            // Pre-scan for variables captured by closures
            var capturedSymbols = Closures.CollectAllCaptures(decl.Body);
            foreach (var sym in capturedSymbols)
                _ctx.CapturedSymbols.Add(sym);

            // Box captured parameters (receiver + regular params)
            BoxCapturedParameters(decl.Symbol.Parameters, decl.Receiver);

            // Declare named return locals (initialized to zero by CLR)
            _namedReturns = decl.NamedReturns;
            foreach (var nr in decl.NamedReturns)
            {
                var clrType = _ctx.Mapper.Map(nr.Type);
                if (_ctx.CapturedSymbols.Contains(nr))
                {
                    var boxType = typeof(Box<>).MakeGenericType(clrType);
                    var local = _ctx.IL.DeclareLocal(boxType);
                    var boxCtor = boxType.GetConstructor(Type.EmptyTypes)!;
                    _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                    _ctx.Locals[nr] = local;
                }
                else
                {
                    var local = _ctx.IL.DeclareLocal(clrType);
                    _ctx.Locals[nr] = local;
                }
            }

            var hasDefer = ContainsDefer(decl.Body);
            if (hasDefer)
            {
                var methodIsVoid = decl.Symbol.ReturnType == BuiltinTypes.Void;
                var methodRetType = methodIsVoid ? null : _ctx.Mapper.MapReturnType(decl.Symbol.ReturnTypes);
                EmitDeferWrappedBody(decl.Body, methodIsVoid, methodRetType, _namedReturns);
            }
            else
            {
                EmitBlock(decl.Body);
                EmitTrailingReturn(decl.Symbol.ReturnType, decl.Symbol.ReturnTypes);
            }

            _namedReturns = null;
        }

        public void EmitPackageInit(IReadOnlyList<VarDeclaration> vars, List<FunctionDeclaration> initFuncs)
        {
            var cctor = _ctx.PackageType.DefineConstructor(
                MethodAttributes.Static | MethodAttributes.Private,
                System.Reflection.CallingConventions.Standard,
                Type.EmptyTypes);

            _ctx.IL = cctor.GetILGenerator();
            _ctx.ResetMethodState();

            // Initialize package-level variables
            foreach (var v in vars)
            {
                if (v.Initializer != null && _ctx.PackageFields.TryGetValue(v.Symbol, out var field))
                {
                    EmitExpression(v.Initializer);
                    _ctx.IL.Emit(OpCodes.Stsfld, field);
                }
            }

            // Call init() functions in declaration order
            foreach (var initFunc in initFuncs)
            {
                if (_ctx.Methods.TryGetValue(initFunc.Symbol, out var mb))
                {
                    _ctx.IL.Emit(OpCodes.Call, mb);
                }
            }

            _ctx.IL.Emit(OpCodes.Ret);
        }

        internal void BoxCapturedParameters(IReadOnlyList<ParameterSymbol> parameters,
            ParameterSymbol? receiver = null)
        {
            // Box the receiver if captured
            if (receiver != null && _ctx.CapturedSymbols.Contains(receiver))
                BoxOneParameter(receiver);

            // Box regular parameters if captured
            foreach (var param in parameters)
            {
                if (_ctx.CapturedSymbols.Contains(param))
                    BoxOneParameter(param);
            }
        }

        internal void BoxOneParameter(Symbol param)
        {
            if (!_ctx.Parameters.TryGetValue(param, out var paramIndex))
                return;

            TypeSymbol paramType;
            if (param is ParameterSymbol ps) paramType = ps.Type;
            else return;

            var innerType = _ctx.Mapper.Map(paramType);
            var boxType = typeof(Box<>).MakeGenericType(innerType);
            var boxCtor = boxType.GetConstructor(new[] { innerType })!;

            var boxLocal = _ctx.IL.DeclareLocal(boxType);
            _ctx.IL.Emit(OpCodes.Ldarg, paramIndex);
            _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
            _ctx.IL.Emit(OpCodes.Stloc, boxLocal);
            _ctx.Locals[param] = boxLocal;
        }

        // --- Statements ---

        public void EmitStatement(AstNode node)
        {
            switch (node.NodeType)
            {
                case NodeType.BlockStatement:
                    EmitBlock((BlockStatement)node);
                    break;
                case NodeType.ReturnStatement:
                    EmitReturn((ReturnStatement)node);
                    break;
                case NodeType.ExpressionStatement:
                    EmitExpressionStatement((ExpressionStatement)node);
                    break;
                case NodeType.VarDeclaration:
                    EmitVarDeclaration((VarDeclaration)node);
                    break;
                case NodeType.AssignmentStatement:
                    EmitAssignment((AssignmentStatement)node);
                    break;
                case NodeType.IncDecStatement:
                    EmitIncDec((IncDecStatement)node);
                    break;
                case NodeType.IfStatement:
                    EmitIf((IfStatement)node);
                    break;
                case NodeType.ForStatement:
                    EmitFor((ForStatement)node);
                    break;
                case NodeType.SwitchStatement:
                    EmitSwitch((SwitchStatement)node);
                    break;
                case NodeType.BranchStatement:
                    EmitBranch((BranchStatement)node);
                    break;
                case NodeType.ForRangeStatement:
                    _ranges.EmitForRange((ForRangeStatement)node);
                    break;
                case NodeType.MultiVarDeclaration:
                    EmitMultiVarDeclaration((MultiVarDeclaration)node);
                    break;
                case NodeType.MultiAssignmentStatement:
                    EmitMultiAssignment((MultiAssignmentStatement)node);
                    break;
                case NodeType.ParallelAssignmentStatement:
                    EmitParallelAssignment((ParallelAssignmentStatement)node);
                    break;
                case NodeType.DeferStatement:
                    _deferGo.EmitDefer((DeferStatement)node);
                    break;
                case NodeType.GoStatement:
                    _deferGo.EmitGo((GoStatement)node);
                    break;
                case NodeType.SendStatement:
                    _deferGo.EmitSend((SendStatement)node);
                    break;
                case NodeType.TypeSwitchStatement:
                    EmitTypeSwitch((TypeSwitchStatement)node);
                    break;
                case NodeType.SelectStatement:
                    _deferGo.EmitSelectStatement((SelectStatement)node);
                    break;
                case NodeType.ConstDeclaration:
                    // Constants are inlined at use sites, no IL needed
                    break;
                case NodeType.TypeDeclaration:
                    EmitLocalTypeDeclaration((TypeDeclaration)node);
                    break;
                case NodeType.LabeledStatement:
                    EmitLabeledStatement((LabeledStatement)node);
                    break;
                default:
                    throw new NotSupportedException($"Statement emission not supported for: {node.NodeType}");
            }
        }

        internal void EmitBlock(BlockStatement block)
        {
            foreach (var stmt in block.Statements)
            {
                EmitStatement(stmt);
            }
        }

        private void EmitReturn(ReturnStatement ret)
        {
            // Named returns + defer: store into named return locals so deferred
            // functions can see/modify the return value, then leave the try block.
            if (_namedReturns != null && _namedReturns.Count > 0 && _ctx.DeferReturnLocal != null)
            {
                // return <values>: store each value into the corresponding named return local
                for (int i = 0; i < ret.Values.Count; i++)
                {
                    EmitExpression(ret.Values[i]);

                    // Interface wrapping if needed
                    if (i < _currentReturnTypes.Count
                        && _currentReturnTypes[i] is InterfaceTypeSymbol
                        && ret.Values[i].Type.TypeKind != TypeKind.Interface
                        && ret.Values[i].Type.TypeKind != TypeKind.UntypedNil)
                    {
                        var clrType = _ctx.Mapper.Map(_currentReturnTypes[i]);
                        EmitInterfaceWrapIfNeeded(ret.Values[i].Type, _currentReturnTypes[i], clrType);
                    }

                    EmitStore(_namedReturns[i]);
                }

                // Bare return (ret.Values.Count == 0): named locals already hold the values
                _ctx.IL.Emit(OpCodes.Leave, _ctx.DeferExitLabel);
                return;
            }

            if (ret.Values.Count == 0)
            {
                // Bare return with named returns: load named return locals
                if (_namedReturns != null && _namedReturns.Count > 0)
                {
                    EmitNamedReturnValues();
                }

                EmitRetOrLeave();
                return;
            }

            if (ret.Values.Count == 1)
            {
                EmitExpression(ret.Values[0]);

                // If return type is an interface but value is a concrete type, wrap
                if (_currentReturnTypes.Count == 1
                    && _currentReturnTypes[0] is InterfaceTypeSymbol
                    && ret.Values[0].Type.TypeKind != TypeKind.Interface
                    && ret.Values[0].Type.TypeKind != TypeKind.UntypedNil)
                {
                    var expectedType = _currentReturnTypes[0];
                    var actualType = ret.Values[0].Type;
                    var clrType = _ctx.Mapper.Map(expectedType);
                    EmitInterfaceWrapIfNeeded(actualType, expectedType, clrType);
                }

                EmitRetOrLeave();
                return;
            }

            // Multiple return values → construct ValueTuple
            for (int i = 0; i < ret.Values.Count; i++)
            {
                EmitExpression(ret.Values[i]);

                // Wrap concrete types into interface types if needed
                if (i < _currentReturnTypes.Count
                    && _currentReturnTypes[i] is InterfaceTypeSymbol
                    && ret.Values[i].Type.TypeKind != TypeKind.Interface
                    && ret.Values[i].Type.TypeKind != TypeKind.UntypedNil)
                {
                    var clrType = _ctx.Mapper.Map(_currentReturnTypes[i]);
                    EmitInterfaceWrapIfNeeded(ret.Values[i].Type, _currentReturnTypes[i], clrType);
                }
            }

            var tupleTypes = new Type[ret.Values.Count];
            for (int i = 0; i < ret.Values.Count; i++)
            {
                // Use declared return type for interfaces to get the correct tuple type
                if (i < _currentReturnTypes.Count
                    && _currentReturnTypes[i] is InterfaceTypeSymbol)
                {
                    tupleTypes[i] = _ctx.Mapper.Map(_currentReturnTypes[i]);
                }
                else
                {
                    tupleTypes[i] = _ctx.Mapper.Map(ret.Values[i].Type);
                }
            }

            var tupleType = TypeMapper.MakeValueTupleType(tupleTypes);
            EmitTupleCreate(tupleTypes);
            EmitRetOrLeave();
        }

        /// <summary>
        /// Emits a return instruction. Inside a defer-wrapped function with a catch block,
        /// stores the return value in a local and leaves the exception block.
        /// Otherwise emits a normal ret.
        /// </summary>
        private void EmitRetOrLeave()
        {
            if (_ctx.DeferReturnLocal != null)
            {
                _ctx.IL.Emit(OpCodes.Stloc, _ctx.DeferReturnLocal);
                _ctx.IL.Emit(OpCodes.Leave, _ctx.DeferExitLabel);
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Ret);
            }
        }

        internal void EmitNamedReturnValues()
        {
            if (_namedReturns!.Count == 1)
            {
                EmitLoad(_namedReturns[0]);
                return;
            }

            // Multiple named returns → construct ValueTuple
            var tupleTypes = new Type[_namedReturns.Count];
            for (int i = 0; i < _namedReturns.Count; i++)
            {
                tupleTypes[i] = _ctx.Mapper.Map(_namedReturns[i].Type);
                EmitLoad(_namedReturns[i]);
            }

            EmitTupleCreate(tupleTypes);
        }

        private void EmitTrailingReturn(TypeSymbol returnType, IReadOnlyList<TypeSymbol> returnTypes)
        {
            if (returnType == BuiltinTypes.Void)
            {
                _ctx.IL.Emit(OpCodes.Ret);
                return;
            }

            // Non-void function: emit default return value as safety net
            // This is unreachable if all paths return, but keeps the IL valid.
            var clrReturnType = _ctx.Mapper.MapReturnType(returnTypes);
            if (clrReturnType.IsValueType)
            {
                var local = _ctx.IL.DeclareLocal(clrReturnType);
                _ctx.IL.Emit(OpCodes.Ldloca, local);
                _ctx.IL.Emit(OpCodes.Initobj, clrReturnType);
                _ctx.IL.Emit(OpCodes.Ldloc, local);
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Ldnull);
            }
            _ctx.IL.Emit(OpCodes.Ret);
        }

        private void EmitExpressionStatement(ExpressionStatement stmt)
        {
            EmitExpression(stmt.Expression);

            // Pop the value if the expression leaves something on the stack
            if (stmt.Expression.Type != BuiltinTypes.Void)
            {
                _ctx.IL.Emit(OpCodes.Pop);
            }
        }

        private void EmitVarDeclaration(VarDeclaration decl)
        {
            var clrType = _ctx.Mapper.Map(decl.Symbol.Type);
            bool isCaptured = _ctx.CapturedSymbols.Contains(decl.Symbol);

            if (isCaptured)
            {
                var boxType = typeof(Box<>).MakeGenericType(clrType);
                var local = _ctx.IL.DeclareLocal(boxType);
                _ctx.Locals[decl.Symbol] = local;

                if (decl.Initializer != null)
                {
                    EmitExpression(decl.Initializer);
                    EmitInterfaceWrapIfNeeded(decl.Initializer.Type, decl.Symbol.Type, clrType);
                    var boxCtor = boxType.GetConstructor(new[] { clrType })!;
                    _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                }
                else if (decl.Symbol.Type.TypeKind == TypeKind.Struct && !clrType.IsValueType)
                {
                    // Runtime reference type (e.g. sync.WaitGroup) — construct then box
                    var ctor = clrType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        _ctx.IL.Emit(OpCodes.Newobj, ctor);
                        var boxCtor = boxType.GetConstructor(new[] { clrType })!;
                        _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                    }
                    else
                    {
                        var boxCtorEmpty = boxType.GetConstructor(Type.EmptyTypes)!;
                        _ctx.IL.Emit(OpCodes.Newobj, boxCtorEmpty);
                    }
                }
                else
                {
                    var boxCtorEmpty = boxType.GetConstructor(Type.EmptyTypes)!;
                    _ctx.IL.Emit(OpCodes.Newobj, boxCtorEmpty);
                }

                _ctx.IL.Emit(OpCodes.Stloc, local);
            }
            else
            {
                var local = _ctx.IL.DeclareLocal(clrType);
                _ctx.Locals[decl.Symbol] = local;

                if (decl.Initializer != null)
                {
                    EmitExpression(decl.Initializer);
                    EmitInterfaceWrapIfNeeded(decl.Initializer.Type, decl.Symbol.Type, clrType);
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                }
                else if (decl.Symbol.Type.TypeKind == TypeKind.Struct && !clrType.IsValueType)
                {
                    // Runtime reference type (e.g. sync.WaitGroup) — construct via default ctor
                    var ctor = clrType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        _ctx.IL.Emit(OpCodes.Newobj, ctor);
                        _ctx.IL.Emit(OpCodes.Stloc, local);
                    }
                }
            }
        }

        private void EmitMultiVarDeclaration(MultiVarDeclaration decl)
        {
            // Emit the call expression (returns ValueTuple on the stack)
            EmitExpression(decl.Initializer);

            // Get return types from the call expression for accurate tuple construction
            IReadOnlyList<TypeSymbol>? returnTypes = GetMultiReturnTypes(decl.Initializer);

            // Build the tuple type — use actual return types for blank identifiers
            var types = new Type[decl.Symbols.Count];
            for (int i = 0; i < decl.Symbols.Count; i++)
            {
                if (decl.Symbols[i] != null)
                    types[i] = _ctx.Mapper.Map(decl.Symbols[i]!.Type);
                else if (returnTypes != null && i < returnTypes.Count)
                    types[i] = _ctx.Mapper.Map(returnTypes[i]);
                else
                    types[i] = _ctx.Mapper.Map(BuiltinTypes.Int);
            }

            var tupleType = TypeMapper.MakeValueTupleType(types);

            // Store tuple in a temp local
            var tupleLocal = _ctx.IL.DeclareLocal(tupleType);
            _ctx.IL.Emit(OpCodes.Stloc, tupleLocal);

            // Extract each element into its local (new or existing for redeclaration)
            for (int i = 0; i < decl.Symbols.Count; i++)
            {
                if (decl.Symbols[i] == null) continue; // blank identifier _

                var sym = decl.Symbols[i]!;
                bool isCaptured = _ctx.CapturedSymbols.Contains(sym);
                bool isNew = !_ctx.Locals.TryGetValue(sym, out var existingLocal);

                // Load tuple field value onto stack
                EmitTupleFieldLoad(tupleLocal, tupleType, i);

                if (isNew && isCaptured)
                {
                    // New captured variable: wrap value in Box<T>
                    var boxType = typeof(Box<>).MakeGenericType(types[i]);
                    var boxCtor = boxType.GetConstructor(new[] { types[i] })!;
                    _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                    var local = _ctx.IL.DeclareLocal(boxType);
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                    _ctx.Locals[sym] = local;
                }
                else if (isNew)
                {
                    var local = _ctx.IL.DeclareLocal(types[i]);
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                    _ctx.Locals[sym] = local;
                }
                else if (isCaptured)
                {
                    // Existing Box<T> — store through .Value
                    var temp = _ctx.IL.DeclareLocal(types[i]);
                    _ctx.IL.Emit(OpCodes.Stloc, temp);
                    _ctx.IL.Emit(OpCodes.Ldloc, existingLocal!);
                    _ctx.IL.Emit(OpCodes.Ldloc, temp);
                    _ctx.IL.Emit(OpCodes.Stfld, existingLocal!.LocalType.GetField("Value")!);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Stloc, existingLocal!);
                }
            }
        }

        private void EmitMultiAssignment(MultiAssignmentStatement assign)
        {
            // Emit the call expression (returns ValueTuple on the stack)
            EmitExpression(assign.Value);

            // Get return types from the call expression for accurate tuple construction
            IReadOnlyList<TypeSymbol>? assignReturnTypes = GetMultiReturnTypes(assign.Value);

            // Build the tuple type from the targets — use actual return types for blanks
            var types = new Type[assign.Targets.Count];
            for (int i = 0; i < assign.Targets.Count; i++)
            {
                if (assign.Targets[i] != null)
                    types[i] = _ctx.Mapper.Map(assign.Targets[i]!.Type);
                else if (assignReturnTypes != null && i < assignReturnTypes.Count)
                    types[i] = _ctx.Mapper.Map(assignReturnTypes[i]);
                else
                    types[i] = typeof(long);
            }

            var tupleType = TypeMapper.MakeValueTupleType(types);

            // Store tuple in a temp local
            var tupleLocal = _ctx.IL.DeclareLocal(tupleType);
            _ctx.IL.Emit(OpCodes.Stloc, tupleLocal);

            // Assign each element to its target
            for (int i = 0; i < assign.Targets.Count; i++)
            {
                if (assign.Targets[i] == null) continue; // blank identifier _

                var target = assign.Targets[i]!;
                EmitTupleFieldLoad(tupleLocal, tupleType, i);

                if (target is IdentifierExpression id)
                {
                    EmitStore(id.Symbol);
                }
                else if (target is SelectorExpression sel)
                {
                    // Need to load the field address first, so reorder:
                    // store value in temp, load target address, load temp, stfld
                    var tempLocal = _ctx.IL.DeclareLocal(types[i]);
                    _ctx.IL.Emit(OpCodes.Stloc, tempLocal);
                    EmitAddressForStore(sel.Target);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempLocal);
                    if (_ctx.StructFields.TryGetValue(sel.Field, out var fb))
                    {
                        _ctx.IL.Emit(OpCodes.Stfld, fb);
                    }
                }
            }
        }

        private void EmitTupleCreate(Type[] elementTypes)
        {
            if (elementTypes.Length <= 7)
            {
                var tupleType = TypeMapper.MakeValueTupleType(elementTypes);
                var ctor = tupleType.GetConstructor(elementTypes)!;
                _ctx.IL.Emit(OpCodes.Newobj, ctor);
                return;
            }

            // 8+ elements: nested ValueTuple<T1..T7, ValueTuple<T8...>>
            // The values are already on the stack in order.
            // We need to construct the inner tuple from items 8+ first,
            // but they're on top of the stack already (after items 1-7).
            // Strategy: store items 1-7 in temps, construct inner, then outer.
            var first7 = new Type[7];
            Array.Copy(elementTypes, first7, 7);
            var rest = new Type[elementTypes.Length - 7];
            Array.Copy(elementTypes, 7, rest, 0, rest.Length);

            // Store rest items (they're on top of the stack) into temps, in reverse order
            var restTemps = new LocalBuilder[rest.Length];
            for (int i = rest.Length - 1; i >= 0; i--)
            {
                restTemps[i] = _ctx.IL.DeclareLocal(rest[i]);
                _ctx.IL.Emit(OpCodes.Stloc, restTemps[i]);
            }

            // Store first 7 into temps (in reverse order)
            var first7Temps = new LocalBuilder[7];
            for (int i = 6; i >= 0; i--)
            {
                first7Temps[i] = _ctx.IL.DeclareLocal(first7[i]);
                _ctx.IL.Emit(OpCodes.Stloc, first7Temps[i]);
            }

            // Load first 7 back
            for (int i = 0; i < 7; i++)
                _ctx.IL.Emit(OpCodes.Ldloc, first7Temps[i]);

            // Load rest and create inner tuple
            for (int i = 0; i < rest.Length; i++)
                _ctx.IL.Emit(OpCodes.Ldloc, restTemps[i]);
            EmitTupleCreate(rest);

            // Create the outer tuple
            var innerType = TypeMapper.MakeValueTupleType(rest);
            var outerTypes = new Type[8];
            Array.Copy(first7, outerTypes, 7);
            outerTypes[7] = innerType;
            var outerTupleType = typeof(ValueTuple<,,,,,,,>).MakeGenericType(outerTypes);
            var outerCtor = outerTupleType.GetConstructor(outerTypes)!;
            _ctx.IL.Emit(OpCodes.Newobj, outerCtor);
        }

        private void EmitTupleFieldLoad(LocalBuilder tupleLocal, Type tupleType, int index)
        {
            if (index < 7)
            {
                _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, tupleType.GetField($"Item{index + 1}")!);
                return;
            }

            // Nested tuple: navigate through Rest fields
            _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
            var currentType = tupleType;
            int remaining = index;

            while (remaining >= 7)
            {
                var restField = currentType.GetField("Rest")!;
                _ctx.IL.Emit(OpCodes.Ldflda, restField);
                currentType = restField.FieldType;
                remaining -= 7;
            }

            _ctx.IL.Emit(OpCodes.Ldfld, currentType.GetField($"Item{remaining + 1}")!);
        }

        private IReadOnlyList<TypeSymbol>? GetMultiReturnTypes(Expression expr)
        {
            if (expr is CallExpression call)
                return call.EffectiveReturnTypes;
            if (expr is MethodCallExpression methodCall)
                return methodCall.Method.ReturnTypes;
            if (expr is IndexExpression idx && idx.IsCommaOk && idx.Target.Type is MapTypeSymbol mapType)
                return new[] { mapType.ValueType, BuiltinTypes.Bool };
            if (expr is TypeAssertExpression ta && ta.IsCommaOk)
                return new[] { ta.AssertedType, BuiltinTypes.Bool };
            if (expr is ReceiveExpression recv && recv.IsCommaOk)
                return new[] { recv.ElementType, BuiltinTypes.Bool };
            return null;
        }

        private void EmitParallelAssignment(ParallelAssignmentStatement assign)
        {
            // Evaluate all RHS values into temp locals first
            var temps = new LocalBuilder[assign.Values.Count];
            for (int i = 0; i < assign.Values.Count; i++)
            {
                EmitExpression(assign.Values[i]);
                var clrType = _ctx.Mapper.Map(assign.Values[i].Type);
                temps[i] = _ctx.IL.DeclareLocal(clrType);
                _ctx.IL.Emit(OpCodes.Stloc, temps[i]);
            }

            // Assign each temp to its target
            for (int i = 0; i < assign.Targets.Count; i++)
            {
                if (assign.Targets[i] == null) continue; // blank identifier

                _ctx.IL.Emit(OpCodes.Ldloc, temps[i]);
                var target = assign.Targets[i]!;
                if (target is IdentifierExpression id)
                {
                    EmitStore(id.Symbol);
                }
                else if (target is SelectorExpression sel)
                {
                    var tempVal = _ctx.IL.DeclareLocal(_ctx.Mapper.Map(target.Type));
                    _ctx.IL.Emit(OpCodes.Stloc, tempVal);
                    EmitAddressForStore(sel.Target);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempVal);
                    if (_ctx.StructFields.TryGetValue(sel.Field, out var fb))
                        _ctx.IL.Emit(OpCodes.Stfld, fb);
                }
                else if (target is IndexExpression idx)
                {
                    var tempVal = _ctx.IL.DeclareLocal(_ctx.Mapper.Map(target.Type));
                    _ctx.IL.Emit(OpCodes.Stloc, tempVal);
                    EmitExpression(idx.Target);
                    EmitExpression(idx.Index);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempVal);
                    var mapClrType = _ctx.Mapper.Map(idx.Target.Type);
                    var setter = mapClrType.GetProperty("Item")!.GetSetMethod()!;
                    _ctx.IL.Emit(OpCodes.Call, setter);
                }
                else if (target is DerefExpression deref)
                {
                    var tempVal = _ctx.IL.DeclareLocal(_ctx.Mapper.Map(target.Type));
                    _ctx.IL.Emit(OpCodes.Stloc, tempVal);
                    EmitExpression(deref.Operand);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempVal);
                    var ptrType = _ctx.Mapper.Map(deref.Operand.Type);
                    var valueField = ptrType.GetField("Value")!;
                    _ctx.IL.Emit(OpCodes.Stfld, valueField);
                }
            }
        }

        private void EmitAssignment(AssignmentStatement assign)
        {
            switch (assign.Target)
            {
                case IdentifierExpression id:
                    EmitExpression(assign.Value);
                    EmitStore(id.Symbol);
                    break;

                case SelectorExpression sel:
                    // p.X = value → load address of p, then stfld
                    EmitAddressForStore(sel.Target);
                    EmitExpression(assign.Value);
                    if (_ctx.StructFields.TryGetValue(sel.Field, out var fb))
                    {
                        _ctx.IL.Emit(OpCodes.Stfld, fb);
                    }
                    break;

                case IndexExpression idx:
                    EmitIndexAssignment(idx, assign.Value);
                    break;

                case DerefExpression deref:
                    // *p = value → p.Value = value
                    EmitExpression(deref.Operand);
                    EmitExpression(assign.Value);
                    var innerType = _ctx.Mapper.Map(deref.Type);
                    var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                    _ctx.IL.Emit(OpCodes.Stfld, ptrType.GetField("Value")!);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Assignment target not supported: {assign.Target.NodeType}");
            }
        }

        private void EmitIncDec(IncDecStatement incDec)
        {
            var id = (IdentifierExpression)incDec.Operand;
            EmitLoad(id.Symbol);
            EmitIntConstant(1, id.Type);
            _ctx.IL.Emit(incDec.IsIncrement ? OpCodes.Add : OpCodes.Sub);
            EmitStore(id.Symbol);
        }

        private void EmitIf(IfStatement ifStmt)
        {
            var elseLabel = _ctx.IL.DefineLabel();
            var endLabel = _ctx.IL.DefineLabel();

            if (ifStmt.Init != null)
                EmitStatement(ifStmt.Init);

            EmitExpression(ifStmt.Condition);
            _ctx.IL.Emit(OpCodes.Brfalse, ifStmt.ElseBody != null ? elseLabel : endLabel);
            EmitBlock(ifStmt.Body);
            if (ifStmt.ElseBody != null)
            {
                _ctx.IL.Emit(OpCodes.Br, endLabel);
                _ctx.IL.MarkLabel(elseLabel);
                EmitStatement(ifStmt.ElseBody);
            }

            _ctx.IL.MarkLabel(endLabel);
        }

        private void EmitFor(ForStatement forStmt)
        {
            var condLabel = _ctx.IL.DefineLabel();
            var endLabel = _ctx.IL.DefineLabel();
            var continueLabel = _ctx.IL.DefineLabel();

            if (forStmt.Init != null)
                EmitStatement(forStmt.Init);

            // Go 1.22: collect captured loop variables for per-iteration scoping
            var capturedLoopVars = CollectCapturedLoopVars(forStmt.Init);
            var counterLocals = new Dictionary<Symbol, LocalBuilder>();

            if (capturedLoopVars.Count > 0)
            {
                // Create non-captured counter locals for condition/post
                foreach (var sym in capturedLoopVars)
                {
                    var boxLocal = _ctx.Locals[sym];
                    var boxType = boxLocal.LocalType;
                    var innerType = boxType.GetField("Value")!.FieldType;
                    var counterLocal = _ctx.IL.DeclareLocal(innerType);
                    counterLocals[sym] = counterLocal;

                    // Copy initial value from Box to counter
                    var valueField = boxType.GetField("Value")!;
                    _ctx.IL.Emit(OpCodes.Ldloc, boxLocal);
                    _ctx.IL.Emit(OpCodes.Ldfld, valueField);
                    _ctx.IL.Emit(OpCodes.Stloc, counterLocal);
                }
            }

            _ctx.IL.MarkLabel(condLabel);

            if (capturedLoopVars.Count > 0)
            {
                // Condition uses counter locals: temporarily swap locals
                foreach (var sym in capturedLoopVars)
                {
                    _ctx.CapturedSymbols.Remove(sym);
                    _ctx.Locals[sym] = counterLocals[sym];
                }
            }

            if (forStmt.Condition != null)
            {
                EmitExpression(forStmt.Condition);
                _ctx.IL.Emit(OpCodes.Brfalse, endLabel);
            }

            if (capturedLoopVars.Count > 0)
            {
                // Create new Box per iteration and restore captured mapping
                foreach (var sym in capturedLoopVars)
                {
                    var counterLocal = counterLocals[sym];
                    var innerType = counterLocal.LocalType;
                    var boxType = typeof(Box<>).MakeGenericType(innerType);
                    var boxCtor = boxType.GetConstructor(new[] { innerType })!;
                    var boxLocal = _ctx.IL.DeclareLocal(boxType);

                    _ctx.IL.Emit(OpCodes.Ldloc, counterLocal);
                    _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                    _ctx.IL.Emit(OpCodes.Stloc, boxLocal);

                    _ctx.CapturedSymbols.Add(sym);
                    _ctx.Locals[sym] = boxLocal;
                }
            }

            PushLoopLabels(endLabel, continueLabel);
            EmitBlock(forStmt.Body);
            _ctx.LoopLabels.Pop();

            _ctx.IL.MarkLabel(continueLabel);

            if (capturedLoopVars.Count > 0)
            {
                // Write back from Box to counter, then use counter for post
                foreach (var sym in capturedLoopVars)
                {
                    var boxLocal = _ctx.Locals[sym];
                    var valueField = boxLocal.LocalType.GetField("Value")!;
                    _ctx.IL.Emit(OpCodes.Ldloc, boxLocal);
                    _ctx.IL.Emit(OpCodes.Ldfld, valueField);
                    _ctx.IL.Emit(OpCodes.Stloc, counterLocals[sym]);

                    _ctx.CapturedSymbols.Remove(sym);
                    _ctx.Locals[sym] = counterLocals[sym];
                }
            }

            if (forStmt.Post != null)
                EmitStatement(forStmt.Post);

            if (capturedLoopVars.Count > 0)
            {
                // Restore captured mapping for next iteration's condition check
                // (will be swapped to counter at condLabel)
                foreach (var sym in capturedLoopVars)
                    _ctx.CapturedSymbols.Add(sym);
            }

            _ctx.IL.Emit(OpCodes.Br, condLabel);
            _ctx.IL.MarkLabel(endLabel);

            // Restore final state: keep counter locals as the active locals
            if (capturedLoopVars.Count > 0)
            {
                foreach (var sym in capturedLoopVars)
                {
                    _ctx.CapturedSymbols.Remove(sym);
                    _ctx.Locals[sym] = counterLocals[sym];
                }
            }
        }

        private List<Symbol> CollectCapturedLoopVars(AstNode? init)
        {
            var result = new List<Symbol>();
            if (init == null) return result;

            if (init is VarDeclaration varDecl && _ctx.CapturedSymbols.Contains(varDecl.Symbol))
            {
                result.Add(varDecl.Symbol);
            }
            else if (init is BlockStatement block)
            {
                foreach (var stmt in block.Statements)
                {
                    if (stmt is VarDeclaration vd && _ctx.CapturedSymbols.Contains(vd.Symbol))
                        result.Add(vd.Symbol);
                }
            }

            return result;
        }

        private void EmitSwitch(SwitchStatement sw)
        {
            var endLabel = _ctx.IL.DefineLabel();

            if (sw.Init != null)
                EmitStatement(sw.Init);

            // Emit tag value once into a local if present
            LocalBuilder? tagLocal = null;
            if (sw.Tag != null)
            {
                var tagType = _ctx.Mapper.Map(sw.Tag.Type);
                tagLocal = _ctx.IL.DeclareLocal(tagType);
                EmitExpression(sw.Tag);
                _ctx.IL.Emit(OpCodes.Stloc, tagLocal);
            }

            // Pre-define body labels for fallthrough support
            var bodyLabels = new Label[sw.Cases.Count];
            for (int i = 0; i < sw.Cases.Count; i++)
                bodyLabels[i] = _ctx.IL.DefineLabel();

            var savedFallthroughLabel = _ctx.FallthroughLabel;

            // Emit as if-else chain
            var nextCaseLabel = _ctx.IL.DefineLabel();
            for (int ci = 0; ci < sw.Cases.Count; ci++)
            {
                var c = sw.Cases[ci];

                _ctx.IL.MarkLabel(nextCaseLabel);
                nextCaseLabel = _ctx.IL.DefineLabel();

                if (!c.IsDefault && c.Expressions != null)
                {
                    // Check each case expression
                    for (int i = 0; i < c.Expressions.Count; i++)
                    {
                        if (tagLocal != null)
                        {
                            // Tagged switch: compare tag == case expr
                            _ctx.IL.Emit(OpCodes.Ldloc, tagLocal);
                            EmitExpression(c.Expressions[i]);
                            if (sw.Tag!.Type.TypeKind == TypeKind.String || sw.Tag.Type.TypeKind == TypeKind.UntypedString)
                            {
                                var strEq = typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) })!;
                                _ctx.IL.Emit(OpCodes.Call, strEq);
                            }
                            else
                            {
                                _ctx.IL.Emit(OpCodes.Ceq);
                            }
                        }
                        else
                        {
                            // Tagless switch: case expr is already a boolean condition
                            EmitExpression(c.Expressions[i]);
                        }

                        if (i < c.Expressions.Count - 1)
                        {
                            _ctx.IL.Emit(OpCodes.Brtrue, bodyLabels[ci]);
                        }
                        else
                        {
                            _ctx.IL.Emit(OpCodes.Brfalse, nextCaseLabel);
                        }
                    }
                }

                _ctx.IL.MarkLabel(bodyLabels[ci]);

                // Set fallthrough target to next case's body (or end if last)
                _ctx.FallthroughLabel = ci + 1 < sw.Cases.Count ? bodyLabels[ci + 1] : endLabel;

                // Check if last statement is fallthrough to avoid emitting br endLabel
                bool hasFallthrough = c.Body.Count > 0
                    && c.Body[c.Body.Count - 1] is BranchStatement bs
                    && bs.BranchKind == BranchKind.Fallthrough;

                foreach (var stmt in c.Body)
                {
                    EmitStatement(stmt);
                }

                if (!hasFallthrough)
                    _ctx.IL.Emit(OpCodes.Br, endLabel);
            }

            _ctx.FallthroughLabel = savedFallthroughLabel;
            _ctx.IL.MarkLabel(nextCaseLabel);
            _ctx.IL.MarkLabel(endLabel);
        }

        private void EmitTypeSwitch(TypeSwitchStatement ts)
        {
            var endLabel = _ctx.IL.DefineLabel();

            if (ts.Init != null)
                EmitStatement(ts.Init);

            // Emit guard expression and store as object for isinst checks
            EmitExpression(ts.GuardExpression);
            var guardClrType = _ctx.Mapper.Map(ts.GuardExpression.Type);
            if (guardClrType.IsValueType)
                _ctx.IL.Emit(OpCodes.Box, guardClrType);

            var guardLocal = _ctx.IL.DeclareLocal(typeof(object));
            _ctx.IL.Emit(OpCodes.Stloc, guardLocal);

            // Also store the unwrapped value for struct type cases (handles interface wrappers)
            _ctx.IL.Emit(OpCodes.Ldloc, guardLocal);
            _ctx.IL.Emit(OpCodes.Call,
                typeof(Ngo.Runtime.BuiltIn).GetMethod("UnwrapInterface")!);
            var unwrappedLocal = _ctx.IL.DeclareLocal(typeof(object));
            _ctx.IL.Emit(OpCodes.Stloc, unwrappedLocal);

            // Find default case index (emit it last)
            int defaultIndex = -1;
            for (int i = 0; i < ts.Cases.Count; i++)
            {
                if (ts.Cases[i].IsDefault)
                {
                    defaultIndex = i;
                    break;
                }
            }

            // Emit non-default cases as if-else chain using isinst
            foreach (var c in ts.Cases)
            {
                if (c.IsDefault)
                    continue;

                var nextCaseLabel = _ctx.IL.DefineLabel();

                if (c.Types != null && c.Types.Count > 0)
                {
                    if (c.Types.Count == 1)
                    {
                        // Single type: try isinst on guard, then on unwrapped value
                        var clrType = _ctx.Mapper.Map(c.Types[0]);
                        _ctx.IL.Emit(OpCodes.Ldloc, guardLocal);
                        _ctx.IL.Emit(OpCodes.Isinst, clrType);
                        var matchedLabel = _ctx.IL.DefineLabel();
                        _ctx.IL.Emit(OpCodes.Brtrue, matchedLabel);
                        // Try unwrapped value (handles struct inside interface wrapper)
                        _ctx.IL.Emit(OpCodes.Ldloc, unwrappedLocal);
                        _ctx.IL.Emit(OpCodes.Isinst, clrType);
                        _ctx.IL.Emit(OpCodes.Brfalse, nextCaseLabel);
                        _ctx.IL.MarkLabel(matchedLabel);
                    }
                    else
                    {
                        // Multiple types: OR them together
                        var bodyLabel = _ctx.IL.DefineLabel();
                        for (int i = 0; i < c.Types.Count; i++)
                        {
                            var clrType = _ctx.Mapper.Map(c.Types[i]);
                            // Try guard first
                            _ctx.IL.Emit(OpCodes.Ldloc, guardLocal);
                            _ctx.IL.Emit(OpCodes.Isinst, clrType);
                            _ctx.IL.Emit(OpCodes.Brtrue, bodyLabel);
                            // Try unwrapped value
                            _ctx.IL.Emit(OpCodes.Ldloc, unwrappedLocal);
                            _ctx.IL.Emit(OpCodes.Isinst, clrType);
                            if (i < c.Types.Count - 1)
                                _ctx.IL.Emit(OpCodes.Brtrue, bodyLabel);
                            else
                                _ctx.IL.Emit(OpCodes.Brfalse, nextCaseLabel);
                        }
                        _ctx.IL.MarkLabel(bodyLabel);
                    }
                }

                // Store typed value in assigned variable if present
                if (c.AssignedSymbol != null)
                {
                    var varType = _ctx.Mapper.Map(c.AssignedSymbol.Type);
                    var varLocal = _ctx.IL.DeclareLocal(varType);
                    _ctx.Locals[c.AssignedSymbol] = varLocal;

                    if (varType.IsValueType)
                    {
                        // Use unwrapped value for struct extraction (handles wrappers)
                        _ctx.IL.Emit(OpCodes.Ldloc, unwrappedLocal);
                        _ctx.IL.Emit(OpCodes.Unbox_Any, varType);
                    }
                    else
                    {
                        // For interface/ref types, try guard first, fall back to unwrapped
                        _ctx.IL.Emit(OpCodes.Ldloc, guardLocal);
                        _ctx.IL.Emit(OpCodes.Isinst, varType);
                        _ctx.IL.Emit(OpCodes.Dup);
                        var assignOk = _ctx.IL.DefineLabel();
                        _ctx.IL.Emit(OpCodes.Brtrue, assignOk);
                        _ctx.IL.Emit(OpCodes.Pop);
                        _ctx.IL.Emit(OpCodes.Ldloc, unwrappedLocal);
                        _ctx.IL.Emit(OpCodes.Castclass, varType);
                        _ctx.IL.MarkLabel(assignOk);
                    }
                    _ctx.IL.Emit(OpCodes.Stloc, varLocal);
                }

                foreach (var stmt in c.Body)
                    EmitStatement(stmt);

                _ctx.IL.Emit(OpCodes.Br, endLabel);
                _ctx.IL.MarkLabel(nextCaseLabel);
            }

            // Emit default case
            if (defaultIndex >= 0)
            {
                var defaultCase = ts.Cases[defaultIndex];

                if (defaultCase.AssignedSymbol != null)
                {
                    var varType = _ctx.Mapper.Map(defaultCase.AssignedSymbol.Type);
                    var varLocal = _ctx.IL.DeclareLocal(varType);
                    _ctx.Locals[defaultCase.AssignedSymbol] = varLocal;

                    _ctx.IL.Emit(OpCodes.Ldloc, guardLocal);
                    if (varType.IsValueType)
                        _ctx.IL.Emit(OpCodes.Unbox_Any, varType);
                    else if (varType != typeof(object))
                        _ctx.IL.Emit(OpCodes.Castclass, varType);
                    _ctx.IL.Emit(OpCodes.Stloc, varLocal);
                }

                foreach (var stmt in defaultCase.Body)
                    EmitStatement(stmt);
            }

            _ctx.IL.MarkLabel(endLabel);
        }

        private void EmitBranch(BranchStatement branch)
        {
            // Goto: jump to a goto label
            if (branch.BranchKind == BranchKind.Goto && branch.Label != null)
            {
                var target = GetOrCreateGotoLabel(branch.Label);
                _ctx.IL.Emit(OpCodes.Br, target);
                return;
            }

            // Labeled break/continue: look up the named label
            if (branch.Label != null)
            {
                if (_ctx.NamedLabels.TryGetValue(branch.Label, out var named))
                {
                    switch (branch.BranchKind)
                    {
                        case BranchKind.Break:
                            _ctx.IL.Emit(OpCodes.Br, named.breakLabel);
                            break;
                        case BranchKind.Continue:
                            _ctx.IL.Emit(OpCodes.Br, named.continueLabel);
                            break;
                    }
                }
                return;
            }

            if (branch.BranchKind == BranchKind.Fallthrough)
            {
                if (_ctx.FallthroughLabel != null)
                    _ctx.IL.Emit(OpCodes.Br, _ctx.FallthroughLabel.Value);
                return;
            }

            if (_ctx.LoopLabels.Count == 0)
                return;

            var (breakLabel, continueLabel) = _ctx.LoopLabels.Peek();
            switch (branch.BranchKind)
            {
                case BranchKind.Break:
                    _ctx.IL.Emit(OpCodes.Br, breakLabel);
                    break;
                case BranchKind.Continue:
                    _ctx.IL.Emit(OpCodes.Br, continueLabel);
                    break;
            }
        }

        private void EmitLabeledStatement(LabeledStatement labeled)
        {
            // Mark as goto target
            var gotoLabel = GetOrCreateGotoLabel(labeled.Label);
            _ctx.IL.MarkLabel(gotoLabel);

            // Set pending label name so the next loop emitter registers its labels under this name
            _pendingLabel = labeled.Label;
            EmitStatement(labeled.InnerStatement);
            _pendingLabel = null;
            _ctx.NamedLabels.Remove(labeled.Label);
        }

        private Label GetOrCreateGotoLabel(string name)
        {
            if (!_ctx.GotoLabels.TryGetValue(name, out var label))
            {
                label = _ctx.IL.DefineLabel();
                _ctx.GotoLabels[name] = label;
            }
            return label;
        }

        internal void PushLoopLabels(Label breakLabel, Label continueLabel)
        {
            _ctx.LoopLabels.Push((breakLabel, continueLabel));
            if (_pendingLabel != null)
            {
                _ctx.NamedLabels[_pendingLabel] = (breakLabel, continueLabel);
                _pendingLabel = null;
            }
        }

        // --- Expressions ---

        public void EmitExpression(Expression expr)
        {
            switch (expr.NodeType)
            {
                case NodeType.LiteralExpression:
                    EmitLiteral((LiteralExpression)expr);
                    break;
                case NodeType.IdentifierExpression:
                    EmitIdentifier((IdentifierExpression)expr);
                    break;
                case NodeType.BinaryExpression:
                    EmitBinary((BinaryExpression)expr);
                    break;
                case NodeType.UnaryExpression:
                    EmitUnary((UnaryExpression)expr);
                    break;
                case NodeType.CallExpression:
                    var callExpr = (CallExpression)expr;
                    EmitCall(callExpr);
                    if (callExpr.IsSpreadArg)
                    {
                        // Store the tuple result for SpreadElement access
                        var tupleType = _ctx.Mapper.MapReturnType(callExpr.EffectiveReturnTypes);
                        var tupleLocal = _ctx.IL.DeclareLocal(tupleType);
                        _ctx.IL.Emit(OpCodes.Stloc, tupleLocal);
                        _spreadLocals[callExpr] = tupleLocal;
                        // Load Item1 (the first element) for the first argument position
                        _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
                        _ctx.IL.Emit(OpCodes.Ldfld, tupleType.GetField("Item1")!);
                    }
                    break;
                case NodeType.SpreadElement:
                    var spread = (SpreadElement)expr;
                    if (_spreadLocals.TryGetValue(spread.Source, out var srcLocal))
                    {
                        var srcType = srcLocal.LocalType;
                        _ctx.IL.Emit(OpCodes.Ldloca, srcLocal);
                        _ctx.IL.Emit(OpCodes.Ldfld, srcType.GetField($"Item{spread.Index + 1}")!);
                    }
                    break;
                case NodeType.ConversionExpression:
                    EmitConversion((ConversionExpression)expr);
                    break;
                case NodeType.SelectorExpression:
                    EmitSelector((SelectorExpression)expr);
                    break;
                case NodeType.MethodCallExpression:
                    EmitMethodCall((MethodCallExpression)expr);
                    break;
                case NodeType.CompositeLiteralExpression:
                    EmitCompositeLiteral((CompositeLiteralExpression)expr);
                    break;
                case NodeType.AddressOfExpression:
                    EmitAddressOf((AddressOfExpression)expr);
                    break;
                case NodeType.DerefExpression:
                    EmitDeref((DerefExpression)expr);
                    break;
                case NodeType.IndexExpression:
                    EmitIndex((IndexExpression)expr);
                    break;
                case NodeType.SliceExpression:
                    EmitSlice((SliceExpression)expr);
                    break;
                case NodeType.TypeAssertExpression:
                    EmitTypeAssert((TypeAssertExpression)expr);
                    break;
                case NodeType.FunctionLiteralExpression:
                    Closures.EmitFunctionLiteral((FunctionLiteralExpression)expr);
                    break;
                case NodeType.MethodValueExpression:
                    Closures.EmitMethodValue((MethodValueExpression)expr);
                    break;
                case NodeType.ReceiveExpression:
                    _deferGo.EmitReceive((ReceiveExpression)expr);
                    break;
                default:
                    throw new NotSupportedException($"Expression emission not supported for: {expr.NodeType}");
            }
        }

        private void EmitLiteral(LiteralExpression lit)
        {
            if (lit.Value == null)
            {
                _ctx.IL.Emit(OpCodes.Ldnull);
                return;
            }

            switch (lit.Type.TypeKind)
            {
                case TypeKind.Bool:
                case TypeKind.UntypedBool:
                    _ctx.IL.Emit((bool)lit.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                    break;
                case TypeKind.Int:
                case TypeKind.Int64:
                case TypeKind.UntypedInt:
                    _ctx.IL.Emit(OpCodes.Ldc_I8, Convert.ToInt64(lit.Value));
                    break;
                case TypeKind.Int32:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, Convert.ToInt32(lit.Value));
                    break;
                case TypeKind.Int8:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, (int)Convert.ToSByte(lit.Value));
                    break;
                case TypeKind.Int16:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, (int)Convert.ToInt16(lit.Value));
                    break;
                case TypeKind.Uint8:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, (int)Convert.ToByte(lit.Value));
                    break;
                case TypeKind.Uint16:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, (int)Convert.ToUInt16(lit.Value));
                    break;
                case TypeKind.Uint32:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, unchecked((int)Convert.ToUInt32(lit.Value)));
                    break;
                case TypeKind.Uint:
                case TypeKind.Uint64:
                    _ctx.IL.Emit(OpCodes.Ldc_I8, unchecked((long)Convert.ToUInt64(lit.Value)));
                    break;
                case TypeKind.Float32:
                    _ctx.IL.Emit(OpCodes.Ldc_R4, Convert.ToSingle(lit.Value));
                    break;
                case TypeKind.Float64:
                case TypeKind.UntypedFloat:
                    _ctx.IL.Emit(OpCodes.Ldc_R8, Convert.ToDouble(lit.Value));
                    break;
                case TypeKind.Complex64:
                case TypeKind.Complex128:
                case TypeKind.UntypedComplex:
                {
                    // Imaginary literal: value is the imaginary part, real part is 0
                    _ctx.IL.Emit(OpCodes.Ldc_R8, 0.0);
                    _ctx.IL.Emit(OpCodes.Ldc_R8, Convert.ToDouble(lit.Value));
                    var ctor = typeof(System.Numerics.Complex).GetConstructor(
                        new[] { typeof(double), typeof(double) })!;
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                    break;
                }
                case TypeKind.String:
                case TypeKind.UntypedString:
                    _ctx.IL.Emit(OpCodes.Ldstr, (string)lit.Value);
                    break;
                default:
                    throw new NotSupportedException($"Literal type not supported: {lit.Type.TypeKind}");
            }
        }

        private void EmitIdentifier(IdentifierExpression id)
        {
            EmitLoad(id.Symbol);
        }

        private void EmitBinary(BinaryExpression bin)
        {
            // Short-circuit for logical operators
            if (bin.Operator == BinaryOperator.LogicalAnd)
            {
                var falseLabel = _ctx.IL.DefineLabel();
                var endLabel = _ctx.IL.DefineLabel();
                EmitExpression(bin.Left);
                _ctx.IL.Emit(OpCodes.Brfalse, falseLabel);
                EmitExpression(bin.Right);
                _ctx.IL.Emit(OpCodes.Br, endLabel);
                _ctx.IL.MarkLabel(falseLabel);
                _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                _ctx.IL.MarkLabel(endLabel);
                return;
            }

            if (bin.Operator == BinaryOperator.LogicalOr)
            {
                var trueLabel = _ctx.IL.DefineLabel();
                var endLabel = _ctx.IL.DefineLabel();
                EmitExpression(bin.Left);
                _ctx.IL.Emit(OpCodes.Brtrue, trueLabel);
                EmitExpression(bin.Right);
                _ctx.IL.Emit(OpCodes.Br, endLabel);
                _ctx.IL.MarkLabel(trueLabel);
                _ctx.IL.Emit(OpCodes.Ldc_I4_1);
                _ctx.IL.MarkLabel(endLabel);
                return;
            }

            // String concatenation
            if (bin.Operator == BinaryOperator.Add &&
                (bin.Type.TypeKind == TypeKind.String || bin.Type.TypeKind == TypeKind.UntypedString))
            {
                EmitExpression(bin.Left);
                EmitExpression(bin.Right);
                var concat = typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) })!;
                _ctx.IL.Emit(OpCodes.Call, concat);
                return;
            }

            // String comparison
            if (bin.Left.Type.TypeKind == TypeKind.String || bin.Left.Type.TypeKind == TypeKind.UntypedString)
            {
                if (bin.Operator == BinaryOperator.Equal || bin.Operator == BinaryOperator.NotEqual)
                {
                    EmitExpression(bin.Left);
                    EmitExpression(bin.Right);
                    var equals = typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) })!;
                    _ctx.IL.Emit(OpCodes.Call, equals);
                    if (bin.Operator == BinaryOperator.NotEqual)
                    {
                        _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                        _ctx.IL.Emit(OpCodes.Ceq);
                    }
                    return;
                }

                if (bin.Operator == BinaryOperator.Less || bin.Operator == BinaryOperator.Greater
                    || bin.Operator == BinaryOperator.LessOrEqual || bin.Operator == BinaryOperator.GreaterOrEqual)
                {
                    EmitExpression(bin.Left);
                    EmitExpression(bin.Right);
                    var compareOrdinal = typeof(string).GetMethod("CompareOrdinal", new[] { typeof(string), typeof(string) })!;
                    _ctx.IL.Emit(OpCodes.Call, compareOrdinal);
                    _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                    switch (bin.Operator)
                    {
                        case BinaryOperator.Less:
                            _ctx.IL.Emit(OpCodes.Clt);
                            break;
                        case BinaryOperator.Greater:
                            _ctx.IL.Emit(OpCodes.Cgt);
                            break;
                        case BinaryOperator.LessOrEqual:
                            _ctx.IL.Emit(OpCodes.Cgt);
                            _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                            _ctx.IL.Emit(OpCodes.Ceq);
                            break;
                        case BinaryOperator.GreaterOrEqual:
                            _ctx.IL.Emit(OpCodes.Clt);
                            _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                            _ctx.IL.Emit(OpCodes.Ceq);
                            break;
                    }
                    return;
                }
            }

            // Complex arithmetic and equality
            if (TypeChecker.IsComplex(bin.Type) || TypeChecker.IsComplex(bin.Left.Type))
            {
                EmitExpression(bin.Left);
                EmitExpression(bin.Right);
                var complexType = typeof(System.Numerics.Complex);

                switch (bin.Operator)
                {
                    case BinaryOperator.Add:
                        _ctx.IL.Emit(OpCodes.Call, complexType.GetMethod("op_Addition",
                            new[] { complexType, complexType })!);
                        return;
                    case BinaryOperator.Subtract:
                        _ctx.IL.Emit(OpCodes.Call, complexType.GetMethod("op_Subtraction",
                            new[] { complexType, complexType })!);
                        return;
                    case BinaryOperator.Multiply:
                        _ctx.IL.Emit(OpCodes.Call, complexType.GetMethod("op_Multiply",
                            new[] { complexType, complexType })!);
                        return;
                    case BinaryOperator.Divide:
                        _ctx.IL.Emit(OpCodes.Call, complexType.GetMethod("op_Division",
                            new[] { complexType, complexType })!);
                        return;
                    case BinaryOperator.Equal:
                        _ctx.IL.Emit(OpCodes.Call, complexType.GetMethod("op_Equality",
                            new[] { complexType, complexType })!);
                        return;
                    case BinaryOperator.NotEqual:
                        _ctx.IL.Emit(OpCodes.Call, complexType.GetMethod("op_Inequality",
                            new[] { complexType, complexType })!);
                        return;
                }
            }

            // Struct equality: field-by-field comparison
            if (bin.Left.Type is StructTypeSymbol structType
                && (bin.Operator == BinaryOperator.Equal || bin.Operator == BinaryOperator.NotEqual))
            {
                EmitStructEquality(bin, structType);
                return;
            }

            // Array equality: element-by-element comparison
            if (bin.Left.Type.TypeKind == TypeKind.Array
                && (bin.Operator == BinaryOperator.Equal || bin.Operator == BinaryOperator.NotEqual))
            {
                EmitArrayEquality(bin, (ArrayTypeSymbol)bin.Left.Type);
                return;
            }

            EmitExpression(bin.Left);
            EmitExpression(bin.Right);

            var isUnsigned = IsUnsignedType(bin.Left.Type);

            switch (bin.Operator)
            {
                case BinaryOperator.Add: _ctx.IL.Emit(OpCodes.Add); break;
                case BinaryOperator.Subtract: _ctx.IL.Emit(OpCodes.Sub); break;
                case BinaryOperator.Multiply: _ctx.IL.Emit(OpCodes.Mul); break;
                case BinaryOperator.Divide:
                    _ctx.IL.Emit(isUnsigned ? OpCodes.Div_Un : OpCodes.Div);
                    break;
                case BinaryOperator.Remainder:
                    _ctx.IL.Emit(isUnsigned ? OpCodes.Rem_Un : OpCodes.Rem);
                    break;
                case BinaryOperator.BitwiseAnd: _ctx.IL.Emit(OpCodes.And); break;
                case BinaryOperator.BitwiseOr: _ctx.IL.Emit(OpCodes.Or); break;
                case BinaryOperator.BitwiseXor: _ctx.IL.Emit(OpCodes.Xor); break;
                case BinaryOperator.ShiftLeft: _ctx.IL.Emit(OpCodes.Shl); break;
                case BinaryOperator.ShiftRight:
                    _ctx.IL.Emit(isUnsigned ? OpCodes.Shr_Un : OpCodes.Shr);
                    break;
                case BinaryOperator.AndNot:
                    _ctx.IL.Emit(OpCodes.Not);
                    _ctx.IL.Emit(OpCodes.And);
                    break;
                case BinaryOperator.Equal: _ctx.IL.Emit(OpCodes.Ceq); break;
                case BinaryOperator.NotEqual:
                    _ctx.IL.Emit(OpCodes.Ceq);
                    _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                    _ctx.IL.Emit(OpCodes.Ceq);
                    break;
                case BinaryOperator.Less:
                    _ctx.IL.Emit(isUnsigned ? OpCodes.Clt_Un : OpCodes.Clt);
                    break;
                case BinaryOperator.Greater:
                    _ctx.IL.Emit(isUnsigned ? OpCodes.Cgt_Un : OpCodes.Cgt);
                    break;
                case BinaryOperator.LessOrEqual:
                    _ctx.IL.Emit(isUnsigned ? OpCodes.Cgt_Un : OpCodes.Cgt);
                    _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                    _ctx.IL.Emit(OpCodes.Ceq);
                    break;
                case BinaryOperator.GreaterOrEqual:
                    _ctx.IL.Emit(isUnsigned ? OpCodes.Clt_Un : OpCodes.Clt);
                    _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                    _ctx.IL.Emit(OpCodes.Ceq);
                    break;
                default:
                    throw new NotSupportedException($"Binary operator not supported: {bin.Operator}");
            }
        }

        private void EmitStructEquality(BinaryExpression bin, StructTypeSymbol structType)
        {
            var clrType = _ctx.Mapper.Map(structType);
            var leftLocal = _ctx.IL.DeclareLocal(clrType);
            var rightLocal = _ctx.IL.DeclareLocal(clrType);

            EmitExpression(bin.Left);
            _ctx.IL.Emit(OpCodes.Stloc, leftLocal);
            EmitExpression(bin.Right);
            _ctx.IL.Emit(OpCodes.Stloc, rightLocal);

            if (structType.Fields.Count == 0)
            {
                // Empty structs are always equal
                _ctx.IL.Emit(bin.Operator == BinaryOperator.Equal ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                return;
            }

            for (int i = 0; i < structType.Fields.Count; i++)
            {
                var field = structType.Fields[i];
                if (!_ctx.StructFields.TryGetValue(field, out var fb))
                    continue;

                _ctx.IL.Emit(OpCodes.Ldloca, leftLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, fb);
                _ctx.IL.Emit(OpCodes.Ldloca, rightLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, fb);

                if (field.Type.TypeKind == TypeKind.String || field.Type.TypeKind == TypeKind.UntypedString)
                {
                    var equals = typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) })!;
                    _ctx.IL.Emit(OpCodes.Call, equals);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ceq);
                }

                if (i > 0)
                {
                    _ctx.IL.Emit(OpCodes.And);
                }
            }

            if (bin.Operator == BinaryOperator.NotEqual)
            {
                _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                _ctx.IL.Emit(OpCodes.Ceq);
            }
        }

        private void EmitArrayEquality(BinaryExpression bin, ArrayTypeSymbol arrayType)
        {
            var clrType = _ctx.Mapper.Map(arrayType);
            var leftLocal = _ctx.IL.DeclareLocal(clrType);
            var rightLocal = _ctx.IL.DeclareLocal(clrType);

            EmitExpression(bin.Left);
            _ctx.IL.Emit(OpCodes.Stloc, leftLocal);
            EmitExpression(bin.Right);
            _ctx.IL.Emit(OpCodes.Stloc, rightLocal);

            if (arrayType.Length == 0)
            {
                _ctx.IL.Emit(bin.Operator == BinaryOperator.Equal ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                return;
            }

            var elemType = _ctx.Mapper.Map(arrayType.ElementType);
            for (int i = 0; i < arrayType.Length; i++)
            {
                _ctx.IL.Emit(OpCodes.Ldloc, leftLocal);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _ctx.IL.Emit(OpCodes.Ldelem, elemType);
                _ctx.IL.Emit(OpCodes.Ldloc, rightLocal);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                _ctx.IL.Emit(OpCodes.Ldelem, elemType);

                if (arrayType.ElementType.TypeKind == TypeKind.String || arrayType.ElementType.TypeKind == TypeKind.UntypedString)
                {
                    var equals = typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) })!;
                    _ctx.IL.Emit(OpCodes.Call, equals);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ceq);
                }

                if (i > 0)
                {
                    _ctx.IL.Emit(OpCodes.And);
                }
            }

            if (bin.Operator == BinaryOperator.NotEqual)
            {
                _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                _ctx.IL.Emit(OpCodes.Ceq);
            }
        }

        private void EmitUnary(UnaryExpression unary)
        {
            EmitExpression(unary.Operand);

            switch (unary.Operator)
            {
                case UnaryOperator.Negate:
                    if (TypeChecker.IsComplex(unary.Operand.Type))
                    {
                        var complexType = typeof(System.Numerics.Complex);
                        _ctx.IL.Emit(OpCodes.Call, complexType.GetMethod("op_UnaryNegation",
                            new[] { complexType })!);
                    }
                    else
                    {
                        _ctx.IL.Emit(OpCodes.Neg);
                    }
                    break;
                case UnaryOperator.Plus:
                    // No-op
                    break;
                case UnaryOperator.BitwiseNot:
                    _ctx.IL.Emit(OpCodes.Not);
                    break;
                case UnaryOperator.LogicalNot:
                    _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                    _ctx.IL.Emit(OpCodes.Ceq);
                    break;
                default:
                    throw new NotSupportedException($"Unary operator not supported: {unary.Operator}");
            }
        }

        private void EmitCall(CallExpression call)
        {
            // Check if this is a known function we've emitted
            if (_ctx.Methods.TryGetValue(call.Function, out var method))
            {
                // Generic function call: instantiate with concrete type args
                if (call.TypeArguments != null && call.TypeArguments.Count > 0)
                {
                    var typeArgs = new Type[call.TypeArguments.Count];
                    for (int i = 0; i < call.TypeArguments.Count; i++)
                    {
                        typeArgs[i] = _ctx.Mapper.Map(call.TypeArguments[i]);
                    }

                    var instantiated = method.MakeGenericMethod(typeArgs);
                    EmitCallArguments(call);
                    _ctx.IL.Emit(OpCodes.Call, instantiated);
                    return;
                }

                if (call.Function.IsVariadic)
                {
                    EmitVariadicCall(call, method);
                }
                else
                {
                    EmitCallArguments(call);
                    _ctx.IL.Emit(OpCodes.Call, method);
                }

                return;
            }

            // Handle builtins by name
            if (_builtins.EmitBuiltinCall(call))
                return;

            // Indirect call: function variable (delegate)
            if (call.CallTarget != null)
            {
                EmitExpression(call.CallTarget);
                foreach (var arg in call.Arguments)
                    EmitExpression(arg);

                var delegateType = _ctx.Mapper.Map(call.CallTarget.Type);
                var invokeMethod = delegateType.GetMethod("Invoke")!;
                _ctx.IL.Emit(OpCodes.Callvirt, invokeMethod);
                return;
            }

            throw new NotSupportedException($"Cannot resolve function: {call.Function.Name}");
        }

        private void EmitVariadicCall(CallExpression call, MethodInfo method)
        {
            // Variadic function: last CLR parameter is Slice<T>.
            // Pack extra arguments past the required params into a Slice<T>.
            int requiredCount = call.Function.Parameters.Count - 1;

            // Emit required (non-variadic) arguments
            for (int i = 0; i < requiredCount; i++)
            {
                EmitExpression(call.Arguments[i]);
            }

            // Build the variadic slice
            var lastParam = call.Function.Parameters[call.Function.Parameters.Count - 1];
            var sliceSymbolType = (SliceTypeSymbol)lastParam.Type;
            var elemClrType = _ctx.Mapper.Map(sliceSymbolType.ElementType);
            var sliceClrType = _ctx.Mapper.Map(sliceSymbolType);
            int variadicCount = call.Arguments.Count - requiredCount;

            // Create T[] array
            _ctx.IL.Emit(OpCodes.Ldc_I4, variadicCount);
            _ctx.IL.Emit(OpCodes.Newarr, elemClrType);

            // Store each variadic arg into the array
            for (int i = 0; i < variadicCount; i++)
            {
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                EmitExpression(call.Arguments[requiredCount + i]);

                if (elemClrType.IsValueType)
                {
                    _ctx.IL.Emit(OpCodes.Stelem, elemClrType);
                }
                else
                {
                    // Reference type element (e.g. interface{} → object)
                    var argType = call.Arguments[requiredCount + i].Type;
                    var argClrType = _ctx.Mapper.Map(argType);
                    if (argClrType.IsValueType)
                    {
                        _ctx.IL.Emit(OpCodes.Box, argClrType);
                    }

                    _ctx.IL.Emit(OpCodes.Stelem_Ref);
                }
            }

            // new Slice<T>(T[] array)
            var ctor = sliceClrType.GetConstructor(new[] { elemClrType.MakeArrayType() })!;
            _ctx.IL.Emit(OpCodes.Newobj, ctor);

            _ctx.IL.Emit(OpCodes.Call, method);
        }

        private void EmitConversion(ConversionExpression conv)
        {
            var targetKind = conv.Type.TypeKind;
            var sourceKind = conv.Operand.Type.TypeKind;

            // int/rune/byte → string: create string from character code
            if (targetKind == TypeKind.String
                && (sourceKind == TypeKind.Int || sourceKind == TypeKind.UntypedInt
                    || sourceKind == TypeKind.Uint8 || sourceKind == TypeKind.Int32))
            {
                EmitExpression(conv.Operand);
                _ctx.IL.Emit(OpCodes.Conv_U2); // convert to char (uint16)
                _ctx.IL.Emit(OpCodes.Ldc_I4_1);
                _ctx.IL.Emit(OpCodes.Newobj, typeof(string).GetConstructor(new[] { typeof(char), typeof(int) })!);
                return;
            }

            // string → []byte
            if (conv.Type is SliceTypeSymbol sliceType
                && sliceType.ElementType.TypeKind == TypeKind.Uint8
                && (sourceKind == TypeKind.String || sourceKind == TypeKind.UntypedString))
            {
                EmitExpression(conv.Operand);
                _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("ToBytes")!);
                return;
            }

            // []byte → string
            if (targetKind == TypeKind.String
                && conv.Operand.Type is SliceTypeSymbol srcSlice
                && srcSlice.ElementType.TypeKind == TypeKind.Uint8)
            {
                EmitExpression(conv.Operand);
                _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("FromBytes")!);
                return;
            }

            // Slice → Array conversion (Go 1.20+)
            if (conv.Type is ArrayTypeSymbol arrType
                && conv.Operand.Type is SliceTypeSymbol)
            {
                EmitExpression(conv.Operand);
                var elemType = _ctx.Mapper.Map(arrType.ElementType);
                var sliceClrType = _ctx.Mapper.Map(conv.Operand.Type);
                var arrayClrType = _ctx.Mapper.Map(arrType);

                // Store slice in local
                var sliceLocal = _ctx.IL.DeclareLocal(sliceClrType);
                _ctx.IL.Emit(OpCodes.Stloc, sliceLocal);

                // Create target array
                _ctx.IL.Emit(OpCodes.Ldc_I4, arrType.Length);
                _ctx.IL.Emit(OpCodes.Newarr, elemType);
                var arrLocal = _ctx.IL.DeclareLocal(arrayClrType);
                _ctx.IL.Emit(OpCodes.Stloc, arrLocal);

                // Copy elements: for i := 0; i < len; i++ { arr[i] = slice[i] }
                var indexer = sliceClrType.GetProperty("Item")!.GetGetMethod()!;
                for (int i = 0; i < arrType.Length; i++)
                {
                    _ctx.IL.Emit(OpCodes.Ldloc, arrLocal);
                    _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                    _ctx.IL.Emit(OpCodes.Ldloca, sliceLocal);
                    _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                    _ctx.IL.Emit(OpCodes.Call, indexer);
                    // indexer returns ref T, load the value
                    _ctx.IL.Emit(OpCodes.Ldobj, elemType);
                    _ctx.IL.Emit(OpCodes.Stelem, elemType);
                }

                _ctx.IL.Emit(OpCodes.Ldloc, arrLocal);
                return;
            }

            EmitExpression(conv.Operand);

            var targetType = _ctx.Mapper.Map(conv.Type);
            var sourceType = _ctx.Mapper.Map(conv.Operand.Type);

            if (targetType == sourceType)
                return;

            EmitConvOpcode(conv.Type.TypeKind);
        }

        private void EmitSelector(SelectorExpression sel)
        {
            if (_ctx.StructFields.TryGetValue(sel.Field, out var fb))
            {
                EmitAddressForStore(sel.Target);
                _ctx.IL.Emit(OpCodes.Ldfld, fb);
            }
        }

        private void EmitMethodCall(MethodCallExpression call)
        {
            if (_ctx.Methods.TryGetValue(call.Method, out var method))
            {
                // Pointer-receiver method on value: need writeback after call
                if (call.Method.IsPointerReceiver
                    && call.Receiver is AddressOfExpression addrOf
                    && addrOf.Operand is IdentifierExpression receiverId)
                {
                    var innerType = _ctx.Mapper.Map(addrOf.Operand.Type);
                    var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                    var ctor = ptrType.GetConstructor(new[] { innerType })!;
                    var valueField = ptrType.GetField("Value")!;

                    // Create Ptr<T> from local value
                    EmitExpression(addrOf.Operand);
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                    var ptrLocal = _ctx.IL.DeclareLocal(ptrType);
                    _ctx.IL.Emit(OpCodes.Stloc, ptrLocal);

                    // Call method with Ptr as receiver
                    _ctx.IL.Emit(OpCodes.Ldloc, ptrLocal);
                    foreach (var arg in call.Arguments)
                        EmitExpression(arg);
                    _ctx.IL.Emit(OpCodes.Call, method);

                    // Copy modified value back to local
                    _ctx.IL.Emit(OpCodes.Ldloc, ptrLocal);
                    _ctx.IL.Emit(OpCodes.Ldfld, valueField);
                    EmitStore(receiverId.Symbol);
                    return;
                }

                // Static method with receiver as first arg
                EmitExpression(call.Receiver);
                foreach (var arg in call.Arguments)
                {
                    EmitExpression(arg);
                }

                _ctx.IL.Emit(OpCodes.Call, method);
                return;
            }

            // Interface method call: receiver is a wrapper, use callvirt
            if (call.Receiver.Type is InterfaceTypeSymbol ifaceType)
            {
                EmitExpression(call.Receiver);
                foreach (var arg in call.Arguments)
                    EmitExpression(arg);

                var ifaceClrType = _ctx.Mapper.Map(ifaceType);
                var paramTypes = new Type[call.Method.Parameters.Count];
                for (int i = 0; i < call.Method.Parameters.Count; i++)
                    paramTypes[i] = _ctx.Mapper.Map(call.Method.Parameters[i].Type);

                var ifaceMethod = ifaceClrType.GetMethod(call.Method.Name, paramTypes);
                if (ifaceMethod != null)
                {
                    _ctx.IL.Emit(OpCodes.Callvirt, ifaceMethod);
                    return;
                }
            }

            // Runtime type method call (e.g. sync.WaitGroup.Add): instance callvirt
            {
                var receiverClrType = _ctx.Mapper.Map(call.Receiver.Type);
                if (!receiverClrType.IsValueType)
                {
                    EmitExpression(call.Receiver);
                    var argTypes = new Type[call.Arguments.Count];
                    for (int i = 0; i < call.Arguments.Count; i++)
                    {
                        EmitExpression(call.Arguments[i]);
                        argTypes[i] = _ctx.Mapper.Map(call.Method.Parameters[i].Type);
                        // Box value types when target param is object
                        if (argTypes[i] == typeof(object))
                        {
                            var argClrType = _ctx.Mapper.Map(call.Arguments[i].Type);
                            if (argClrType.IsValueType)
                                _ctx.IL.Emit(OpCodes.Box, argClrType);
                        }
                    }

                    var clrMethod = receiverClrType.GetMethod(call.Method.Name, argTypes);
                    if (clrMethod != null)
                    {
                        _ctx.IL.Emit(OpCodes.Callvirt, clrMethod);
                        return;
                    }
                }
            }

            throw new NotSupportedException($"Cannot resolve method: {call.Method.Name}");
        }

        private void EmitCompositeLiteral(CompositeLiteralExpression lit)
        {
            if (lit.Type is StructTypeSymbol structType)
            {
                EmitStructLiteral(lit, structType);
            }
            else if (lit.Type is SliceTypeSymbol sliceType)
            {
                EmitSliceLiteral(lit, sliceType);
            }
            else if (lit.Type is ArrayTypeSymbol arrayType)
            {
                EmitArrayLiteral(lit, arrayType);
            }
            else if (lit.Type is MapTypeSymbol mapType)
            {
                EmitMapLiteral(lit, mapType);
            }
            else
            {
                throw new NotSupportedException($"Composite literal not supported for: {lit.Type.TypeKind}");
            }
        }

        private void EmitStructLiteral(CompositeLiteralExpression lit, StructTypeSymbol structType)
        {
            var clrType = _ctx.Mapper.Map(structType);
            var local = _ctx.IL.DeclareLocal(clrType);
            _ctx.IL.Emit(OpCodes.Ldloca, local);
            _ctx.IL.Emit(OpCodes.Initobj, clrType);

            if (lit.Initializers != null)
            {
                foreach (var init in lit.Initializers)
                {
                    if (_ctx.StructFields.TryGetValue(init.Field, out var fb))
                    {
                        _ctx.IL.Emit(OpCodes.Ldloca, local);
                        EmitExpression(init.Value);
                        _ctx.IL.Emit(OpCodes.Stfld, fb);
                    }
                }
            }

            _ctx.IL.Emit(OpCodes.Ldloc, local);
        }

        private void EmitSliceLiteral(CompositeLiteralExpression lit, SliceTypeSymbol sliceType)
        {
            var elemClrType = _ctx.Mapper.Map(sliceType.ElementType);
            var elements = lit.Elements;

            // Compute array size from max key + 1
            int arraySize = 0;
            if (elements != null)
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    if (elements[i].Key is LiteralExpression keyLit && keyLit.Value is int idx)
                    {
                        if (idx + 1 > arraySize) arraySize = idx + 1;
                    }
                    else
                    {
                        // Fallback: sequential
                        if (i + 1 > arraySize) arraySize = i + 1;
                    }
                }
            }

            // Create T[] array
            _ctx.IL.Emit(OpCodes.Ldc_I4, arraySize);
            _ctx.IL.Emit(OpCodes.Newarr, elemClrType);

            if (elements != null)
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    _ctx.IL.Emit(OpCodes.Dup);
                    if (elements[i].Key is LiteralExpression keyLit && keyLit.Value is int idx)
                    {
                        _ctx.IL.Emit(OpCodes.Ldc_I4, idx);
                    }
                    else
                    {
                        _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                    }
                    EmitExpression(elements[i].Value);
                    EmitStelem(elemClrType);
                }
            }

            // new Slice<T>(array)
            var sliceClrType = _ctx.Mapper.Map(sliceType);
            var ctor = sliceClrType.GetConstructor(new[] { elemClrType.MakeArrayType() })!;
            _ctx.IL.Emit(OpCodes.Newobj, ctor);
        }

        private void EmitArrayLiteral(CompositeLiteralExpression lit, ArrayTypeSymbol arrayType)
        {
            var elemClrType = _ctx.Mapper.Map(arrayType.ElementType);
            var elements = lit.Elements;
            var count = arrayType.Length;

            _ctx.IL.Emit(OpCodes.Ldc_I4, count);
            _ctx.IL.Emit(OpCodes.Newarr, elemClrType);

            if (elements != null)
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    _ctx.IL.Emit(OpCodes.Dup);
                    if (elements[i].Key is LiteralExpression keyLit && keyLit.Value is int idx)
                    {
                        _ctx.IL.Emit(OpCodes.Ldc_I4, idx);
                    }
                    else
                    {
                        _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                    }
                    EmitExpression(elements[i].Value);
                    EmitStelem(elemClrType);
                }
            }
        }

        private void EmitMapLiteral(CompositeLiteralExpression lit, MapTypeSymbol mapType)
        {
            var mapClrType = _ctx.Mapper.Map(mapType);
            var ctor = mapClrType.GetConstructor(Type.EmptyTypes)!;
            _ctx.IL.Emit(OpCodes.Newobj, ctor);

            if (lit.Elements != null)
            {
                foreach (var elem in lit.Elements)
                {
                    _ctx.IL.Emit(OpCodes.Dup);
                    EmitExpression(elem.Key!);
                    EmitExpression(elem.Value);
                    var setter = mapClrType.GetProperty("Item")!.GetSetMethod()!;
                    _ctx.IL.Emit(OpCodes.Call, setter);
                }
            }
        }

        private void EmitAddressOf(AddressOfExpression addrOf)
        {
            // &x → new Ptr<T>(x)
            var innerType = _ctx.Mapper.Map(addrOf.Operand.Type);
            var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
            var ctor = ptrType.GetConstructor(new[] { innerType })!;

            EmitExpression(addrOf.Operand);
            _ctx.IL.Emit(OpCodes.Newobj, ctor);
        }

        private void EmitDeref(DerefExpression deref)
        {
            // *p → p.Value
            EmitExpression(deref.Operand);
            var innerType = _ctx.Mapper.Map(deref.Type);
            // Reference types don't use Ptr<T> — pointer is the reference itself
            if (!innerType.IsValueType)
                return;
            var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
            var valueField = ptrType.GetField("Value")!;
            _ctx.IL.Emit(OpCodes.Ldfld, valueField);
        }

        private void EmitIndex(IndexExpression idx)
        {
            var targetType = idx.Target.Type;

            if (targetType is SliceTypeSymbol)
            {
                var sliceClrType = _ctx.Mapper.Map(targetType);
                EmitExpressionAddress(idx.Target, sliceClrType);
                EmitExpression(idx.Index);
                var indexer = sliceClrType.GetProperty("Item")!;
                _ctx.IL.Emit(OpCodes.Call, indexer.GetGetMethod()!);
                // indexer returns ref T, need to load the value
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                if (indexer.PropertyType.IsByRef)
                {
                    _ctx.IL.Emit(OpCodes.Ldobj, elemClrType);
                }
            }
            else if (targetType is ArrayTypeSymbol)
            {
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                _ctx.IL.Emit(OpCodes.Conv_I4);
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                EmitLdelem(elemClrType);
            }
            else if (targetType is MapTypeSymbol)
            {
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                var mapClrType = _ctx.Mapper.Map(targetType);
                if (idx.IsCommaOk)
                {
                    // val, ok := m[key] → Map.Get(key) returns (V, bool) tuple
                    var getMethod = mapClrType.GetMethod("Get")!;
                    _ctx.IL.Emit(OpCodes.Call, getMethod);
                }
                else
                {
                    var getter = mapClrType.GetProperty("Item")!.GetGetMethod()!;
                    _ctx.IL.Emit(OpCodes.Call, getter);
                }
            }
            else if (targetType.TypeKind == TypeKind.String || targetType.TypeKind == TypeKind.UntypedString)
            {
                // String byte indexing via GoString.ByteAt
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                EmitLdconv(null);
                var byteAt = typeof(GoString).GetMethod("ByteAt")!;
                _ctx.IL.Emit(OpCodes.Call, byteAt);
            }
            else
            {
                throw new NotSupportedException($"Index on type {targetType.TypeKind} not supported");
            }
        }

        private void EmitSlice(SliceExpression slice)
        {
            var targetType = slice.Operand.Type;

            if (targetType is SliceTypeSymbol sliceType)
            {
                var sliceClrType = _ctx.Mapper.Map(targetType);
                EmitExpressionAddress(slice.Operand, sliceClrType);

                EmitExpression(slice.Low ?? MakeIntLiteral(0));
                EmitExpression(slice.High ?? MakeIntLiteral(-1)); // sentinel for len

                if (slice.Max != null)
                {
                    EmitExpression(slice.Max);
                    var reslice3 = sliceClrType.GetMethod("Reslice", new[] { typeof(int), typeof(int), typeof(int) })!;
                    _ctx.IL.Emit(OpCodes.Call, reslice3);
                }
                else
                {
                    var reslice2 = sliceClrType.GetMethod("Reslice", new[] { typeof(int), typeof(int) })!;
                    _ctx.IL.Emit(OpCodes.Call, reslice2);
                }
            }
            else if (targetType.TypeKind == TypeKind.String || targetType.TypeKind == TypeKind.UntypedString)
            {
                EmitExpression(slice.Operand);
                EmitExpression(slice.Low ?? MakeIntLiteral(0));
                EmitExpression(slice.High ?? MakeIntLiteral(-1));
                var sliceStr = typeof(GoString).GetMethod("SliceString")!;
                _ctx.IL.Emit(OpCodes.Call, sliceStr);
            }
            else
            {
                throw new NotSupportedException($"Slice on type {targetType.TypeKind} not supported");
            }
        }

        private void EmitTypeAssert(TypeAssertExpression typeAssert)
        {
            EmitExpression(typeAssert.Expression);

            var targetType = _ctx.Mapper.Map(typeAssert.AssertedType);

            if (typeAssert.IsCommaOk)
            {
                // val, ok := x.(T) → isinst check, return (value, bool) tuple
                var tupleType = typeof(ValueTuple<,>).MakeGenericType(targetType, typeof(bool));
                var tempObj = _ctx.IL.DeclareLocal(typeof(object));
                _ctx.IL.Emit(OpCodes.Stloc, tempObj);

                var okLabel = _ctx.IL.DefineLabel();
                var endLabel = _ctx.IL.DefineLabel();

                _ctx.IL.Emit(OpCodes.Ldloc, tempObj);
                _ctx.IL.Emit(OpCodes.Isinst, targetType.IsValueType ? typeof(object) : targetType);
                if (targetType.IsValueType)
                {
                    // For value types, isinst against the boxed type
                    // We need to check if it's the right boxed type
                    _ctx.IL.Emit(OpCodes.Pop);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempObj);
                    _ctx.IL.Emit(OpCodes.Isinst, targetType);
                }
                _ctx.IL.Emit(OpCodes.Brtrue, okLabel);

                // Failure: default value, false
                var resultLocal = _ctx.IL.DeclareLocal(tupleType);
                _ctx.IL.Emit(OpCodes.Ldloca, resultLocal);
                _ctx.IL.Emit(OpCodes.Initobj, tupleType);
                _ctx.IL.Emit(OpCodes.Ldloc, resultLocal);
                _ctx.IL.Emit(OpCodes.Br, endLabel);

                // Success: unboxed/cast value, true
                _ctx.IL.MarkLabel(okLabel);
                _ctx.IL.Emit(OpCodes.Ldloc, tempObj);
                if (targetType.IsValueType)
                    _ctx.IL.Emit(OpCodes.Unbox_Any, targetType);
                else
                    _ctx.IL.Emit(OpCodes.Castclass, targetType);
                _ctx.IL.Emit(OpCodes.Ldc_I4_1);
                var ctor = tupleType.GetConstructor(new[] { targetType, typeof(bool) })!;
                _ctx.IL.Emit(OpCodes.Newobj, ctor);

                _ctx.IL.MarkLabel(endLabel);
            }
            else
            {
                if (targetType.IsValueType)
                {
                    _ctx.IL.Emit(OpCodes.Unbox_Any, targetType);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Castclass, targetType);
                }
            }
        }

        private void EmitIndexAssignment(IndexExpression idx, Expression value)
        {
            var targetType = idx.Target.Type;

            if (targetType is SliceTypeSymbol)
            {
                var sliceClrType = _ctx.Mapper.Map(targetType);
                EmitExpressionAddress(idx.Target, sliceClrType);
                EmitExpression(idx.Index);
                var indexer = sliceClrType.GetProperty("Item")!;
                _ctx.IL.Emit(OpCodes.Call, indexer.GetGetMethod()!);
                // Get ref, then store into it
                EmitExpression(value);
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                _ctx.IL.Emit(OpCodes.Stobj, elemClrType);
            }
            else if (targetType is ArrayTypeSymbol)
            {
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                _ctx.IL.Emit(OpCodes.Conv_I4);
                EmitExpression(value);
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                EmitStelem(elemClrType);
            }
            else if (targetType is MapTypeSymbol)
            {
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                EmitExpression(value);
                var mapClrType = _ctx.Mapper.Map(targetType);
                var setter = mapClrType.GetProperty("Item")!.GetSetMethod()!;
                _ctx.IL.Emit(OpCodes.Call, setter);
            }
        }

        // --- Defer, Go, Channels ---

        internal void EmitDeferWrappedBody(BlockStatement body, bool isVoid, Type? returnType = null,
            IReadOnlyList<LocalSymbol>? namedReturns = null)
            => _deferGo.EmitDeferWrappedBody(body, isVoid, returnType, namedReturns);

        internal static bool ContainsDefer(BlockStatement block)
            => DeferGoEmitter.ContainsDefer(block);

        // --- Helpers ---

        private void EmitCallArguments(CallExpression call)
        {
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                EmitExpression(call.Arguments[i]);

                // Check if argument needs interface wrapping
                if (i < call.Function.Parameters.Count)
                {
                    var paramType = call.Function.Parameters[i].Type;
                    var argType = call.Arguments[i].Type;
                    if (paramType is InterfaceTypeSymbol && argType.TypeKind != TypeKind.Interface)
                    {
                        var targetClrType = _ctx.Mapper.Map(paramType);
                        EmitInterfaceWrapIfNeeded(argType, paramType, targetClrType);
                    }
                }
            }
        }

        private void EmitInterfaceWrapIfNeeded(TypeSymbol sourceType, TypeSymbol targetType, Type targetClrType)
        {
            // Named interface with methods: generate wrapper
            if (targetType is InterfaceTypeSymbol ifaceType && ifaceType.Methods.Count > 0
                && sourceType.TypeKind != TypeKind.Interface)
            {
                _ctx.DeclEmitter!.GenerateWrapper(sourceType, ifaceType);
                var (_, ctor) = _ctx.WrapperTypes[(sourceType, ifaceType)];
                _ctx.IL.Emit(OpCodes.Newobj, ctor);
                return;
            }

            // Empty interface or other ref-type target: box value types
            var sourceClrType = _ctx.Mapper.Map(sourceType);
            if (sourceClrType.IsValueType && !targetClrType.IsValueType)
                _ctx.IL.Emit(OpCodes.Box, sourceClrType);
        }

        internal void EmitLoad(Symbol symbol)
        {
            if (_ctx.Locals.TryGetValue(symbol, out var local))
            {
                _ctx.IL.Emit(OpCodes.Ldloc, local);
                if (_ctx.CapturedSymbols.Contains(symbol))
                    _ctx.IL.Emit(OpCodes.Ldfld, local.LocalType.GetField("Value")!);
                return;
            }

            if (_ctx.Parameters.TryGetValue(symbol, out var paramIndex))
            {
                _ctx.IL.Emit(OpCodes.Ldarg, paramIndex);
                return;
            }

            if (_ctx.PackageFields.TryGetValue(symbol, out var field))
            {
                _ctx.IL.Emit(OpCodes.Ldsfld, field);
                return;
            }

            if (symbol is ConstantSymbol constant)
            {
                EmitConstantValue(constant);
                return;
            }

            if (symbol is PackageVarSymbol pkgVar)
            {
                var prop = pkgVar.RuntimeType.GetProperty(pkgVar.RuntimeMember);
                if (prop != null)
                {
                    _ctx.IL.Emit(OpCodes.Call, prop.GetGetMethod()!);
                    return;
                }

                var runtimeField = pkgVar.RuntimeType.GetField(pkgVar.RuntimeMember);
                if (runtimeField != null)
                {
                    _ctx.IL.Emit(OpCodes.Ldsfld, runtimeField);
                    return;
                }

                throw new NotSupportedException(
                    $"Package variable '{pkgVar.Name}' — runtime member '{pkgVar.RuntimeMember}' not found on {pkgVar.RuntimeType.Name}");
            }

            if (symbol is FunctionSymbol funcSym && _ctx.Methods.TryGetValue(funcSym, out var funcMethod))
            {
                var paramTypes = new List<TypeSymbol>();
                foreach (var p in funcSym.Parameters)
                    paramTypes.Add(p.Type);
                var funcType = new FunctionTypeSymbol(paramTypes, funcSym.ReturnTypes);
                var delegateType = _ctx.Mapper.Map(funcType);
                var delegateCtor = delegateType.GetConstructor(new[] { typeof(object), typeof(IntPtr) })!;
                _ctx.IL.Emit(OpCodes.Ldnull);
                _ctx.IL.Emit(OpCodes.Ldftn, funcMethod);
                _ctx.IL.Emit(OpCodes.Newobj, delegateCtor);
                return;
            }

            throw new NotSupportedException($"Cannot load symbol: {symbol.Name} ({symbol.Kind})");
        }

        private void EmitStore(Symbol symbol)
        {
            if (_ctx.Locals.TryGetValue(symbol, out var local))
            {
                if (_ctx.CapturedSymbols.Contains(symbol))
                {
                    var valueField = local.LocalType.GetField("Value")!;
                    var temp = _ctx.IL.DeclareLocal(valueField.FieldType);
                    _ctx.IL.Emit(OpCodes.Stloc, temp);
                    _ctx.IL.Emit(OpCodes.Ldloc, local);
                    _ctx.IL.Emit(OpCodes.Ldloc, temp);
                    _ctx.IL.Emit(OpCodes.Stfld, valueField);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                }

                return;
            }

            if (_ctx.Parameters.TryGetValue(symbol, out var paramIndex))
            {
                _ctx.IL.Emit(OpCodes.Starg, paramIndex);
                return;
            }

            if (_ctx.PackageFields.TryGetValue(symbol, out var field))
            {
                _ctx.IL.Emit(OpCodes.Stsfld, field);
                return;
            }

            throw new NotSupportedException($"Cannot store to symbol: {symbol.Name} ({symbol.Kind})");
        }

        private void EmitConstantValue(ConstantSymbol constant)
        {
            if (constant.Value == null)
            {
                _ctx.IL.Emit(OpCodes.Ldnull);
                return;
            }

            switch (constant.Type.TypeKind)
            {
                case TypeKind.Bool:
                case TypeKind.UntypedBool:
                    _ctx.IL.Emit((bool)constant.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                    break;
                case TypeKind.Int:
                case TypeKind.Int64:
                case TypeKind.UntypedInt:
                    _ctx.IL.Emit(OpCodes.Ldc_I8, Convert.ToInt64(constant.Value));
                    break;
                case TypeKind.Int32:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, Convert.ToInt32(constant.Value));
                    break;
                case TypeKind.String:
                case TypeKind.UntypedString:
                    _ctx.IL.Emit(OpCodes.Ldstr, (string)constant.Value);
                    break;
                case TypeKind.Float64:
                case TypeKind.UntypedFloat:
                    _ctx.IL.Emit(OpCodes.Ldc_R8, Convert.ToDouble(constant.Value));
                    break;
                default:
                    throw new NotSupportedException($"Constant type not supported: {constant.Type.TypeKind}");
            }
        }

        private void EmitIntConstant(int value, TypeSymbol type)
        {
            switch (type.TypeKind)
            {
                case TypeKind.Int:
                case TypeKind.Int64:
                case TypeKind.UntypedInt:
                    _ctx.IL.Emit(OpCodes.Ldc_I8, (long)value);
                    break;
                case TypeKind.Int32:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, value);
                    break;
                default:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, value);
                    break;
            }
        }

        internal void EmitBoxIfNeeded(TypeSymbol type)
        {
            var clrType = _ctx.Mapper.Map(type);
            if (clrType.IsValueType)
            {
                _ctx.IL.Emit(OpCodes.Box, clrType);
            }
        }

        private void EmitConvOpcode(TypeKind target)
        {
            switch (target)
            {
                case TypeKind.Int8: _ctx.IL.Emit(OpCodes.Conv_I1); break;
                case TypeKind.Int16: _ctx.IL.Emit(OpCodes.Conv_I2); break;
                case TypeKind.Int32: _ctx.IL.Emit(OpCodes.Conv_I4); break;
                case TypeKind.Int:
                case TypeKind.Int64: _ctx.IL.Emit(OpCodes.Conv_I8); break;
                case TypeKind.Uint8: _ctx.IL.Emit(OpCodes.Conv_U1); break;
                case TypeKind.Uint16: _ctx.IL.Emit(OpCodes.Conv_U2); break;
                case TypeKind.Uint32: _ctx.IL.Emit(OpCodes.Conv_U4); break;
                case TypeKind.Uint:
                case TypeKind.Uint64: _ctx.IL.Emit(OpCodes.Conv_U8); break;
                case TypeKind.Float32: _ctx.IL.Emit(OpCodes.Conv_R4); break;
                case TypeKind.Float64: _ctx.IL.Emit(OpCodes.Conv_R8); break;
            }
        }

        private static bool IsUnsignedType(TypeSymbol type)
        {
            switch (type.TypeKind)
            {
                case TypeKind.Uint:
                case TypeKind.Uint8:
                case TypeKind.Uint16:
                case TypeKind.Uint32:
                case TypeKind.Uint64:
                    return true;
                default:
                    return false;
            }
        }

        private void EmitAddressForStore(Expression target)
        {
            // Load address of struct local/param so we can stfld
            if (target is IdentifierExpression id)
            {
                if (_ctx.Locals.TryGetValue(id.Symbol, out var local))
                {
                    _ctx.IL.Emit(OpCodes.Ldloca, local);
                    return;
                }

                if (_ctx.Parameters.TryGetValue(id.Symbol, out var pIdx))
                {
                    _ctx.IL.Emit(OpCodes.Ldarga, pIdx);
                    return;
                }
            }

            // Deref pointer: *p → address of Ptr<T>.Value (for mutation through pointers)
            if (target is DerefExpression deref)
            {
                EmitExpression(deref.Operand);
                var innerType = _ctx.Mapper.Map(deref.Type);
                var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                var valueField = ptrType.GetField("Value")!;
                _ctx.IL.Emit(OpCodes.Ldflda, valueField);
                return;
            }

            // Fallback: emit as value (works for reference types)
            EmitExpression(target);
        }

        internal void EmitExpressionAddress(Expression expr, Type clrType)
        {
            // For value types, we need to load the address
            if (expr is IdentifierExpression id && clrType.IsValueType)
            {
                if (_ctx.Locals.TryGetValue(id.Symbol, out var local))
                {
                    if (_ctx.CapturedSymbols.Contains(id.Symbol))
                    {
                        // Captured variable: local is Box<T>, get address of .Value field
                        _ctx.IL.Emit(OpCodes.Ldloc, local);
                        _ctx.IL.Emit(OpCodes.Ldflda, local.LocalType.GetField("Value")!);
                    }
                    else
                    {
                        _ctx.IL.Emit(OpCodes.Ldloca, local);
                    }
                    return;
                }

                if (_ctx.Parameters.TryGetValue(id.Symbol, out var pIdx))
                {
                    _ctx.IL.Emit(OpCodes.Ldarga, pIdx);
                    return;
                }
            }

            // For non-local value types, store to temp and load address
            if (clrType.IsValueType)
            {
                EmitExpression(expr);
                var temp = _ctx.IL.DeclareLocal(clrType);
                _ctx.IL.Emit(OpCodes.Stloc, temp);
                _ctx.IL.Emit(OpCodes.Ldloca, temp);
            }
            else
            {
                EmitExpression(expr);
            }
        }

        internal void EmitStelem(Type elemType)
        {
            if (elemType == typeof(int)) _ctx.IL.Emit(OpCodes.Stelem_I4);
            else if (elemType == typeof(long)) _ctx.IL.Emit(OpCodes.Stelem_I8);
            else if (elemType == typeof(float)) _ctx.IL.Emit(OpCodes.Stelem_R4);
            else if (elemType == typeof(double)) _ctx.IL.Emit(OpCodes.Stelem_R8);
            else if (elemType == typeof(byte)) _ctx.IL.Emit(OpCodes.Stelem_I1);
            else if (elemType == typeof(short)) _ctx.IL.Emit(OpCodes.Stelem_I2);
            else if (!elemType.IsValueType) _ctx.IL.Emit(OpCodes.Stelem_Ref);
            else _ctx.IL.Emit(OpCodes.Stelem, elemType);
        }

        private void EmitLdelem(Type elemType)
        {
            if (elemType == typeof(int)) _ctx.IL.Emit(OpCodes.Ldelem_I4);
            else if (elemType == typeof(long)) _ctx.IL.Emit(OpCodes.Ldelem_I8);
            else if (elemType == typeof(float)) _ctx.IL.Emit(OpCodes.Ldelem_R4);
            else if (elemType == typeof(double)) _ctx.IL.Emit(OpCodes.Ldelem_R8);
            else if (elemType == typeof(byte)) _ctx.IL.Emit(OpCodes.Ldelem_U1);
            else if (elemType == typeof(short)) _ctx.IL.Emit(OpCodes.Ldelem_I2);
            else if (!elemType.IsValueType) _ctx.IL.Emit(OpCodes.Ldelem_Ref);
            else _ctx.IL.Emit(OpCodes.Ldelem, elemType);
        }

        private void EmitLdconv(TypeSymbol? type)
        {
            // Convert int64 index to int32 for array/string indexing
            if (type != null && (type.TypeKind == TypeKind.Int || type.TypeKind == TypeKind.UntypedInt
                || type.TypeKind == TypeKind.Int64))
            {
                _ctx.IL.Emit(OpCodes.Conv_I4);
            }
        }

        private static Expression MakeIntLiteral(int value)
        {
            return new LiteralExpression(value, BuiltinTypes.Int,
                new Language.TextSpan(0, 0));
        }

        private void EmitLocalTypeDeclaration(TypeDeclaration typeDecl)
        {
            if (typeDecl.Symbol is StructTypeSymbol structType)
            {
                var typeBuilder = _ctx.Module.DefineType(
                    structType.Name,
                    TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
                    typeof(ValueType));

                foreach (var field in structType.Fields)
                {
                    var fieldType = _ctx.Mapper.Map(field.Type);
                    var fb = typeBuilder.DefineField(field.Name, fieldType, FieldAttributes.Public);
                    _ctx.StructFields[field] = fb;
                }

                var runtimeType = typeBuilder.CreateType()!;
                _ctx.Mapper.Register(structType, runtimeType);
                _ctx.StructTypes[structType] = typeBuilder;
                _ctx.FinalizedTypes.Add(structType);
            }
            else if (typeDecl.Symbol is InterfaceTypeSymbol ifaceType)
            {
                var typeBuilder = _ctx.Module.DefineType(
                    ifaceType.Name,
                    TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);

                foreach (var method in ifaceType.Methods)
                {
                    var paramTypes = new Type[method.Parameters.Count];
                    for (int i = 0; i < method.Parameters.Count; i++)
                        paramTypes[i] = _ctx.Mapper.Map(method.Parameters[i].Type);
                    var returnType = _ctx.Mapper.Map(method.ReturnType);
                    typeBuilder.DefineMethod(method.Name,
                        MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
                        returnType, paramTypes);
                }

                var runtimeIfaceType = typeBuilder.CreateType()!;
                _ctx.Mapper.Register(ifaceType, runtimeIfaceType);
                _ctx.InterfaceTypes[ifaceType] = typeBuilder;
                _ctx.FinalizedTypes.Add(ifaceType);
            }
            // Non-struct/interface types (type aliases, named types) don't need IL emission
        }
    }
}

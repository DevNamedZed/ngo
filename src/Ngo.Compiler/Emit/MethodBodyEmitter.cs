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
using Ngo.Compiler.Archive;
using Ngo.Compiler.Emit.Builder;
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
        private readonly Dictionary<Expression, LocalBuilder> _spreadLocals = new();
        internal readonly ClosureEmitter Closures;
        private readonly ForRangeEmitter _ranges;
        private readonly DeferGoEmitter _deferGo;
        private IReadOnlyList<LocalSymbol>? _namedReturns;
        private string? _pendingLabel;

        internal IReadOnlyList<LocalSymbol>? NamedReturns
        {
            get => _namedReturns;
            set => _namedReturns = value;
        }

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

        /// <summary>
        /// Emit function body into the already-set _ctx.IL (used by NgoWriter capture path).
        /// Caller must set _ctx.IL and call _ctx.ResetMethodState() before calling.
        /// </summary>
        public void EmitFunctionBodyInto(FunctionDeclaration func)
        {
            _currentReturnTypes = func.Symbol.ReturnTypes;
            EmitFunctionBodyCore(func);
        }

        public void EmitFunctionBody(FunctionDeclaration func)
        {
            var method = _ctx.Methods[func.Symbol];
            _ctx.IL = method.GetILWriter();

            if (_ctx.TracedMethodNames != null && _ctx.TracedMethodNames.Contains(func.Symbol.Name))
            {
                var tracer = new Builder.TracingCilWriter(_ctx.IL);
                _ctx.IL = tracer;
                _ctx.ILTraces[func.Symbol.Name] = tracer.Trace;
            }

            _ctx.ResetMethodState();
            _currentReturnTypes = func.Symbol.ReturnTypes;

            // Build the method emit context for generic param resolution
            IReadOnlyList<Symbols.TypeParameterSymbol>? methodTypeParams = null;
            Type[]? methodGenericParams = null;
            if (func.Symbol.TypeParameters.Count > 0)
            {
                methodTypeParams = func.Symbol.TypeParameters;
                methodGenericParams = method.AsMethodInfo().GetGenericArguments();

                var typeParams = func.Symbol.TypeParameters;
                var names = new string[typeParams.Count];
                var symbols = new Symbols.TypeParameterSymbol[typeParams.Count];
                for (int i = 0; i < typeParams.Count; i++)
                {
                    names[i] = typeParams[i].Name;
                    symbols[i] = typeParams[i];
                }
                _ctx.EnclosingGenericParamNames = names;
                _ctx.EnclosingGenericParamSymbols = symbols;
            }
            else
            {
                _ctx.EnclosingGenericParamNames = Array.Empty<string>();
                _ctx.EnclosingGenericParamSymbols = Array.Empty<Symbols.TypeParameterSymbol>();
            }


            // If the function has no body (assembly-only in Go source),
            // try to emit a .NET intrinsic implementation instead of a no-op.
            if (func.Body.Statements.Count == 0)
            {
                // Try by function name + package
                if (RuntimeIntrinsics.TryEmitBody(_ctx, func.Symbol.Name, func.Symbol.PackageName))
                    return;
                // Try by go:linkname target (e.g., runtime_Semacquire → runtime.semacquire)
                if (func.Symbol.LinkName != null &&
                    RuntimeIntrinsics.TryEmitByLinkName(_ctx, func.Symbol.LinkName))
                    return;
            }


            EmitFunctionBodyCore(func);
        }

        private void EmitFunctionBodyCore(FunctionDeclaration func)
        {
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
                    var boxCtor = _ctx.Definitions.GetConstructor(boxType, Type.EmptyTypes);
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

        /// <summary>
        /// Emit method body into the already-set _ctx.IL (used by NgoWriter capture path).
        /// Caller must set _ctx.IL and call _ctx.ResetMethodState() before calling.
        /// </summary>
        public void EmitMethodBodyInto(MethodDeclaration decl)
        {
            _currentReturnTypes = decl.Symbol.ReturnTypes;
            EmitMethodBodyCore(decl);
        }

        public void EmitMethodBody(MethodDeclaration decl)
        {
            var method = _ctx.Methods[decl.Symbol];
            _ctx.IL = method.GetILWriter();

            if (_ctx.TracedMethodNames != null && _ctx.TracedMethodNames.Contains(decl.Symbol.Name))
            {
                var tracer = new Builder.TracingCilWriter(_ctx.IL);
                _ctx.IL = tracer;
                var traceName = decl.Symbol.ReceiverType != null
                    ? $"{decl.Symbol.ReceiverType.Name}.{decl.Symbol.Name}"
                    : decl.Symbol.Name;
                _ctx.ILTraces[traceName] = tracer.Trace;
            }

            _ctx.ResetMethodState();
            _currentReturnTypes = decl.Symbol.ReturnTypes;

            var genericSource = decl.Symbol.TypeParameters.Count > 0
                ? decl.Symbol.TypeParameters
                : (decl.Symbol.ReceiverType as Symbols.StructTypeSymbol)?.TypeParameters;
            if (genericSource != null && genericSource.Count > 0)
            {
                var names = new string[genericSource.Count];
                var symbols = new Symbols.TypeParameterSymbol[genericSource.Count];
                for (int i = 0; i < genericSource.Count; i++)
                {
                    names[i] = genericSource[i].Name;
                    symbols[i] = genericSource[i];
                }
                _ctx.EnclosingGenericParamNames = names;
                _ctx.EnclosingGenericParamSymbols = symbols;
            }
            else
            {
                _ctx.EnclosingGenericParamNames = Array.Empty<string>();
                _ctx.EnclosingGenericParamSymbols = Array.Empty<Symbols.TypeParameterSymbol>();
            }

            // NgoWriter gets its SerializationContext at construction from NgoMethodBuilder.

            if (decl.Body.Statements.Count == 0)
            {
                if (RuntimeIntrinsics.TryEmitBody(_ctx, decl.Symbol.Name, decl.Symbol.ReceiverType?.PackagePath))
                {
                    return;
                }
            }

            EmitMethodBodyCore(decl);
        }

        private void EmitMethodBodyCore(MethodDeclaration decl)
        {
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
                    var boxCtor = _ctx.Definitions.GetConstructor(boxType, Type.EmptyTypes);
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

        /// <summary>
        /// Emit package init body into the already-set _ctx.IL (used by NgoWriter capture path).
        /// Caller must set _ctx.IL and call _ctx.ResetMethodState() before calling.
        /// The static constructor must already be defined.
        /// </summary>
        public void EmitPackageInitInto(IReadOnlyList<VarDeclaration> vars, List<FunctionDeclaration> initFuncs)
        {
            EmitPackageInitCore(vars, initFuncs);
        }

        public void EmitPackageInit(IReadOnlyList<VarDeclaration> vars, List<FunctionDeclaration> initFuncs)
        {
            var cctor = _ctx.PackageType.DefineConstructor(
                MethodAttributes.Static | MethodAttributes.Private,
                System.Reflection.CallingConventions.Standard,
                Type.EmptyTypes);

            _ctx.IL = cctor.GetILWriter();
            _ctx.ResetMethodState();
            EmitPackageInitCore(vars, initFuncs);
        }

        private void EmitPackageInitCore(IReadOnlyList<VarDeclaration> vars, List<FunctionDeclaration> initFuncs)
        {
            // Initialize CGo native library resolver before anything else
            if (_ctx.CgoResolverInitMethod != null)
            {
                _ctx.IL.Emit(OpCodes.Call, _ctx.CgoResolverInitMethod.AsMethodInfo());
                _ctx.CgoResolverInitMethod = null; // Only emit once
            }

            // Initialize package-level variables
            foreach (var v in vars)
            {
                if (!_ctx.PackageFields.TryGetValue(v.Symbol, out var field))
                {
                    continue;
                }

                if (v.Initializer != null)
                {
                    EmitExpression(v.Initializer);
                    _ctx.IL.Emit(OpCodes.Stsfld, field.AsFieldInfo());
                }
                else if (v.Symbol.Type is ArrayTypeSymbol arrayType)
                {
                    var elemClrType = _ctx.Mapper.Map(arrayType.ElementType);
                    _ctx.IL.Emit(OpCodes.Ldc_I4, arrayType.Length);
                    _ctx.IL.Emit(OpCodes.Newarr, elemClrType);
                    _ctx.IL.Emit(OpCodes.Stsfld, field.AsFieldInfo());
                }
                else
                {
                    // Go struct types that map to C# classes need explicit zero-value
                    // initialization (new T()) since class fields default to null,
                    // not to the zero-valued struct that Go expects.
                    var fieldClrType = field.AsFieldInfo().FieldType;
                    if (!fieldClrType.IsValueType && !fieldClrType.IsArray
                        && fieldClrType != typeof(object)
                        && !fieldClrType.IsInterface && !fieldClrType.IsAbstract)
                    {
                        var defaultCtor = fieldClrType.GetConstructor(Type.EmptyTypes);
                        if (defaultCtor != null)
                        {
                            _ctx.IL.Emit(OpCodes.Newobj, defaultCtor);
                            _ctx.IL.Emit(OpCodes.Stsfld, field.AsFieldInfo());
                        }
                    }
                }
            }

            // Call init() functions in declaration order
            foreach (var initFunc in initFuncs)
            {
                if (_ctx.Methods.TryGetValue(initFunc.Symbol, out var mb))
                {
                    _ctx.IL.Emit(OpCodes.Call, mb.AsMethodInfo());
                }
            }

            _ctx.IL.Emit(OpCodes.Ret);
        }

        internal void BoxCapturedParameters(IReadOnlyList<ParameterSymbol> parameters,
            ParameterSymbol? receiver = null)
        {
            // Box the receiver if captured
            if (receiver != null && IsCaptured(receiver))
                BoxOneParameter(receiver);

            // Box regular parameters if captured
            foreach (var param in parameters)
            {
                if (IsCaptured(param))
                    BoxOneParameter(param);
            }
        }

        private bool IsCaptured(Symbol symbol)
        {
            if (_ctx.CapturedSymbols.Contains(symbol))
            {
                return true;
            }
            foreach (var captured in _ctx.CapturedSymbols)
            {
                if (captured.Name == symbol.Name && captured.Kind == symbol.Kind)
                {
                    return true;
                }
            }
            return false;
        }

        internal void BoxOneParameter(Symbol param)
        {
            if (!TryResolveParameterIndex(param, out var paramIndex))
            {
                return;
            }

            TypeSymbol paramType;
            if (param is ParameterSymbol ps)
            {
                paramType = ps.Type;
            }
            else
            {
                return;
            }

            var innerType = _ctx.Mapper.Map(paramType);
            var boxType = typeof(Box<>).MakeGenericType(innerType);
            var boxCtor = _ctx.Definitions.GetConstructor(boxType, new[] { innerType });

            var boxLocal = _ctx.IL.DeclareLocal(boxType);
            _ctx.IL.Emit(OpCodes.Ldarg, paramIndex);
            _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
            _ctx.IL.Emit(OpCodes.Stloc, boxLocal);
            _ctx.Locals[param] = boxLocal;

            // Also register under any matching captured symbols so closure lookup
            // by name finds the boxed local
            foreach (var captured in _ctx.CapturedSymbols)
            {
                if (captured != param && captured.Name == param.Name && captured.Kind == param.Kind)
                {
                    _ctx.Locals[captured] = boxLocal;
                }
            }
        }

        private bool TryResolveParameterIndex(Symbol param, out int index)
        {
            if (_ctx.Parameters.TryGetValue(param, out index))
            {
                return true;
            }
            if (param.Kind == SymbolKind.Parameter || param is ParameterSymbol)
            {
                foreach (var kvp in _ctx.Parameters)
                {
                    if (kvp.Key.Name == param.Name)
                    {
                        index = kvp.Value;
                        return true;
                    }
                }
            }
            index = -1;
            return false;
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
                // Nil literal for value types needs initobj instead of ldnull
                if (ret.Values[i].Type.TypeKind == TypeKind.UntypedNil
                    && i < _currentReturnTypes.Count)
                {
                    var targetClrType = _ctx.Mapper.Map(_currentReturnTypes[i]);
                    if (targetClrType.IsValueType)
                    {
                        var tempLocal = _ctx.IL.DeclareLocal(targetClrType);
                        _ctx.IL.Emit(OpCodes.Ldloca, tempLocal);
                        _ctx.IL.Emit(OpCodes.Initobj, targetClrType);
                        _ctx.IL.Emit(OpCodes.Ldloc, tempLocal);
                    }
                    else if (_currentReturnTypes[i].TypeKind == TypeKind.Map)
                    {
                        var nilMethod = targetClrType.GetMethod("Nil")!;
                        _ctx.IL.Emit(OpCodes.Call, nilMethod);
                    }
                    else
                    {
                        _ctx.IL.Emit(OpCodes.Ldnull);
                    }
                }
                else
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
            }

            var tupleTypes = new Type[ret.Values.Count];
            for (int i = 0; i < ret.Values.Count; i++)
            {
                // Use declared return type when the value type doesn't match
                // (nil literals, interface types, untyped constants)
                if (i < _currentReturnTypes.Count
                    && (ret.Values[i].Type.TypeKind == TypeKind.UntypedNil
                        || _currentReturnTypes[i] is InterfaceTypeSymbol))
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
                    var boxCtor = _ctx.Definitions.GetConstructor(boxType, new[] { clrType });
                    _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                }
                else if (decl.Symbol.Type.TypeKind == TypeKind.Struct && !clrType.IsValueType)
                {
                    // Runtime reference type (e.g. sync.WaitGroup) — construct then box
                    var ctor = clrType.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        _ctx.IL.Emit(OpCodes.Newobj, ctor);
                        var boxCtor = _ctx.Definitions.GetConstructor(boxType, new[] { clrType });
                        _ctx.IL.Emit(OpCodes.Newobj, boxCtor);
                    }
                    else
                    {
                        var boxCtorEmpty = _ctx.Definitions.GetConstructor(boxType, Type.EmptyTypes);
                        _ctx.IL.Emit(OpCodes.Newobj, boxCtorEmpty);
                    }
                }
                else
                {
                    var boxCtorEmpty = _ctx.Definitions.GetConstructor(boxType, Type.EmptyTypes);
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
                    if (TryEmitSliceElementPointer(decl.Symbol, decl.Initializer))
                    {
                        // Variable tracks a slice element via ref — no Ptr<T> created.
                        // The local in Locals is unused; all access goes through
                        // the SliceElementPointers entry.
                    }
                    else
                    {
                        EmitExpression(decl.Initializer);
                        EmitInterfaceWrapIfNeeded(decl.Initializer.Type, decl.Symbol.Type, clrType);
                        _ctx.IL.Emit(OpCodes.Stloc, local);
                    }
                }
                else if (decl.Symbol.Type is ArrayTypeSymbol arrayDecl)
                {
                    // Go arrays are zero-initialized: var buf [5]byte → new byte[5]
                    var elemClrType = _ctx.Mapper.Map(arrayDecl.ElementType);
                    _ctx.IL.Emit(OpCodes.Ldc_I4, arrayDecl.Length);
                    _ctx.IL.Emit(OpCodes.Newarr, elemClrType);
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                }
                else if (decl.Symbol.Type is SliceTypeSymbol sliceDecl)
                {
                    // Go slices are nil by default — create empty Slice<T>
                    var sliceClrType = _ctx.Mapper.Map(sliceDecl);
                    _ctx.IL.Emit(OpCodes.Ldloca, local);
                    _ctx.IL.Emit(OpCodes.Initobj, sliceClrType);
                }
                else if (decl.Symbol.Type is MapTypeSymbol mapDecl)
                {
                    // Go nil maps return zero values on read, panic on write.
                    // Must initialize with Map<K,V>.Nil() — not .NET null — so instance
                    // method calls (indexer, Len, etc.) don't NullReferenceException.
                    var mapClrType = _ctx.Mapper.Map(mapDecl);
                    var nilMethod = mapClrType.GetMethod("Nil")!;
                    _ctx.IL.Emit(OpCodes.Call, nilMethod);
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
                    var boxCtor = _ctx.Definitions.GetConstructor(boxType, new[] { types[i] });
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
                    _ctx.IL.Emit(OpCodes.Stfld, _ctx.Definitions.GetField(existingLocal!.LocalType, "Value"));
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
                    if (!_ctx.StructFields.TryGetValue(sel.Field, out var fb))
                    {
                        throw new InvalidOperationException(
                            $"EmitMultiAssignment: field '{sel.Field.Name}' not found in StructFields " +
                            $"(declaring type={sel.Target?.Type?.Name}, pkg={_ctx.CurrentPackagePath})");
                    }
                    _ctx.IL.Emit(OpCodes.Stfld, fb.AsFieldInfo());
                }
                else if (target is IndexExpression idx)
                {
                    var tempLocal = _ctx.IL.DeclareLocal(types[i]);
                    _ctx.IL.Emit(OpCodes.Stloc, tempLocal);
                    EmitIndexAssignmentFromTemp(idx, tempLocal);
                }
                else if (target is DerefExpression deref)
                {
                    var tempLocal = _ctx.IL.DeclareLocal(types[i]);
                    _ctx.IL.Emit(OpCodes.Stloc, tempLocal);
                    EmitExpression(deref.Operand);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempLocal);
                    var ptrType = _ctx.Mapper.Map(deref.Operand.Type);
                    var valueField = _ctx.Definitions.GetField(ptrType, "Value");
                    _ctx.IL.Emit(OpCodes.Stfld, valueField);
                }
            }
        }

        private void EmitTupleCreate(Type[] elementTypes)
        {
            if (elementTypes.Length <= 7)
            {
                var tupleType = TypeMapper.MakeValueTupleType(elementTypes);
                var ctor = _ctx.Definitions.GetConstructor(tupleType, elementTypes);
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
            var outerCtor = _ctx.Definitions.GetConstructor(outerTupleType, outerTypes);
            _ctx.IL.Emit(OpCodes.Newobj, outerCtor);
        }

        private void EmitTupleFieldLoad(LocalBuilder tupleLocal, Type tupleType, int index)
        {
            if (index < 7)
            {
                _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, _ctx.Definitions.GetField(tupleType, $"Item{index + 1}"));
                return;
            }

            // Nested tuple: navigate through Rest fields
            _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
            var currentType = tupleType;
            int remaining = index;

            while (remaining >= 7)
            {
                var restField = _ctx.Definitions.GetField(currentType, "Rest");
                _ctx.IL.Emit(OpCodes.Ldflda, restField);
                currentType = restField.FieldType;
                remaining -= 7;
            }

            _ctx.IL.Emit(OpCodes.Ldfld, _ctx.Definitions.GetField(currentType, $"Item{remaining + 1}"));
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
                    if (!_ctx.StructFields.TryGetValue(sel.Field, out var fb))
                    {
                        throw new InvalidOperationException(
                            $"EmitParallelAssignment: field '{sel.Field.Name}' not found in StructFields " +
                            $"(declaring type={sel.Target?.Type?.Name}, pkg={_ctx.CurrentPackagePath})");
                    }
                    _ctx.IL.Emit(OpCodes.Stfld, fb.AsFieldInfo());
                }
                else if (target is IndexExpression idx)
                {
                    var tempVal = _ctx.IL.DeclareLocal(_ctx.Mapper.Map(target.Type));
                    _ctx.IL.Emit(OpCodes.Stloc, tempVal);
                    // Use the same emit path as regular index assignment
                    EmitIndexAssignmentFromTemp(idx, tempVal);
                }
                else if (target is DerefExpression deref)
                {
                    var tempVal = _ctx.IL.DeclareLocal(_ctx.Mapper.Map(target.Type));
                    _ctx.IL.Emit(OpCodes.Stloc, tempVal);
                    EmitExpression(deref.Operand);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempVal);
                    var ptrType = _ctx.Mapper.Map(deref.Operand.Type);
                    var valueField = _ctx.Definitions.GetField(ptrType, "Value");
                    _ctx.IL.Emit(OpCodes.Stfld, valueField);
                }
            }
        }

        private void EmitAssignment(AssignmentStatement assign)
        {
            switch (assign.Target)
            {
                case IdentifierExpression id:
                    EmitNilAwareExpression(assign.Value, id.Type);
                    EmitStore(id.Symbol);
                    break;

                case SelectorExpression sel:
                    // p.X = value → load address of p, then stfld
                    EmitAddressForStore(sel.Target);
                    EmitNilAwareExpression(assign.Value, sel.Type);
                    if (!_ctx.StructFields.TryGetValue(sel.Field, out var assignFb))
                    {
                        throw new InvalidOperationException(
                            $"EmitAssignment: field '{sel.Field.Name}' not found in StructFields " +
                            $"(declaring type={sel.Target?.Type?.Name}, pkg={_ctx.CurrentPackagePath})");
                    }
                    _ctx.IL.Emit(OpCodes.Stfld, assignFb.AsFieldInfo());
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
                    _ctx.IL.Emit(OpCodes.Stfld, _ctx.Definitions.GetField(ptrType, "Value"));
                    break;

                default:
                    if (_ctx.IsDependencyEmit)
                    {
                        _ctx.IL.Emit(OpCodes.Pop);
                        break;
                    }
                    throw new NotSupportedException(
                        $"Assignment target not supported: {assign.Target.NodeType}");
            }
        }

        private void EmitIncDec(IncDecStatement incDec)
        {
            if (incDec.Operand is IdentifierExpression id)
            {
                EmitLoad(id.Symbol);
                EmitIntConstant(1, id.Type);
                _ctx.IL.Emit(incDec.IsIncrement ? OpCodes.Add : OpCodes.Sub);
                EmitStore(id.Symbol);
            }
            else if (incDec.Operand is IndexExpression idx)
            {
                // slice[i]++ or array[i]++ or map[k]++
                var targetType = ResolveTypeParamConstraint(idx.Target.Type);
                if (targetType is SliceTypeSymbol)
                {
                    var sliceClrType = _ctx.Mapper.Map(targetType);
                    var elemClrType = _ctx.Mapper.Map(idx.Type);

                    // Get ref to element
                    EmitExpressionAddress(idx.Target, sliceClrType);
                    EmitExpression(idx.Index);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    var indexerGetter = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Item");
                    _ctx.IL.Emit(OpCodes.Call, indexerGetter);

                    // Duplicate ref, load value, inc/dec, store back
                    _ctx.IL.Emit(OpCodes.Dup);
                    _ctx.IL.Emit(OpCodes.Ldobj, elemClrType);
                    EmitIntConstant(1, idx.Type);
                    _ctx.IL.Emit(incDec.IsIncrement ? OpCodes.Add : OpCodes.Sub);
                    _ctx.IL.Emit(OpCodes.Stobj, elemClrType);
                }
                else if (targetType is ArrayTypeSymbol)
                {
                    var elemClrType = _ctx.Mapper.Map(idx.Type);
                    EmitExpression(idx.Target);
                    EmitExpression(idx.Index);
                    _ctx.IL.Emit(OpCodes.Conv_I4);

                    // Duplicate array + index for store, then load element, inc/dec, store
                    var arrLocal = _ctx.IL.DeclareLocal(_ctx.Mapper.Map(targetType));
                    var idxLocal = _ctx.IL.DeclareLocal(typeof(int));
                    _ctx.IL.Emit(OpCodes.Stloc, idxLocal);
                    _ctx.IL.Emit(OpCodes.Stloc, arrLocal);

                    _ctx.IL.Emit(OpCodes.Ldloc, arrLocal);
                    _ctx.IL.Emit(OpCodes.Ldloc, idxLocal);
                    _ctx.IL.Emit(OpCodes.Ldloc, arrLocal);
                    _ctx.IL.Emit(OpCodes.Ldloc, idxLocal);
                    EmitLdelem(elemClrType);
                    EmitIntConstant(1, idx.Type);
                    _ctx.IL.Emit(incDec.IsIncrement ? OpCodes.Add : OpCodes.Sub);
                    EmitStelem(elemClrType);
                }
                else if (targetType is MapTypeSymbol)
                {
                    var mapClrType = _ctx.Mapper.Map(targetType);
                    // m[k]++ → m[k] = m[k] + 1
                    EmitExpression(idx.Target);
                    EmitExpression(idx.Index);
                    EmitExpression(idx.Target);
                    EmitExpression(idx.Index);
                    var getter = _ctx.Definitions.GetPropertyGetter(mapClrType, "Item");
                    _ctx.IL.Emit(OpCodes.Call, getter);
                    EmitIntConstant(1, idx.Type);
                    _ctx.IL.Emit(incDec.IsIncrement ? OpCodes.Add : OpCodes.Sub);
                    var setter = _ctx.Definitions.GetPropertySetter(mapClrType, "Item");
                    _ctx.IL.Emit(OpCodes.Call, setter);
                }
                else
                {
                    throw new NotSupportedException($"IncDec on index into {targetType.TypeKind} not supported");
                }
            }
            else if (incDec.Operand is SelectorExpression sel && sel.Field != null
                     && _ctx.StructFields.TryGetValue(sel.Field, out var fieldBuilder))
            {
                // s.field++ → load address, load field, inc/dec, store field
                EmitAddressForStore(sel.Target);
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldfld, fieldBuilder.AsFieldInfo());
                EmitIntConstant(1, incDec.Operand.Type);
                _ctx.IL.Emit(incDec.IsIncrement ? OpCodes.Add : OpCodes.Sub);
                _ctx.IL.Emit(OpCodes.Stfld, fieldBuilder.AsFieldInfo());
            }
            else if (incDec.Operand is DerefExpression deref)
            {
                // (*p)++
                var innerType = _ctx.Mapper.Map(deref.Type);
                var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                EmitExpression(deref.Operand);
                _ctx.IL.Emit(OpCodes.Dup);
                _ctx.IL.Emit(OpCodes.Ldfld, _ctx.Definitions.GetField(ptrType, "Value"));
                EmitIntConstant(1, deref.Type);
                _ctx.IL.Emit(incDec.IsIncrement ? OpCodes.Add : OpCodes.Sub);
                _ctx.IL.Emit(OpCodes.Stfld, _ctx.Definitions.GetField(ptrType, "Value"));
            }
            else
            {
                throw new NotSupportedException(
                    $"IncDec operand type not supported: {incDec.Operand.NodeType}");
            }
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
                    var innerType = boxType.GetGenericArguments()[0];
                    var counterLocal = _ctx.IL.DeclareLocal(innerType);
                    counterLocals[sym] = counterLocal;

                    // Copy initial value from Box to counter
                    var valueField = _ctx.Definitions.GetField(boxType, "Value");
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
                    var boxCtor = _ctx.Definitions.GetConstructor(boxType, new[] { innerType });
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
                    var valueField = _ctx.Definitions.GetField(boxLocal.LocalType, "Value");
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

            // Reorder cases so default is last (Go allows default anywhere in source)
            var orderedCases = new List<(int originalIndex, Ast.SwitchCase switchCase)>();
            int defaultOriginalIndex = -1;
            for (int i = 0; i < sw.Cases.Count; i++)
            {
                if (sw.Cases[i].IsDefault)
                {
                    defaultOriginalIndex = i;
                }
                else
                {
                    orderedCases.Add((i, sw.Cases[i]));
                }
            }
            if (defaultOriginalIndex >= 0)
            {
                orderedCases.Add((defaultOriginalIndex, sw.Cases[defaultOriginalIndex]));
            }

            // Pre-define body labels for fallthrough support
            var bodyLabels = new Label[sw.Cases.Count];
            for (int i = 0; i < sw.Cases.Count; i++)
                bodyLabels[i] = _ctx.IL.DefineLabel();

            var savedFallthroughLabel = _ctx.FallthroughLabel;

            // Push break label so explicit break statements inside cases jump to end
            _ctx.LoopLabels.Push(new LoopLabel(endLabel, endLabel));

            // Emit as if-else chain
            var nextCaseLabel = _ctx.IL.DefineLabel();
            for (int oci = 0; oci < orderedCases.Count; oci++)
            {
                var (ci, c) = orderedCases[oci];

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
                                var strEq = typeof(GoString).GetMethod("op_Equality", new[] { typeof(GoString), typeof(GoString) })!;
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

                // Set fallthrough target to next case's body in source order (or end if last)
                _ctx.FallthroughLabel = ci + 1 < sw.Cases.Count ? bodyLabels[ci + 1] : endLabel;
                // For reordered cases, use emit order for fallthrough
                if (oci + 1 < orderedCases.Count)
                {
                    _ctx.FallthroughLabel = bodyLabels[orderedCases[oci + 1].originalIndex];
                }

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

            _ctx.LoopLabels.Pop();
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
                            _ctx.IL.Emit(OpCodes.Br, named.BreakLabel);
                            break;
                        case BranchKind.Continue:
                            _ctx.IL.Emit(OpCodes.Br, named.ContinueLabel);
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

            var loopLabel = _ctx.LoopLabels.Peek();
            switch (branch.BranchKind)
            {
                case BranchKind.Break:
                    _ctx.IL.Emit(OpCodes.Br, loopLabel.BreakLabel);
                    break;
                case BranchKind.Continue:
                    _ctx.IL.Emit(OpCodes.Br, loopLabel.ContinueLabel);
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
            _ctx.LoopLabels.Push(new LoopLabel(breakLabel, continueLabel));
            if (_pendingLabel != null)
            {
                _ctx.NamedLabels[_pendingLabel] = new LoopLabel(breakLabel, continueLabel);
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
                        _ctx.IL.Emit(OpCodes.Ldfld, _ctx.Definitions.GetField(tupleType, "Item1"));
                    }
                    break;
                case NodeType.SpreadElement:
                    var spread = (SpreadElement)expr;
                    if (_spreadLocals.TryGetValue(spread.Source, out var srcLocal))
                    {
                        var srcType = srcLocal.LocalType;
                        _ctx.IL.Emit(OpCodes.Ldloca, srcLocal);
                        _ctx.IL.Emit(OpCodes.Ldfld, _ctx.Definitions.GetField(srcType, $"Item{spread.Index + 1}"));
                    }
                    break;
                case NodeType.ConversionExpression:
                    EmitConversion((ConversionExpression)expr);
                    break;
                case NodeType.SelectorExpression:
                    EmitSelector((SelectorExpression)expr);
                    break;
                case NodeType.MethodCallExpression:
                    var methodCallExpr = (MethodCallExpression)expr;
                    EmitMethodCall(methodCallExpr);
                    if (methodCallExpr.IsSpreadArg)
                    {
                        var tupleType = _ctx.Mapper.MapReturnType(methodCallExpr.Method.ReturnTypes);
                        var tupleLocal = _ctx.IL.DeclareLocal(tupleType);
                        _ctx.IL.Emit(OpCodes.Stloc, tupleLocal);
                        _spreadLocals[methodCallExpr] = tupleLocal;
                        _ctx.IL.Emit(OpCodes.Ldloca, tupleLocal);
                        _ctx.IL.Emit(OpCodes.Ldfld, _ctx.Definitions.GetField(tupleType, "Item1"));
                    }
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
                    if (_ctx.IsDependencyEmit)
                    {
                        var exprClrType = _ctx.Mapper.Map(expr.Type);
                        if (exprClrType.IsValueType)
                        {
                            var tempLocal = _ctx.IL.DeclareLocal(exprClrType);
                            _ctx.IL.Emit(OpCodes.Ldloca, tempLocal);
                            _ctx.IL.Emit(OpCodes.Initobj, exprClrType);
                            _ctx.IL.Emit(OpCodes.Ldloc, tempLocal);
                        }
                        else
                        {
                            _ctx.IL.Emit(OpCodes.Ldnull);
                        }
                        break;
                    }
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
                case TypeKind.UntypedRune:
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
                    _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("FromNetString")!);
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
                var concat = typeof(GoString).GetMethod("op_Addition", new[] { typeof(GoString), typeof(GoString) })!;
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
                    var equals = typeof(GoString).GetMethod("op_Equality", new[] { typeof(GoString), typeof(GoString) })!;
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
                    var compareTo = typeof(GoString).GetMethod("CompareTo", new[] { typeof(GoString) })!;
                    // CompareTo is an instance method — need to rearrange: stash right, call left.CompareTo(right)
                    var tempRight = _ctx.IL.DeclareLocal(typeof(GoString));
                    var tempLeft = _ctx.IL.DeclareLocal(typeof(GoString));
                    _ctx.IL.Emit(OpCodes.Stloc, tempRight);
                    _ctx.IL.Emit(OpCodes.Stloc, tempLeft);
                    _ctx.IL.Emit(OpCodes.Ldloca, tempLeft);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempRight);
                    _ctx.IL.Emit(OpCodes.Call, compareTo);
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
                EmitArrayEquality(bin, (ArrayTypeSymbol)bin.Left.Type.Resolved());
                return;
            }

            // Slice/Map nil comparison: use the IsNil property.
            // Slice<T> is a value type so Ceq with null never works;
            // Map<K,V>.Nil() is an object instance so reference equality with null is wrong.
            if ((bin.Operator == BinaryOperator.Equal || bin.Operator == BinaryOperator.NotEqual)
                && (bin.Left.Type.TypeKind == TypeKind.Slice || bin.Left.Type.TypeKind == TypeKind.Map)
                && bin.Right.Type.TypeKind == TypeKind.UntypedNil)
            {
                EmitSliceOrMapNilCheck(bin);
                return;
            }
            if ((bin.Operator == BinaryOperator.Equal || bin.Operator == BinaryOperator.NotEqual)
                && bin.Left.Type.TypeKind == TypeKind.UntypedNil
                && (bin.Right.Type.TypeKind == TypeKind.Slice || bin.Right.Type.TypeKind == TypeKind.Map))
            {
                var swapped = new BinaryExpression(bin.Right, bin.Operator, bin.Left, bin.Type, bin.Span);
                EmitSliceOrMapNilCheck(swapped);
                return;
            }

            // Generic type parameter comparison — Clt/Cgt opcodes don't work for generic T.
            // Use Comparer<T>.Default.Compare(left, right) which returns int, then compare with 0.
            var leftPrecheck = _ctx.Mapper.Map(bin.Left.Type);
            if (leftPrecheck != null && leftPrecheck.IsGenericParameter
                && (bin.Operator == BinaryOperator.Less || bin.Operator == BinaryOperator.Greater
                    || bin.Operator == BinaryOperator.LessOrEqual || bin.Operator == BinaryOperator.GreaterOrEqual))
            {
                var openComparerType = typeof(Comparer<>);
                var closedComparerType = openComparerType.MakeGenericType(leftPrecheck);
                var defaultGetter = TypeBuilder.GetMethod(closedComparerType,
                    openComparerType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!.GetGetMethod()!);
                var compareMethod = TypeBuilder.GetMethod(closedComparerType,
                    openComparerType.GetMethod("Compare")!);

                _ctx.IL.Emit(OpCodes.Call, defaultGetter);
                EmitExpression(bin.Left);
                EmitExpression(bin.Right);
                _ctx.IL.Emit(OpCodes.Callvirt, compareMethod);
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

            EmitExpression(bin.Left);
            var leftClrType = _ctx.Mapper.Map(bin.Left.Type);
            EmitExpression(bin.Right);
            var rightClrType = _ctx.Mapper.Map(bin.Right.Type);

            // Match operand types on the stack when they have different CLR sizes.
            // In Go, untyped constants take the type of the typed operand, so we narrow
            // the untyped side to match, rather than widening the typed side.
            // Skip entirely for shift operators: the shift count type is independent
            // of the value type, and CLR shift instructions take the count as int32.
            bool isShift = bin.Operator is BinaryOperator.ShiftLeft or BinaryOperator.ShiftRight;
            if (!isShift && leftClrType != rightClrType && leftClrType.IsPrimitive && rightClrType.IsPrimitive)
            {
                bool leftIsUntyped = TypeChecker.IsUntyped(bin.Left.Type);
                bool rightIsUntyped = TypeChecker.IsUntyped(bin.Right.Type);

                if (leftIsUntyped && !rightIsUntyped)
                {
                    // Left is untyped — narrow it to match the typed right operand.
                    // Left is below right on the stack, so store right, convert left, reload right.
                    var tempRight = _ctx.IL.DeclareLocal(rightClrType);
                    _ctx.IL.Emit(OpCodes.Stloc, tempRight);
                    EmitConvOpcode(bin.Right.Type.TypeKind);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempRight);
                }
                else if (rightIsUntyped && !leftIsUntyped)
                {
                    // Right is untyped — narrow it to match the typed left operand.
                    EmitConvOpcode(bin.Left.Type.TypeKind);
                }
                else
                {
                    // Both typed (or both untyped): widen the smaller to the larger.
                    int leftSize = System.Runtime.InteropServices.Marshal.SizeOf(leftClrType);
                    int rightSize = System.Runtime.InteropServices.Marshal.SizeOf(rightClrType);
                    if (leftSize < rightSize)
                    {
                        var tempRight = _ctx.IL.DeclareLocal(rightClrType);
                        _ctx.IL.Emit(OpCodes.Stloc, tempRight);
                        EmitConvOpcode(bin.Right.Type.TypeKind);
                        _ctx.IL.Emit(OpCodes.Ldloc, tempRight);
                    }
                    else if (rightSize < leftSize)
                    {
                        EmitConvOpcode(bin.Left.Type.TypeKind);
                    }
                }
            }

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
                case BinaryOperator.ShiftLeft:
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    _ctx.IL.Emit(OpCodes.Shl);
                    break;
                case BinaryOperator.ShiftRight:
                    _ctx.IL.Emit(OpCodes.Conv_I4);
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

        private void EmitSliceOrMapNilCheck(BinaryExpression bin)
        {
            var clrType = _ctx.Mapper.Map(bin.Left.Type);
            bool isMapType = bin.Left.Type.TypeKind == TypeKind.Map;

            MethodInfo? isNilGetter = null;
            try
            {
                isNilGetter = clrType.GetProperty("IsNil")?.GetGetMethod();
            }
            catch (NotSupportedException)
            {
                // TypeBuilder doesn't support GetProperty
            }

            if (isNilGetter == null && clrType.IsGenericType)
            {
                var genericDef = clrType.GetGenericTypeDefinition();
                var baseProp = genericDef.GetProperty("IsNil");
                if (baseProp != null)
                {
                    isNilGetter = TypeBuilder.GetMethod(clrType, baseProp.GetGetMethod()!);
                }
            }

            if (isNilGetter == null)
            {
                var runtimeGenericDef = isMapType ? typeof(Map<,>) : typeof(Slice<>);
                var baseGetter = runtimeGenericDef.GetProperty("IsNil")!.GetGetMethod()!;
                isNilGetter = TypeBuilder.GetMethod(clrType, baseGetter);
            }

            if (isMapType)
            {
                // Map<K,V> is a reference type — load the reference and callvirt
                EmitExpression(bin.Left);
                _ctx.IL.Emit(OpCodes.Callvirt, isNilGetter);
            }
            else
            {
                // Slice<T> is a value type — load address and call
                EmitExpressionAddress(bin.Left, clrType);
                _ctx.IL.Emit(OpCodes.Call, isNilGetter);
            }

            if (bin.Operator == BinaryOperator.NotEqual)
            {
                _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                _ctx.IL.Emit(OpCodes.Ceq);
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
                _ctx.IL.Emit(OpCodes.Ldfld, fb.AsFieldInfo());
                _ctx.IL.Emit(OpCodes.Ldloca, rightLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, fb.AsFieldInfo());

                if (field.Type.TypeKind == TypeKind.String || field.Type.TypeKind == TypeKind.UntypedString)
                {
                    var equals = typeof(GoString).GetMethod("op_Equality", new[] { typeof(GoString), typeof(GoString) })!;
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
                    var equals = typeof(GoString).GetMethod("op_Equality", new[] { typeof(GoString), typeof(GoString) })!;
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

                    var instantiated = method.AsMethodInfo().MakeGenericMethod(typeArgs);
                    EmitCallArguments(call);
                    _ctx.IL.Emit(OpCodes.Call, instantiated);
                    EmitPendingWritebacks();
                    return;
                }

                if (call.Function.IsVariadic)
                {
                    EmitVariadicCall(call, method.AsMethodInfo());
                }
                else
                {
                    EmitCallArguments(call);
                    _ctx.IL.Emit(OpCodes.Call, method.AsMethodInfo());
                    EmitPendingWritebacks();
                }

                return;
            }

            // Check cached assemblies (precompiled packages)
            if (_ctx.CachedMethods.TryGetValue(call.Function, out var cachedMethod))
            {
                MethodInfo resolvedCachedMethod = cachedMethod;
                if (cachedMethod.IsGenericMethodDefinition
                    && call.TypeArguments != null && call.TypeArguments.Count > 0)
                {
                    var typeArgs = new Type[call.TypeArguments.Count];
                    for (int i = 0; i < call.TypeArguments.Count; i++)
                    {
                        typeArgs[i] = _ctx.Mapper.Map(call.TypeArguments[i]);
                    }
                    resolvedCachedMethod = cachedMethod.MakeGenericMethod(typeArgs);
                }

                if (call.Function.IsVariadic)
                {
                    EmitVariadicCall(call, resolvedCachedMethod);
                }
                else
                {
                    EmitCallArguments(call);
                    _ctx.IL.Emit(OpCodes.Call, resolvedCachedMethod);
                    EmitPendingWritebacks();
                }
                return;
            }

            // Handle builtins by name
            if (_builtins.EmitBuiltinCall(call))
                return;

            // Runtime package function: resolve CLR method by package path + function name
            if (call.Function.PackageName != null || call.Function is Symbols.FunctionSymbol funcSym2 && funcSym2.PackageName != null)
            {
                var pkgName = call.Function.PackageName ?? ((Symbols.FunctionSymbol)call.Function).PackageName;
                var runtimeMethod = ResolveRuntimeFunction(pkgName, call.Function.Name, call);
                if (runtimeMethod != null)
                {
                    var runtimeParams = runtimeMethod.GetParameters();
                    bool runtimeIsVariadic = runtimeParams.Length > 0
                        && runtimeParams[runtimeParams.Length - 1].IsDefined(typeof(System.ParamArrayAttribute), false);

                    if (runtimeIsVariadic && call.Arguments.Count >= runtimeParams.Length - 1)
                    {
                        int fixedCount = runtimeParams.Length - 1;
                        var arrayElemType = runtimeParams[fixedCount].ParameterType.GetElementType()!;

                        // Emit fixed (non-variadic) arguments
                        for (int i = 0; i < fixedCount; i++)
                        {
                            EmitExpression(call.Arguments[i]);
                            if (runtimeParams[i].ParameterType == typeof(string)
                                && _ctx.Mapper.Map(call.Arguments[i].Type) == typeof(GoString))
                            {
                                EmitGoStringToNetString();
                            }
                        }

                        // Pack remaining args into T[] for params
                        int varCount = call.Arguments.Count - fixedCount;
                        _ctx.IL.Emit(OpCodes.Ldc_I4, varCount);
                        _ctx.IL.Emit(OpCodes.Newarr, arrayElemType);
                        for (int i = 0; i < varCount; i++)
                        {
                            _ctx.IL.Emit(OpCodes.Dup);
                            _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                            EmitExpression(call.Arguments[fixedCount + i]);
                            var argClrType = _ctx.Mapper.Map(call.Arguments[fixedCount + i].Type);
                            if (argClrType == typeof(GoString) && arrayElemType == typeof(object))
                            {
                                EmitGoStringToNetString();
                                _ctx.IL.Emit(OpCodes.Box, typeof(string));
                            }
                            else if (argClrType.IsValueType && arrayElemType == typeof(object))
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

                        _ctx.IL.Emit(OpCodes.Call, runtimeMethod);
                    }
                    else if (!call.Function.IsVariadic)
                    {
                        EmitRuntimeCallArguments(call, runtimeParams);
                        _ctx.IL.Emit(OpCodes.Call, runtimeMethod);
                        EmitPendingWritebacks();
                    }
                    else
                    {
                        EmitVariadicCall(call, runtimeMethod);
                    }
                    return;
                }

                // Source-compiled dependency: look up in LinkedMethods
                if (call.Function is Symbols.FunctionSymbol linkedFunc)
                {
                    var lastSlash = pkgName.LastIndexOf('/');
                    var shortPkg = lastSlash >= 0 ? pkgName.Substring(lastSlash + 1) : pkgName;
                    var qualifiedKey = shortPkg + "." + linkedFunc.Name;

                    MethodBuilder linkedMethod = null;
                    if (!_ctx.LinkedMethods.TryGetValue(qualifiedKey, out linkedMethod))
                    {
                        _ctx.LinkedMethods.TryGetValue(linkedFunc.Name, out linkedMethod);
                    }

                    if (linkedMethod != null)
                    {
                        MethodInfo resolvedLinked = linkedMethod;
                        if (linkedMethod.IsGenericMethodDefinition
                            && call.TypeArguments != null && call.TypeArguments.Count > 0)
                        {
                            var typeArgs = new Type[call.TypeArguments.Count];
                            for (int i = 0; i < typeArgs.Length; i++)
                            {
                                typeArgs[i] = _ctx.Mapper.Map(call.TypeArguments[i]);
                            }
                            resolvedLinked = linkedMethod.MakeGenericMethod(typeArgs);
                        }

                        if (linkedFunc.IsVariadic)
                        {
                            EmitVariadicCall(call, resolvedLinked);
                        }
                        else
                        {
                            EmitCallArguments(call);
                            _ctx.IL.Emit(OpCodes.Call, resolvedLinked);
                            EmitPendingWritebacks();
                        }
                        return;
                    }
                }
            }

            // Indirect call: function variable (delegate)
            if (call.CallTarget != null)
            {
                EmitExpression(call.CallTarget);
                foreach (var arg in call.Arguments)
                    EmitExpression(arg);

                var delegateType = _ctx.Mapper.Map(call.CallTarget.Type);
                var invokeMethod = _ctx.Definitions.GetMethod(delegateType, "Invoke");
                if (invokeMethod == null)
                {
                    // Delegate type mapping failed — try direct call by name
                    MethodInfo? directClr = null;
                    if (_ctx.Methods.TryGetValue(call.Function, out var directMethod))
                    {
                        directClr = directMethod.AsMethodInfo();
                    }
                    else
                    {
                        // Search by name
                        foreach (var kvp in _ctx.Methods)
                        {
                            if (kvp.Key.Name == call.Function.Name)
                            {
                                directClr = kvp.Value.AsMethodInfo();
                                break;
                            }
                        }
                    }
                    if (directClr == null)
                    {
                        foreach (var kvp in _ctx.CachedMethods)
                        {
                            if (kvp.Key.Name == call.Function.Name)
                            {
                                directClr = kvp.Value;
                                break;
                            }
                        }
                    }
                    if (directClr != null)
                    {
                        // Pop the function value (not needed for direct call) and call directly
                        _ctx.IL.Emit(OpCodes.Pop);
                        _ctx.IL.Emit(OpCodes.Call, directClr);
                    }
                    else
                    {
                        if (_ctx.IsDependencyEmit || delegateType == typeof(Delegate) || delegateType == typeof(object))
                        {
                            for (int i = 0; i < call.Arguments.Count; i++)
                            {
                                _ctx.IL.Emit(OpCodes.Pop);
                            }
                            _ctx.IL.Emit(OpCodes.Pop); // function value
                            if (call.Function.ReturnType != BuiltinTypes.Void)
                            {
                                _ctx.IL.Emit(OpCodes.Ldnull);
                            }
                            return;
                        }
                        throw new NotSupportedException($"Cannot resolve indirect call: {call.Function.Name}");
                    }
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Callvirt, invokeMethod);
                }
                return;
            }

            // Cross-package call during dependency emit: emit real IL with proxy MethodInfo.
            // NgoWriter captures the symbolic token reference (e.g., "slices::Sort(...)").
            // The linker resolves it at link time against the host module's linked types.

            if (_ctx.IsDependencyEmit && call.Function is Symbols.FunctionSymbol crossFunc
                && crossFunc.PackageName != null)
            {
                var fullPkgName = crossFunc.PackageName ?? "unknown";
                var lastSlash = fullPkgName.LastIndexOf('/');
                var shortPkgName = lastSlash >= 0 ? fullPkgName.Substring(lastSlash + 1) : fullPkgName;

                // When emitting into a real module (EmitDependencyFromSource), resolve
                // cross-package calls against already-linked MethodBuilders. Proxy methods
                // only work with NgoWriter for archive serialization.
                bool isRealModule = _ctx.Module is Builder.LiveModuleBuilder;
                if (isRealModule)
                {
                    // Try to find the linked method by name
                    MethodBuilder? linkedCrossMethod = null;
                    var funcName = crossFunc.Name;
                    var qualifiedKey = shortPkgName + "." + funcName;
                    if (!_ctx.LinkedMethods.TryGetValue(qualifiedKey, out linkedCrossMethod))
                    {
                        _ctx.LinkedMethods.TryGetValue(funcName, out linkedCrossMethod);
                    }

                    if (linkedCrossMethod != null)
                    {
                        MethodInfo resolvedMethod = linkedCrossMethod;
                        if (linkedCrossMethod.IsGenericMethodDefinition
                            && call.TypeArguments != null && call.TypeArguments.Count > 0)
                        {
                            var typeArgs = new Type[call.TypeArguments.Count];
                            for (int i = 0; i < typeArgs.Length; i++)
                            {
                                typeArgs[i] = _ctx.Mapper.Map(call.TypeArguments[i]);
                            }
                            resolvedMethod = linkedCrossMethod.MakeGenericMethod(typeArgs);
                        }

                        EmitCallArguments(call);
                        _ctx.IL.Emit(OpCodes.Call, resolvedMethod);
                        return;
                    }

                    // Linked method not found — try runtime function resolution.
                    // Runtime intrinsic packages (sort, fmt, etc.) won't have linked
                    // MethodBuilders since they're implemented in C#, not Go source.
                    var runtimeCrossMethod = ResolveRuntimeFunction(fullPkgName, crossFunc.Name, call);
                    if (runtimeCrossMethod != null)
                    {
                        EmitCallArguments(call);
                        _ctx.IL.Emit(OpCodes.Call, runtimeCrossMethod);
                        EmitPendingWritebacks();
                        return;
                    }
                }

                // On real modules, if we didn't find the linked method above, we can't
                // create a proxy (NgoProxyMethodInfo only works with NgoWriter).
                // Fall through to the IsDependencyEmit stub.
                if (!isRealModule)
                {
                // Archive serialization path: create proxy for NgoWriter to capture
                var declaringType = new Builder.NgoProxyType(shortPkgName);

                Type[] clrParamTypes;
                Type clrReturnType;

                if (call.TypeArguments != null && call.TypeArguments.Count > 0
                    && crossFunc.TypeParameters.Count > 0)
                {
                    var substParams = TypeSubstituter.SubstituteParams(
                        crossFunc.Parameters, crossFunc.TypeParameters, call.TypeArguments);
                    var substReturns = TypeSubstituter.SubstituteTypes(
                        crossFunc.ReturnTypes, crossFunc.TypeParameters, call.TypeArguments);

                    clrParamTypes = new Type[substParams.Count];
                    for (int i = 0; i < clrParamTypes.Length; i++)
                    {
                        clrParamTypes[i] = _ctx.Mapper.Map(substParams[i].Type);
                    }
                    clrReturnType = _ctx.Mapper.MapReturnType(substReturns);
                }
                else
                {
                    clrParamTypes = new Type[call.Arguments.Count];
                    for (int i = 0; i < clrParamTypes.Length; i++)
                    {
                        clrParamTypes[i] = _ctx.Mapper.Map(call.Arguments[i].Type);
                    }
                    clrReturnType = _ctx.Mapper.MapReturnType(crossFunc.ReturnTypes);
                }

                var proxyMethodDef = new Builder.NgoProxyMethodInfo(
                    declaringType, crossFunc.Name, clrParamTypes, clrReturnType);

                MethodInfo proxyMethod = proxyMethodDef;
                if (call.TypeArguments != null && call.TypeArguments.Count > 0
                    && crossFunc.TypeParameters.Count > 0)
                {
                    var methodTypeArgs = new Type[call.TypeArguments.Count];
                    for (int i = 0; i < methodTypeArgs.Length; i++)
                    {
                        methodTypeArgs[i] = _ctx.Mapper.Map(call.TypeArguments[i]);
                    }
                    proxyMethod = proxyMethodDef.MakeGenericMethod(methodTypeArgs);
                }

                EmitCallArguments(call);
                _ctx.IL.Emit(OpCodes.Call, proxyMethod);
                return;
                } // end if (!isRealModule)
            }

            if (_ctx.IsDependencyEmit)
            {
                var argTypes = new string[call.Arguments.Count];
                for (int argIdx = 0; argIdx < call.Arguments.Count; argIdx++)
                {
                    argTypes[argIdx] = call.Arguments[argIdx].Type?.Name ?? "?";
                }
                throw new InvalidOperationException(
                    $"EmitCall: unresolved function '{call.Function.Name}' during dependency emit " +
                    $"(pkg={_ctx.CurrentPackagePath}, args=[{string.Join(",", argTypes)}], " +
                    $"returnType={call.Function.ReturnType?.Name})");
            }

            throw new NotSupportedException($"Cannot resolve function: {call.Function.Name} (pkg={call.Function.PackageName}, kind={call.Function.Kind})");
        }

        /// <summary>
        /// Emits an expression, but if it's a nil literal and the target type is a value type
        /// (e.g., Slice&lt;T&gt;), emits default(T) via Initobj instead of Ldnull.
        /// </summary>
        private void EmitNilAwareExpression(Expression value, TypeSymbol targetType)
        {
            if (value.Type.TypeKind == TypeKind.UntypedNil)
            {
                var targetClrType = _ctx.Mapper.Map(targetType);
                if (targetClrType.IsValueType)
                {
                    var tempLocal = _ctx.IL.DeclareLocal(targetClrType);
                    _ctx.IL.Emit(OpCodes.Ldloca, tempLocal);
                    _ctx.IL.Emit(OpCodes.Initobj, targetClrType);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempLocal);
                    return;
                }
                if (targetType.TypeKind == TypeKind.Map)
                {
                    var nilMethod = targetClrType.GetMethod("Nil")!;
                    _ctx.IL.Emit(OpCodes.Call, nilMethod);
                    return;
                }
            }
            EmitExpression(value);
        }

        /// <summary>
        /// Resolves a method on a runtime type by checking [GoMethod(Name="...")] attributes.
        /// Go names (e.g., "UTC") may differ from C# names (e.g., "UtcTime").
        /// </summary>
        private static MethodInfo? ResolveByGoMethodAttribute(Type clrType, string goMethodName, int paramCount)
        {
            try
            {
                foreach (var method in clrType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.GetParameters().Length != paramCount)
                    {
                        continue;
                    }
                    var goAttr = method.GetCustomAttribute(
                        typeof(Ngo.Runtime.Discovery.GoMethodAttribute)) as Ngo.Runtime.Discovery.GoMethodAttribute;
                    if (goAttr != null && goAttr.Name == goMethodName)
                    {
                        return method;
                    }
                }
            }
            catch (NotSupportedException)
            {
                // TypeBuilder doesn't support GetCustomAttribute
            }
            return null;
        }

        private MethodInfo? ResolveRuntimeFunction(string packageName, string funcName, CallExpression call)
        {
            // Find the CLR type for this Go package by scanning Ngo.Runtime assembly
            var runtimeAssembly = typeof(Ngo.Runtime.Slice<>).Assembly;
            Type? pkgType = null;
            foreach (var type in runtimeAssembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<Ngo.Runtime.Discovery.GoPackageAttribute>();
                if (attr == null)
                {
                    continue;
                }
                // Match by full import path or short name
                if (attr.ImportPath == packageName)
                {
                    pkgType = type;
                    break;
                }
                var shortName = attr.ImportPath.Contains('/')
                    ? attr.ImportPath.Substring(attr.ImportPath.LastIndexOf('/') + 1)
                    : attr.ImportPath;
                if (shortName == packageName)
                {
                    pkgType = type;
                    break;
                }
            }
            if (pkgType == null)
            {
                return null;
            }

            // Find the static method by Go function name
            foreach (var method in pkgType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                var goFuncAttr = method.GetCustomAttribute<Ngo.Runtime.Discovery.GoFuncAttribute>();
                var goName = goFuncAttr?.Name ?? method.Name;
                if (goName == funcName)
                {
                    return method;
                }
            }

            return null;
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
            var sliceSymbolType = (SliceTypeSymbol)lastParam.Type.Resolved();
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

            // Go-compiled functions always use Slice<T> for variadic params
            var ctor = _ctx.Definitions.GetConstructor(sliceClrType, new[] { elemClrType.MakeArrayType() });
            _ctx.IL.Emit(OpCodes.Newobj, ctor);
            _ctx.IL.Emit(OpCodes.Call, method);
        }

        private void EmitConversion(ConversionExpression conv)
        {
            var targetKind = conv.Type.TypeKind;
            var sourceKind = conv.Operand.Type.TypeKind;

            // int/rune/byte → string: create GoString from rune value
            if (targetKind == TypeKind.String
                && (sourceKind == TypeKind.Int || sourceKind == TypeKind.UntypedInt || sourceKind == TypeKind.UntypedRune
                    || sourceKind == TypeKind.Uint8 || sourceKind == TypeKind.Int32))
            {
                EmitExpression(conv.Operand);
                _ctx.IL.Emit(OpCodes.Conv_I4);
                _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("FromRune")!);
                return;
            }

            // string → []byte
            if (conv.Type is SliceTypeSymbol sliceType
                && sliceType.ElementType.TypeKind == TypeKind.Uint8
                && (sourceKind == TypeKind.String || sourceKind == TypeKind.UntypedString))
            {
                EmitExpression(conv.Operand);
                _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("ToBytes", new[] { typeof(GoString) })!);
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
                var indexer = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Item");
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
            if (!_ctx.StructFields.TryGetValue(sel.Field, out var fb))
            {
                // Fallback: match by name when symbol identity differs
                foreach (var kvp in _ctx.StructFields)
                {
                    if (kvp.Key.Name == sel.Field.Name
                        && kvp.Key.Type is ArrayTypeSymbol)
                    {
                        fb = kvp.Value;
                        break;
                    }
                }
                if (fb == null && sel.Field.Type is ArrayTypeSymbol)
                {
                    System.Console.Error.WriteLine($"FIELD-MISS name={sel.Field.Name} type={sel.Field.Type} structFieldCount={_ctx.StructFields.Count}");
                    foreach (var kvp in _ctx.StructFields)
                    {
                        if (kvp.Key.Name == sel.Field.Name)
                            System.Console.Error.WriteLine($"  CANDIDATE: {kvp.Key.Name} type={kvp.Key.Type} match={kvp.Key == sel.Field}");
                    }
                }
            }
            if (fb != null)
            {
                if (sel.Field.Type is ArrayTypeSymbol selectorArrType
                    && !fb.AsFieldInfo().FieldType.IsArray)
                {
                    EmitInlineArrayAsArray(sel, selectorArrType,
                        _ctx.Mapper.Map(selectorArrType.ElementType));
                    return;
                }
                EmitExpression(sel.Target);
                _ctx.IL.Emit(OpCodes.Ldfld, fb.AsFieldInfo());
                return;
            }

            // Runtime type field/property access (C# [GoField] properties on runtime types)
            var targetType = sel.Target.Type;
            var clrType = _ctx.Mapper.Map(targetType is PointerTypeSymbol ptr ? ptr.ElementType : targetType);
            if (clrType != null && clrType != typeof(object))
            {
                EmitExpression(sel.Target);
                // Dereference pointer if needed
                if (targetType is PointerTypeSymbol)
                {
                    // For reference types, pointer dereference is implicit
                    // For value types, need to load from Ptr<T>
                    if (clrType.IsValueType)
                    {
                        var ptrType = typeof(Ptr<>).MakeGenericType(clrType);
                        _ctx.IL.Emit(OpCodes.Ldfld, _ctx.Definitions.GetField(ptrType, "Value"));
                    }
                }

                // Check emitter-registered fields first (anonymous/dynamic structs)
                if (_ctx.StructFields.TryGetValue(sel.Field, out var registeredField))
                {
                    _ctx.IL.Emit(OpCodes.Ldfld, registeredField.AsFieldInfo());
                    return;
                }

                // Try property first (C# [GoField] uses properties)
                System.Reflection.PropertyInfo? prop = null;
                try
                {
                    prop = clrType.GetProperty(sel.Field.Name,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                }
                catch (System.NotSupportedException)
                {
                    // TypeBuilder doesn't support GetProperty
                }
                if (prop != null)
                {
                    var getter = prop.GetGetMethod();
                    if (getter != null)
                    {
                        _ctx.IL.Emit(clrType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, getter);
                        return;
                    }
                }

                // Try field
                var field = clrType.GetField(sel.Field.Name,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    _ctx.IL.Emit(OpCodes.Ldfld, field);
                    return;
                }
            }
        }

        private void EmitMethodCall(MethodCallExpression call)
        {
            // Special case: error.Error() — errors are strings in our runtime,
            // so Error() just returns the string itself via ToString()
            if (call.Method.Name == "Error" && call.Arguments.Count == 0
                && call.Receiver != null
                && (call.Receiver.Type.Name == "error"
                    || call.Receiver.Type is Symbols.InterfaceTypeSymbol errIface
                       && errIface.Methods.Count == 1 && errIface.Methods[0].Name == "Error"))
            {
                EmitExpression(call.Receiver);
                var toStringMethod = typeof(object).GetMethod("ToString")!;
                _ctx.IL.Emit(OpCodes.Callvirt, toStringMethod);
                return;
            }

            // Find method by identity first, then by name+receiver (handles symbol mismatch across analysis passes)
            if (!_ctx.Methods.TryGetValue(call.Method, out var method))
            {
                // Only search by name when the receiver type also matches
                var receiverTypeName = call.Method.ReceiverType?.Name;
                foreach (var kvp in _ctx.Methods)
                {
                    if (kvp.Key is MethodSymbol ms && ms.Name == call.Method.Name
                        && ms.Parameters.Count == call.Method.Parameters.Count
                        && ms.ReceiverType?.Name == receiverTypeName)
                    {
                        method = kvp.Value;
                        break;
                    }
                }
            }
            if (method != null)
            {
                // Pointer-receiver method on value: need writeback after call
                if (call.Method.IsPointerReceiver
                    && call.Receiver is AddressOfExpression addrOf
                    && addrOf.Operand is IdentifierExpression receiverId)
                {
                    var innerType = _ctx.Mapper.Map(addrOf.Operand.Type);
                    var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                    var ctor = _ctx.Definitions.GetConstructor(ptrType, new[] { innerType });
                    var valueField = _ctx.Definitions.GetField(ptrType, "Value");

                    // Create Ptr<T> from local value
                    EmitExpression(addrOf.Operand);
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                    var ptrLocal = _ctx.IL.DeclareLocal(ptrType);
                    _ctx.IL.Emit(OpCodes.Stloc, ptrLocal);

                    // Call method with Ptr as receiver
                    _ctx.IL.Emit(OpCodes.Ldloc, ptrLocal);
                    foreach (var arg in call.Arguments)
                        EmitExpression(arg);
                    _ctx.IL.Emit(OpCodes.Call, method.AsMethodInfo());

                    // Copy modified value back to local
                    _ctx.IL.Emit(OpCodes.Ldloc, ptrLocal);
                    _ctx.IL.Emit(OpCodes.Ldfld, valueField);
                    EmitStore(receiverId.Symbol);
                    return;
                }

                // Static method with receiver as first arg
                EmitExpression(call.Receiver);
                EmitMethodCallArguments(call);
                _ctx.IL.Emit(OpCodes.Call, method.AsMethodInfo());
                return;
            }

            // Check cached assemblies (precompiled packages)
            if (_ctx.CachedMethods.TryGetValue(call.Method, out var cachedMethod))
            {
                EmitExpression(call.Receiver);
                EmitMethodCallArguments(call);
                _ctx.IL.Emit(OpCodes.Call, cachedMethod);
                return;
            }

            // Interface method call: receiver is a wrapper, use callvirt
            // Unwrap named types to find interface
            var receiverType = call.Receiver.Type.Resolved();
            if (receiverType is InterfaceTypeSymbol ifaceType)
            {
                EmitExpression(call.Receiver);
                foreach (var arg in call.Arguments)
                    EmitExpression(arg);

                var ifaceClrType = _ctx.Mapper.Map(ifaceType);
                var paramTypes = new Type[call.Method.Parameters.Count];
                for (int i = 0; i < call.Method.Parameters.Count; i++)
                    paramTypes[i] = _ctx.Mapper.Map(call.Method.Parameters[i].Type);

                MethodInfo? ifaceMethod = _ctx.Definitions.GetMethod(ifaceClrType, call.Method.Name, paramTypes);

                if (ifaceMethod == null)
                {
                    ifaceMethod = _ctx.Definitions.GetMethod(ifaceClrType, call.Method.Name);
                }

                // Search parent interfaces
                if (ifaceMethod == null && ifaceClrType.IsInterface)
                {
                    try
                    {
                        foreach (var parentIface in ifaceClrType.GetInterfaces())
                        {
                            ifaceMethod = _ctx.Definitions.GetMethod(parentIface, call.Method.Name, paramTypes);
                            if (ifaceMethod != null)
                            {
                                break;
                            }
                        }
                    }
                    catch (NotSupportedException)
                    {
                    }
                }
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
                    // For generic types, verify the method exists on the generic definition
                    // before trying to resolve on the instantiation. This prevents creating
                    // fake proxy methods for non-existent methods (e.g., Ptr<T> doesn't have
                    // Load/Store — those are on the wrapped Go struct type).
                    bool methodExistsOnType = true;
                    if (receiverClrType.IsGenericType)
                    {
                        try
                        {
                            var genericDef = receiverClrType.GetGenericTypeDefinition();
                            methodExistsOnType = genericDef.GetMethod(call.Method.Name) != null;
                        }
                        catch (AmbiguousMatchException)
                        {
                            methodExistsOnType = true;
                        }
                    }

                    MethodInfo? clrMethod = null;
                    if (methodExistsOnType && call.Arguments.Count <= call.Method.Parameters.Count && !call.Method.IsVariadic)
                    {
                        var argTypes = new Type[call.Arguments.Count];
                        for (int i = 0; i < call.Arguments.Count; i++)
                        {
                            argTypes[i] = _ctx.Mapper.Map(call.Method.Parameters[i].Type);
                        }
                        clrMethod = _ctx.Definitions.GetMethod(receiverClrType, call.Method.Name, argTypes);
                    }

                    // Runtime types use [GoMethod(Name="...")] — C# name differs from Go name
                    if (clrMethod == null)
                    {
                        clrMethod = ResolveByGoMethodAttribute(receiverClrType, call.Method.Name, call.Arguments.Count);
                    }

                    // If not found, check for variadic (params array) method
                    if (clrMethod == null && methodExistsOnType)
                    {
                        foreach (var candidate in receiverClrType.GetMethods())
                        {
                            if (candidate.Name != call.Method.Name)
                            {
                                continue;
                            }
                            var candidateParams = candidate.GetParameters();
                            if (candidateParams.Length >= 1
                                && candidateParams[candidateParams.Length - 1].ParameterType.IsArray
                                && candidateParams[candidateParams.Length - 1].IsDefined(typeof(System.ParamArrayAttribute), false))
                            {
                                clrMethod = candidate;
                                break;
                            }
                        }

                        if (clrMethod != null)
                        {
                            // Variadic call: emit receiver, pack all args into an array
                            var methodParams = clrMethod.GetParameters();
                            var elemType = methodParams[methodParams.Length - 1].ParameterType.GetElementType()!;
                            int fixedParams = methodParams.Length - 1;

                            EmitExpression(call.Receiver);

                            // Emit fixed (non-variadic) arguments
                            for (int i = 0; i < fixedParams; i++)
                            {
                                EmitExpression(call.Arguments[i]);
                            }

                            // Pack remaining arguments into array
                            int varArgCount = call.Arguments.Count - fixedParams;
                            _ctx.IL.Emit(OpCodes.Ldc_I4, varArgCount);
                            _ctx.IL.Emit(OpCodes.Newarr, elemType);
                            for (int i = 0; i < varArgCount; i++)
                            {
                                _ctx.IL.Emit(OpCodes.Dup);
                                _ctx.IL.Emit(OpCodes.Ldc_I4, i);
                                EmitExpression(call.Arguments[fixedParams + i]);
                                var argClrType = _ctx.Mapper.Map(call.Arguments[fixedParams + i].Type);
                                if (argClrType.IsValueType && elemType == typeof(object))
                                {
                                    _ctx.IL.Emit(OpCodes.Box, argClrType);
                                }
                                _ctx.IL.Emit(OpCodes.Stelem_Ref);
                            }

                            _ctx.IL.Emit(OpCodes.Callvirt, clrMethod);
                            return;
                        }
                    }

                    if (clrMethod != null)
                    {
                        // Non-variadic: emit receiver + args + call
                        EmitExpression(call.Receiver);
                        var clrParams = clrMethod.GetParameters();
                        for (int i = 0; i < call.Arguments.Count; i++)
                        {
                            EmitExpression(call.Arguments[i]);
                            if (i < clrParams.Length && clrParams[i].ParameterType == typeof(object))
                            {
                                var argClrType = _ctx.Mapper.Map(call.Arguments[i].Type);
                                if (argClrType.IsValueType)
                                {
                                    _ctx.IL.Emit(OpCodes.Box, argClrType);
                                }
                            }
                        }
                        _ctx.IL.Emit(OpCodes.Callvirt, clrMethod);
                        return;
                    }
                }
            }

            // Value type method call (structs from runtime packages)
            {
                var receiverClrType = _ctx.Mapper.Map(call.Receiver.Type);
                if (receiverClrType != typeof(object))
                {
                    var methodParamTypes = new Type[call.Method.Parameters.Count];
                    for (int i = 0; i < methodParamTypes.Length; i++)
                    {
                        methodParamTypes[i] = _ctx.Mapper.Map(call.Method.Parameters[i].Type);
                    }
                    var clrMethod = _ctx.Definitions.GetMethod(receiverClrType, call.Method.Name, methodParamTypes);
                    if (clrMethod == null)
                    {
                        clrMethod = ResolveByGoMethodAttribute(receiverClrType, call.Method.Name, call.Method.Parameters.Count);
                    }
                    if (clrMethod != null)
                    {
                        if (receiverClrType.IsValueType)
                        {
                            EmitExpressionAddress(call.Receiver, receiverClrType);
                        }
                        else
                        {
                            EmitExpression(call.Receiver);
                        }
                        for (int i = 0; i < call.Arguments.Count; i++)
                        {
                            EmitExpression(call.Arguments[i]);
                        }
                        _ctx.IL.Emit(receiverClrType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, clrMethod);
                        return;
                    }
                }
            }

            // Go-source-compiled struct methods: stored as static methods TypeName_MethodName
            // on the linked package class. Try both unqualified and package-qualified keys.
            {
                var receiverResolved = call.Receiver.Type.Resolved();
                // Unwrap pointer types: *Scanner → Scanner
                if (receiverResolved is Symbols.PointerTypeSymbol receiverPtr && receiverPtr.ElementType != null)
                {
                    receiverResolved = receiverPtr.ElementType.Resolved();
                }
                var staticMethodName = receiverResolved.Name + "_" + call.Method.Name;
                MethodBuilder? linkedMethod = null;

                if (!_ctx.LinkedMethods.TryGetValue(staticMethodName, out linkedMethod))
                {
                    // Try package-qualified key: "bufio.Scanner_Scan"
                    var packagePath = receiverResolved.PackagePath;
                    if (packagePath != null)
                    {
                        var lastSlash = packagePath.LastIndexOf('/');
                        var shortPackageName = lastSlash >= 0 ? packagePath.Substring(lastSlash + 1) : packagePath;
                        _ctx.LinkedMethods.TryGetValue(shortPackageName + "." + staticMethodName, out linkedMethod);
                    }

                    // Search by suffix match across all linked methods
                    if (linkedMethod == null)
                    {
                        var suffix = "." + staticMethodName;
                        foreach (var (key, linkedCandidate) in _ctx.LinkedMethods)
                        {
                            if (key.EndsWith(suffix) || key == staticMethodName)
                            {
                                linkedMethod = linkedCandidate;
                                break;
                            }
                        }
                    }
                }

                if (linkedMethod != null)
                {
                    EmitExpression(call.Receiver);
                    EmitMethodCallArguments(call);
                    _ctx.IL.Emit(OpCodes.Call, linkedMethod);
                    return;
                }
            }

            // Cross-package method call during dependency emit: emit as static call
            // TypeName_MethodName(receiver, args...) on the package class.
            if (_ctx.IsDependencyEmit)
            {
                var crossReceiverType = call.Receiver.Type.Resolved();
                if (crossReceiverType is Symbols.PointerTypeSymbol crossPtr && crossPtr.ElementType != null)
                {
                    crossReceiverType = crossPtr.ElementType.Resolved();
                }
                var packagePath = crossReceiverType.PackagePath;
                if (packagePath != null)
                {
                    var staticName = crossReceiverType.Name + "_" + call.Method.Name;

                    // On real modules, resolve against linked methods
                    if (_ctx.Module is Builder.LiveModuleBuilder)
                    {
                        var lastSlashCross = packagePath.LastIndexOf('/');
                        var shortPkgCross = lastSlashCross >= 0 ? packagePath.Substring(lastSlashCross + 1) : packagePath;
                        MethodBuilder? linkedCrossMethod = null;
                        if (!_ctx.LinkedMethods.TryGetValue(shortPkgCross + "." + staticName, out linkedCrossMethod))
                        {
                            if (!_ctx.LinkedMethods.TryGetValue(staticName, out linkedCrossMethod))
                            {
                                var suffix = "." + staticName;
                                foreach (var (key, candidate) in _ctx.LinkedMethods)
                                {
                                    if (key.EndsWith(suffix))
                                    {
                                        linkedCrossMethod = candidate;
                                        break;
                                    }
                                }
                            }
                        }
                        if (linkedCrossMethod != null)
                        {
                            EmitExpression(call.Receiver);
                            foreach (var arg in call.Arguments)
                            {
                                EmitExpression(arg);
                            }
                            _ctx.IL.Emit(OpCodes.Call, linkedCrossMethod);
                            return;
                        }
                    }

                    // Archive serialization path: create proxy for NgoWriter.
                    // Only for NgoModuleBuilder — real modules can't use proxy methods.
                    if (_ctx.Module is Builder.LiveModuleBuilder)
                    {
                        // Fall through to IsDependencyEmit stub
                    }
                    else
                    {
                        var allParams = new Symbols.ParameterSymbol[1 + call.Method.Parameters.Count];
                        allParams[0] = new Symbols.ParameterSymbol("recv", call.Receiver.Type, 0);
                        for (int i = 0; i < call.Method.Parameters.Count; i++)
                        {
                            allParams[i + 1] = call.Method.Parameters[i];
                        }
                        var proxyFunc = new Symbols.FunctionSymbol(staticName, allParams,
                            call.Method.ReturnTypes, call.Method.IsVariadic, packagePath);
                        var proxyMethod = _ctx.GetCrossPackageMethod(proxyFunc, _ctx.Mapper);

                        EmitExpression(call.Receiver);
                        foreach (var arg in call.Arguments)
                        {
                            EmitExpression(arg);
                        }
                        _ctx.IL.Emit(OpCodes.Call, proxyMethod);
                        return;
                    }
                }
            }

            if (_ctx.IsDependencyEmit)
            {
                _ctx.IL.Emit(OpCodes.Pop); // receiver
                foreach (var arg in call.Arguments)
                {
                    _ctx.IL.Emit(OpCodes.Pop);
                }
                if (call.Method.ReturnType != BuiltinTypes.Void
                    && call.Method.ReturnTypes.Count > 0)
                {
                    var clrRetType = _ctx.Mapper.MapReturnType(call.Method.ReturnTypes);
                    if (clrRetType.IsValueType)
                    {
                        var tempLocal = _ctx.IL.DeclareLocal(clrRetType);
                        _ctx.IL.Emit(OpCodes.Ldloca, tempLocal);
                        _ctx.IL.Emit(OpCodes.Initobj, clrRetType);
                        _ctx.IL.Emit(OpCodes.Ldloc, tempLocal);
                    }
                    else
                    {
                        _ctx.IL.Emit(OpCodes.Ldnull);
                    }
                }
                return;
            }

            throw new NotSupportedException($"Cannot resolve method: {call.Method.Name}");
        }

        private void EmitCompositeLiteral(CompositeLiteralExpression lit)
        {
            // Unwrap named types to find underlying composite type
            var litType = lit.Type;
            var resolved = litType.Resolved();

            if (resolved is StructTypeSymbol structType)
            {
                EmitStructLiteral(lit, structType);
            }
            else if (litType is StructTypeSymbol directStruct)
            {
                EmitStructLiteral(lit, directStruct);
            }
            else if (resolved is SliceTypeSymbol sliceType)
            {
                EmitSliceLiteral(lit, sliceType);
            }
            else if (resolved is ArrayTypeSymbol arrayType)
            {
                EmitArrayLiteral(lit, arrayType);
            }
            else if (resolved is MapTypeSymbol mapType)
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
            if (clrType == null)
            {
                clrType = typeof(object);
            }
            var local = _ctx.IL.DeclareLocal(clrType);
            bool isReferenceType = !clrType.IsValueType;

            if (isReferenceType)
            {
                var ctor = _ctx.Definitions.GetConstructor(clrType, Type.EmptyTypes);
                if (ctor != null)
                {
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldnull);
                    _ctx.IL.Emit(OpCodes.Stloc, local);
                }
            }
            else
            {
                _ctx.IL.Emit(OpCodes.Ldloca, local);
                _ctx.IL.Emit(OpCodes.Initobj, clrType);
            }

            if (lit.Initializers != null)
            {
                foreach (var init in lit.Initializers)
                {
                    if (_ctx.StructFields.TryGetValue(init.Field, out var fb))
                    {
                        if (isReferenceType)
                        {
                            _ctx.IL.Emit(OpCodes.Ldloc, local);
                        }
                        else
                        {
                            _ctx.IL.Emit(OpCodes.Ldloca, local);
                        }
                        EmitExpression(init.Value);
                        // Wrap value types for interface-typed fields
                        var fieldType = init.Field.Type;
                        if (fieldType is Symbols.InterfaceTypeSymbol fieldIfaceType
                            && fieldIfaceType.Methods.Count > 0
                            && init.Value.Type.TypeKind != Symbols.TypeKind.Interface)
                        {
                            var fieldClrType = _ctx.Mapper.Map(fieldType);
                            EmitInterfaceWrapIfNeeded(init.Value.Type, fieldIfaceType, fieldClrType);
                        }
                        else
                        {
                            var valClrType = _ctx.Mapper.Map(init.Value.Type);
                            var fldClrType = fb.AsFieldInfo().FieldType;
                            if (valClrType.IsValueType && !fldClrType.IsValueType)
                            {
                                _ctx.IL.Emit(OpCodes.Box, valClrType);
                            }
                        }
                        _ctx.IL.Emit(OpCodes.Stfld, fb.AsFieldInfo());
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
                    // Wrap value types in interface wrappers when storing into interface slices
                    if (sliceType.ElementType is Symbols.InterfaceTypeSymbol sliceIfaceElem
                        && elements[i].Value.Type.TypeKind != Symbols.TypeKind.Interface)
                    {
                        EmitInterfaceWrapIfNeeded(elements[i].Value.Type, sliceIfaceElem, elemClrType);
                    }
                    else
                    {
                        var valClrType = _ctx.Mapper.Map(elements[i].Value.Type);
                        if (valClrType.IsValueType && !elemClrType.IsValueType)
                        {
                            _ctx.IL.Emit(OpCodes.Box, valClrType);
                        }
                        else if (valClrType != elemClrType && valClrType.IsPrimitive && elemClrType.IsPrimitive)
                        {
                            // Convert literal value to match element type (e.g. int64 → uint16 for []uint16{...})
                            EmitConvOpcode(sliceType.ElementType.TypeKind);
                        }
                    }
                    EmitStelem(elemClrType);
                }
            }

            // new Slice<T>(array)
            var sliceClrType = _ctx.Mapper.Map(sliceType);
            var ctor = _ctx.Definitions.GetConstructor(sliceClrType, new[] { elemClrType.MakeArrayType() });
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
                    var arrValClrType = _ctx.Mapper.Map(elements[i].Value.Type);
                    if (arrValClrType != elemClrType && arrValClrType.IsPrimitive && elemClrType.IsPrimitive)
                    {
                        EmitConvOpcode(arrayType.ElementType.TypeKind);
                    }
                    EmitStelem(elemClrType);
                }
            }
        }

        private void EmitMapLiteral(CompositeLiteralExpression lit, MapTypeSymbol mapType)
        {
            var mapClrType = _ctx.Mapper.Map(mapType);
            var ctor = _ctx.Definitions.GetConstructor(mapClrType, Type.EmptyTypes);
            _ctx.IL.Emit(OpCodes.Newobj, ctor);

            if (lit.Elements != null)
            {
                foreach (var elem in lit.Elements)
                {
                    _ctx.IL.Emit(OpCodes.Dup);
                    EmitExpression(elem.Key!);
                    EmitExpression(elem.Value);
                    var setter = _ctx.Definitions.GetPropertySetter(mapClrType, "Item");
                    _ctx.IL.Emit(OpCodes.Call, setter);
                }
            }
        }

        /// <summary>
        /// Detects &amp;slice[i] on value-type elements and sets up slice-element
        /// pointer tracking so that field access through this variable goes
        /// through the slice indexer (ref return) instead of a Ptr&lt;T&gt; copy.
        /// Returns true if the pattern matched and was emitted.
        /// </summary>
        private bool TryEmitSliceElementPointer(Symbol symbol, Expression initializer)
        {
            if (initializer is not AddressOfExpression addrOf)
            {
                return false;
            }

            if (addrOf.Operand is not IndexExpression idx)
            {
                return false;
            }

            var targetType = ResolveTypeParamConstraint(idx.Target.Type);
            var resolvedTarget = targetType.Resolved();
            while (resolvedTarget != null && resolvedTarget.GetType() == typeof(TypeSymbol)
                   && resolvedTarget.UnderlyingType != null)
            {
                resolvedTarget = resolvedTarget.UnderlyingType;
            }

            if (resolvedTarget is not SliceTypeSymbol && targetType.TypeKind != TypeKind.Slice)
            {
                return false;
            }

            var elemClrType = _ctx.Mapper.Map(addrOf.Operand.Type);
            if (elemClrType == null || !elemClrType.IsValueType)
            {
                return false;
            }

            var sliceSymbol = resolvedTarget is SliceTypeSymbol ? resolvedTarget : targetType;
            var sliceClrType = _ctx.Mapper.Map(sliceSymbol);

            // Store the slice into a hidden local
            var sliceLocal = _ctx.IL.DeclareLocal(sliceClrType);
            EmitExpression(idx.Target);
            _ctx.IL.Emit(OpCodes.Stloc, sliceLocal);

            // Store the index into a hidden local
            var indexLocal = _ctx.IL.DeclareLocal(typeof(int));
            EmitExpression(idx.Index);
            _ctx.IL.Emit(OpCodes.Conv_I4);
            _ctx.IL.Emit(OpCodes.Stloc, indexLocal);

            _ctx.SliceElementPointers[symbol] = new SliceElementPointer(
                sliceLocal, indexLocal, sliceClrType, elemClrType);
            return true;
        }

        /// <summary>
        /// Emits IL to get a managed reference (ref T) to the element tracked by
        /// a SliceElementPointer. The ref points into the slice's backing array.
        /// </summary>
        private void EmitSliceElementRef(SliceElementPointer sliceRef)
        {
            _ctx.IL.Emit(OpCodes.Ldloca, sliceRef.SliceLocal);
            _ctx.IL.Emit(OpCodes.Ldloc, sliceRef.IndexLocal);
            var indexerGetter = _ctx.Definitions.GetPropertyGetter(sliceRef.SliceClrType, "Item");
            _ctx.IL.Emit(OpCodes.Call, indexerGetter);
        }

        private void EmitAddressOf(AddressOfExpression addrOf)
        {
            // &x → new Ptr<T>(x) for value types, or just x for reference types
            var innerType = _ctx.Mapper.Map(addrOf.Operand.Type);
            if (innerType == null)
            {
                innerType = typeof(object);
            }

            // Reference types (classes, arrays) are already reference-semantics — no Ptr<T> wrapper needed
            if (!innerType.IsValueType && innerType is not TypeBuilder && innerType is not GenericTypeParameterBuilder)
            {
                EmitExpression(addrOf.Operand);
                return;
            }

            var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
            var ctor = _ctx.Definitions.GetConstructor(ptrType, new[] { innerType });

            EmitExpression(addrOf.Operand);
            _ctx.IL.Emit(OpCodes.Newobj, ctor);
        }

        private void EmitDeref(DerefExpression deref)
        {
            // Slice-element pointer: access through the slice indexer ref
            if (deref.Operand is IdentifierExpression derefId
                && _ctx.SliceElementPointers.TryGetValue(derefId.Symbol, out var sliceRef))
            {
                EmitSliceElementRef(sliceRef);
                _ctx.IL.Emit(OpCodes.Ldobj, sliceRef.ElementClrType);
                return;
            }

            // *p → p.Value
            EmitExpression(deref.Operand);
            var innerType = _ctx.Mapper.Map(deref.Type);
            if (innerType == null)
            {
                return;
            }
            // Reference types don't use Ptr<T> — pointer is the reference itself
            if (!innerType.IsValueType)
            {
                return;
            }
            var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
            var valueField = _ctx.Definitions.GetField(ptrType, "Value");
            _ctx.IL.Emit(OpCodes.Ldfld, valueField);
        }

        private void EmitIndex(IndexExpression idx)
        {
            var targetType = ResolveTypeParamConstraint(idx.Target.Type);

            // Unwrap named types to find the underlying slice/array/map
            var resolvedTarget = targetType;
            while (resolvedTarget != null && resolvedTarget.GetType() == typeof(TypeSymbol)
                   && resolvedTarget.UnderlyingType != null)
            {
                resolvedTarget = resolvedTarget.UnderlyingType;
            }

            if (resolvedTarget is SliceTypeSymbol || targetType.TypeKind == TypeKind.Slice)
            {
                // Use resolved (underlying) type for slice operations to access Slice<T> indexer
                var sliceSymbol = resolvedTarget is SliceTypeSymbol ? resolvedTarget : targetType;
                var sliceClrType = _ctx.Mapper.Map(sliceSymbol);
                EmitExpressionAddress(idx.Target, sliceClrType);
                EmitExpression(idx.Index);
                _ctx.IL.Emit(OpCodes.Conv_I4);
                var indexerGetter = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Item");
                _ctx.IL.Emit(OpCodes.Call, indexerGetter);
                // Slice<T> indexer returns ref T, load the value
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                _ctx.IL.Emit(OpCodes.Ldobj, elemClrType);
            }
            else if (resolvedTarget is ArrayTypeSymbol || targetType.TypeKind == TypeKind.Array)
            {
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                _ctx.IL.Emit(OpCodes.Conv_I4);
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                EmitLdelem(elemClrType);
            }
            else if (resolvedTarget is MapTypeSymbol || targetType.TypeKind == TypeKind.Map)
            {
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                var mapClrType = _ctx.Mapper.Map(targetType);
                if (idx.IsCommaOk)
                {
                    // val, ok := m[key] → Map.Get(key) returns (V, bool) tuple
                    var getMethod = _ctx.Definitions.GetMethod(mapClrType, "Get");
                    _ctx.IL.Emit(OpCodes.Call, getMethod);
                }
                else
                {
                    var getter = _ctx.Definitions.GetPropertyGetter(mapClrType, "Item");
                    _ctx.IL.Emit(OpCodes.Call, getter);
                }
            }
            else if (targetType.TypeKind == TypeKind.String || targetType.TypeKind == TypeKind.UntypedString)
            {
                // String byte indexing via GoString indexer (get_Item)
                EmitExpression(idx.Target);
                var tempStr = _ctx.IL.DeclareLocal(typeof(GoString));
                _ctx.IL.Emit(OpCodes.Stloc, tempStr);
                _ctx.IL.Emit(OpCodes.Ldloca, tempStr);
                EmitExpression(idx.Index);
                EmitLdconv(idx.Index.Type);
                var indexer = typeof(GoString).GetProperty("Item")!.GetGetMethod()!;
                _ctx.IL.Emit(OpCodes.Call, indexer);
            }
            else if (resolvedTarget is Symbols.TypeParameterSymbol || targetType is Symbols.TypeParameterSymbol)
            {
                var mappedTarget = _ctx.Mapper.Map(targetType);
                Type sliceClrType;
                sliceClrType = mappedTarget != typeof(object)
                    ? typeof(Slice<>).MakeGenericType(mappedTarget)
                    : typeof(Slice<object>);
                EmitExpressionAddress(idx.Target, sliceClrType);
                EmitExpression(idx.Index);
                _ctx.IL.Emit(OpCodes.Conv_I4);
                var indexerGetter = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Item");
                _ctx.IL.Emit(OpCodes.Call, indexerGetter);
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                if (elemClrType == typeof(object))
                {
                    _ctx.IL.Emit(OpCodes.Ldobj, typeof(object));
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldobj, elemClrType);
                }
            }
            else
            {
                throw new NotSupportedException($"Index on type {targetType.TypeKind} not supported");
            }
        }

        private void EmitSlice(SliceExpression slice)
        {
            var targetType = ResolveTypeParamConstraint(slice.Operand.Type);
            var resolvedTarget = targetType.Resolved();

            if (resolvedTarget is SliceTypeSymbol sliceType)
            {
                var sliceClrType = _ctx.Mapper.Map(targetType);
                EmitExpressionAddress(slice.Operand, sliceClrType);

                EmitExpression(slice.Low ?? MakeIntLiteral(0));
                _ctx.IL.Emit(OpCodes.Conv_I4);
                EmitExpression(slice.High ?? MakeIntLiteral(-1)); // sentinel for len
                _ctx.IL.Emit(OpCodes.Conv_I4);

                if (slice.Max != null)
                {
                    EmitExpression(slice.Max);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    var reslice3 = _ctx.Definitions.GetMethod(sliceClrType, "Reslice", new[] { typeof(int), typeof(int), typeof(int) });
                    _ctx.IL.Emit(OpCodes.Call, reslice3);
                }
                else
                {
                    var reslice2 = _ctx.Definitions.GetMethod(sliceClrType, "Reslice", new[] { typeof(int), typeof(int) });
                    _ctx.IL.Emit(OpCodes.Call, reslice2);
                }
            }
            else if (resolvedTarget is ArrayTypeSymbol arrayType)
            {
                // array[:] or array[low:high] — convert array to slice via Slice<T>
                var elemClrType = _ctx.Mapper.Map(arrayType.ElementType);
                var sliceClrType = typeof(Slice<>).MakeGenericType(elemClrType);
                EmitExpression(slice.Operand);

                if (slice.Low == null && slice.High == null)
                {
                    // array[:] — full slice from array
                    var ctor = _ctx.Definitions.GetConstructor(sliceClrType, new[] { elemClrType.MakeArrayType() });
                    _ctx.IL.Emit(OpCodes.Newobj, ctor);
                }
                else
                {
                    // array[low:high] — sub-slice
                    var low = slice.Low ?? MakeIntLiteral(0);
                    EmitExpression(low);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    // Compute length = high - low (or arrayLen - low)
                    if (slice.High != null)
                    {
                        EmitExpression(slice.High);
                        _ctx.IL.Emit(OpCodes.Conv_I4);
                    }
                    else
                    {
                        _ctx.IL.Emit(OpCodes.Ldc_I4, arrayType.Length);
                    }
                    // Stack: array, low, high → need array, offset, length
                    // length = high - low
                    var tempHigh = _ctx.IL.DeclareLocal(typeof(int));
                    var tempLow = _ctx.IL.DeclareLocal(typeof(int));
                    _ctx.IL.Emit(OpCodes.Stloc, tempHigh);
                    _ctx.IL.Emit(OpCodes.Stloc, tempLow);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempLow);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempHigh);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempLow);
                    _ctx.IL.Emit(OpCodes.Sub);
                    var ctor3 = _ctx.Definitions.GetConstructor(sliceClrType, new[] { elemClrType.MakeArrayType(), typeof(int), typeof(int) });
                    _ctx.IL.Emit(OpCodes.Newobj, ctor3);
                }
            }
            else if (targetType.TypeKind == TypeKind.String || targetType.TypeKind == TypeKind.UntypedString)
            {
                // GoString.Slice is an instance method — stash, then call via ldloca
                EmitExpression(slice.Operand);
                var tempStr = _ctx.IL.DeclareLocal(typeof(GoString));
                _ctx.IL.Emit(OpCodes.Stloc, tempStr);
                _ctx.IL.Emit(OpCodes.Ldloca, tempStr);
                EmitExpression(slice.Low ?? MakeIntLiteral(0));
                _ctx.IL.Emit(OpCodes.Conv_I4);
                EmitExpression(slice.High ?? MakeIntLiteral(-1));
                _ctx.IL.Emit(OpCodes.Conv_I4);
                var sliceMethod = typeof(GoString).GetMethod("Slice")!;
                _ctx.IL.Emit(OpCodes.Call, sliceMethod);
            }
            else if (targetType.TypeKind == TypeKind.Pointer)
            {
                // Pointer slicing (e.g., unsafe.Slice) — emit operand and discard,
                // return zero-length slice. Actual pointer slicing requires unsafe operations.
                EmitExpression(slice.Operand);
                _ctx.IL.Emit(OpCodes.Pop);
                if (slice.Low != null) { EmitExpression(slice.Low); _ctx.IL.Emit(OpCodes.Pop); }
                if (slice.High != null) { EmitExpression(slice.High); _ctx.IL.Emit(OpCodes.Pop); }
                var resultType = _ctx.Mapper.Map(slice.Type);
                if (resultType.IsValueType)
                {
                    var local = _ctx.IL.DeclareLocal(resultType);
                    _ctx.IL.Emit(OpCodes.Ldloca, local);
                    _ctx.IL.Emit(OpCodes.Initobj, resultType);
                    _ctx.IL.Emit(OpCodes.Ldloc, local);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldnull);
                }
            }
            else if (resolvedTarget is Symbols.TypeParameterSymbol || targetType is Symbols.TypeParameterSymbol)
            {
                var elemClrType = _ctx.Mapper.Map(resolvedTarget is Symbols.TypeParameterSymbol tp ? tp : targetType);
                var sliceClrType = typeof(Slice<>).MakeGenericType(elemClrType);
                EmitExpressionAddress(slice.Operand, sliceClrType);
                if (slice.Low != null)
                {
                    EmitExpression(slice.Low);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldc_I4_0);
                }
                _ctx.IL.Emit(OpCodes.Conv_I4);
                if (slice.High != null)
                {
                    EmitExpression(slice.High);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldarg_0);
                    var lenProp = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Len");
                    _ctx.IL.Emit(OpCodes.Call, lenProp);
                }
                _ctx.IL.Emit(OpCodes.Conv_I4);
                var sliceMethod = _ctx.Definitions.GetMethod(sliceClrType, "Reslice", new[] { typeof(int), typeof(int) });
                _ctx.IL.Emit(OpCodes.Call, sliceMethod);
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
                var ctor = _ctx.Definitions.GetConstructor(tupleType, new[] { targetType, typeof(bool) });
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

        private static TypeSymbol ResolveTypeParamConstraint(TypeSymbol type)
        {
            if (type is TypeParameterSymbol tp && tp.Constraint.TypeElements.Count > 0)
            {
                return tp.Constraint.TypeElements[0].Type;
            }
            return type;
        }

        private void EmitIndexAssignment(IndexExpression idx, Expression value)
        {
            var targetType = ResolveTypeParamConstraint(idx.Target.Type);

            if (targetType is SliceTypeSymbol)
            {
                var sliceClrType = _ctx.Mapper.Map(targetType);
                EmitExpressionAddress(idx.Target, sliceClrType);
                EmitExpression(idx.Index);
                _ctx.IL.Emit(OpCodes.Conv_I4);
                var indexerGetter = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Item");
                _ctx.IL.Emit(OpCodes.Call, indexerGetter);
                // Get ref, then store into it
                EmitExpression(value);
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                var sliceElemType = ((SliceTypeSymbol)targetType.Resolved()).ElementType;
                if (sliceElemType is Symbols.InterfaceTypeSymbol idxIfaceElem
                    && value.Type.TypeKind != Symbols.TypeKind.Interface)
                {
                    EmitInterfaceWrapIfNeeded(value.Type, idxIfaceElem, elemClrType);
                }
                else
                {
                    var valClrType = _ctx.Mapper.Map(value.Type);
                    if (valClrType.IsValueType && !elemClrType.IsValueType)
                    {
                        _ctx.IL.Emit(OpCodes.Box, valClrType);
                    }
                }
                _ctx.IL.Emit(OpCodes.Stobj, elemClrType);
            }
            else if (targetType is ArrayTypeSymbol)
            {
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                Builder.IFieldBuilder? writeFb = null;
                SelectorExpression? writeSel = idx.Target as SelectorExpression;
                if (writeSel?.Field?.Type is ArrayTypeSymbol)
                {
                    if (!_ctx.StructFields.TryGetValue(writeSel.Field, out writeFb))
                    {
                        foreach (var kvp in _ctx.StructFields)
                        {
                            if (kvp.Key.Name == writeSel.Field.Name
                                && kvp.Key.Type is ArrayTypeSymbol)
                            {
                                writeFb = kvp.Value;
                                break;
                            }
                        }
                    }
                }
                if (writeFb != null && !writeFb.AsFieldInfo().FieldType.IsArray)
                {
                    EmitInlineArrayFieldAddress(writeSel);
                    EmitExpression(idx.Index);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    EmitExpression(value);
                    var bufferType = writeFb.AsFieldInfo().FieldType;
                    _ctx.IL.Emit(OpCodes.Call,
                        typeof(BuiltIn).GetMethod("InlineArraySet")!
                            .MakeGenericMethod(bufferType, elemClrType));
                }
                else
                {
                    EmitExpression(idx.Target);
                    EmitExpression(idx.Index);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    EmitExpression(value);
                    EmitStelem(elemClrType);
                }
            }
            else if (targetType is MapTypeSymbol)
            {
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                EmitExpression(value);
                var mapClrType = _ctx.Mapper.Map(targetType);
                var setter = _ctx.Definitions.GetPropertySetter(mapClrType, "Item");
                _ctx.IL.Emit(OpCodes.Call, setter);
            }
        }

        private void EmitIndexAssignmentFromTemp(IndexExpression idx, LocalBuilder tempVal)
        {
            var targetType = ResolveTypeParamConstraint(idx.Target.Type);

            if (targetType is SliceTypeSymbol)
            {
                var sliceClrType = _ctx.Mapper.Map(targetType);
                EmitExpressionAddress(idx.Target, sliceClrType);
                EmitExpression(idx.Index);
                _ctx.IL.Emit(OpCodes.Conv_I4);
                var indexerGetter = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Item");
                _ctx.IL.Emit(OpCodes.Call, indexerGetter);
                _ctx.IL.Emit(OpCodes.Ldloc, tempVal);
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                _ctx.IL.Emit(OpCodes.Stobj, elemClrType);
            }
            else if (targetType is ArrayTypeSymbol)
            {
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                _ctx.IL.Emit(OpCodes.Conv_I4);
                _ctx.IL.Emit(OpCodes.Ldloc, tempVal);
                var elemClrType = _ctx.Mapper.Map(idx.Type);
                EmitStelem(elemClrType);
            }
            else if (targetType is MapTypeSymbol)
            {
                EmitExpression(idx.Target);
                EmitExpression(idx.Index);
                _ctx.IL.Emit(OpCodes.Ldloc, tempVal);
                var mapClrType = _ctx.Mapper.Map(targetType);
                var setter = _ctx.Definitions.GetPropertySetter(mapClrType, "Item");
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

        private List<(Symbol symbol, LocalBuilder ptrLocal, Type innerType)>? _pendingWritebacks;

        private void EmitMethodCallArguments(MethodCallExpression call)
        {
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                var arg = call.Arguments[i];
                if (arg.Type.TypeKind == TypeKind.UntypedNil && i < call.Method.Parameters.Count)
                {
                    var paramType = call.Method.Parameters[i].Type;
                    var paramClrType = _ctx.Mapper.Map(paramType);
                    if (paramClrType.IsValueType)
                    {
                        var tempLocal = _ctx.IL.DeclareLocal(paramClrType);
                        _ctx.IL.Emit(OpCodes.Ldloca, tempLocal);
                        _ctx.IL.Emit(OpCodes.Initobj, paramClrType);
                        _ctx.IL.Emit(OpCodes.Ldloc, tempLocal);
                    }
                    else if (paramType.TypeKind == TypeKind.Map)
                    {
                        var nilMethod = paramClrType.GetMethod("Nil")!;
                        _ctx.IL.Emit(OpCodes.Call, nilMethod);
                    }
                    else
                    {
                        EmitExpression(arg);
                    }
                }
                else
                {
                    EmitExpression(arg);
                }
            }
        }

        private void EmitCallArguments(CallExpression call)
        {
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                var arg = call.Arguments[i];

                // Detect &localVar pattern for pointer writeback
                if (arg is AddressOfExpression addrOfArg
                    && addrOfArg.Operand is IdentifierExpression idArg
                    && _ctx.Locals.TryGetValue(idArg.Symbol, out _))
                {
                    var innerType = _ctx.Mapper.Map(addrOfArg.Operand.Type);
                    if (innerType.IsValueType)
                    {
                        // Create Ptr<T>, store in temp for writeback after call
                        var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                        var ctor = _ctx.Definitions.GetConstructor(ptrType, new[] { innerType });
                        var ptrLocal = _ctx.IL.DeclareLocal(ptrType);

                        EmitExpression(addrOfArg.Operand);
                        _ctx.IL.Emit(OpCodes.Newobj, ctor);
                        _ctx.IL.Emit(OpCodes.Dup);
                        _ctx.IL.Emit(OpCodes.Stloc, ptrLocal);

                        _pendingWritebacks ??= new List<(Symbol, LocalBuilder, Type)>();
                        _pendingWritebacks.Add((idArg.Symbol, ptrLocal, innerType));
                        continue;
                    }
                }

                // Nil literal for value type parameters needs initobj instead of ldnull
                if (arg.Type.TypeKind == TypeKind.UntypedNil && i < call.Function.Parameters.Count)
                {
                    var paramType = call.Function.Parameters[i].Type;
                    var paramClrType = _ctx.Mapper.Map(paramType);
                    if (paramClrType.IsValueType)
                    {
                        var tempLocal = _ctx.IL.DeclareLocal(paramClrType);
                        _ctx.IL.Emit(OpCodes.Ldloca, tempLocal);
                        _ctx.IL.Emit(OpCodes.Initobj, paramClrType);
                        _ctx.IL.Emit(OpCodes.Ldloc, tempLocal);
                    }
                    else if (paramType.TypeKind == TypeKind.Map)
                    {
                        var nilMethod = paramClrType.GetMethod("Nil")!;
                        _ctx.IL.Emit(OpCodes.Call, nilMethod);
                    }
                    else
                    {
                        EmitExpression(arg);
                    }
                }
                else
                {
                    EmitExpression(arg);

                    // Check if argument needs interface wrapping
                    if (i < call.Function.Parameters.Count)
                    {
                        var paramType = call.Function.Parameters[i].Type;
                        var argType = arg.Type;
                        if (paramType is InterfaceTypeSymbol && argType.TypeKind != TypeKind.Interface)
                        {
                            var targetClrType = _ctx.Mapper.Map(paramType);
                            EmitInterfaceWrapIfNeeded(argType, paramType, targetClrType);
                        }
                    }
                }
            }
        }

        private void EmitPendingWritebacks()
        {
            if (_pendingWritebacks == null || _pendingWritebacks.Count == 0)
            {
                return;
            }

            foreach (var (symbol, ptrLocal, innerType) in _pendingWritebacks)
            {
                var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                var valueField = _ctx.Definitions.GetField(ptrType, "Value");

                _ctx.IL.Emit(OpCodes.Ldloc, ptrLocal);
                _ctx.IL.Emit(OpCodes.Ldfld, valueField);
                EmitStore(symbol);
            }

            _pendingWritebacks.Clear();
        }

        private void EmitGoStringToNetString()
        {
            var tempLocal = _ctx.IL.DeclareLocal(typeof(GoString));
            _ctx.IL.Emit(OpCodes.Stloc, tempLocal);
            _ctx.IL.Emit(OpCodes.Ldloca, tempLocal);
            _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("ToNetString")!);
        }

        private void EmitRuntimeCallArguments(CallExpression call, ParameterInfo[] runtimeParams)
        {
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                EmitExpression(call.Arguments[i]);
                if (i < runtimeParams.Length
                    && runtimeParams[i].ParameterType == typeof(string)
                    && _ctx.Mapper.Map(call.Arguments[i].Type) == typeof(GoString))
                {
                    EmitGoStringToNetString();
                }
            }
        }

        internal void EmitInterfaceWrapIfNeeded(TypeSymbol sourceType, TypeSymbol targetType, Type targetClrType)
        {
            // Named interface with methods: generate wrapper
            if (targetType is InterfaceTypeSymbol ifaceType && ifaceType.Methods.Count > 0
                && sourceType.TypeKind != TypeKind.Interface)
            {
                _ctx.DeclEmitter!.GenerateWrapper(sourceType, ifaceType);
                if (_ctx.WrapperTypes.TryGetValue(new WrapperTypeKey(sourceType, ifaceType), out var wrapper))
                {
                    _ctx.IL.Emit(OpCodes.Newobj, wrapper.Constructor);
                }
                else
                {
                    var srcClrType = _ctx.Mapper.Map(sourceType);
                    if (srcClrType.IsValueType)
                    {
                        _ctx.IL.Emit(OpCodes.Box, srcClrType);
                    }
                }
                return;
            }

            // Empty interface or other ref-type target: box value types
            var sourceClrType = _ctx.Mapper.Map(sourceType);
            if (sourceClrType.IsValueType && !targetClrType.IsValueType)
                _ctx.IL.Emit(OpCodes.Box, sourceClrType);
        }

        internal void EmitLoad(Symbol symbol)
        {
            // Slice-element pointer: create a Ptr<T> wrapping the current element value.
            // This is used when the pointer itself is needed as a value (e.g., function arg).
            if (_ctx.SliceElementPointers.TryGetValue(symbol, out var sliceRef))
            {
                var ptrType = typeof(Ptr<>).MakeGenericType(sliceRef.ElementClrType);
                var ctor = _ctx.Definitions.GetConstructor(ptrType, new[] { sliceRef.ElementClrType });
                EmitSliceElementRef(sliceRef);
                _ctx.IL.Emit(OpCodes.Ldobj, sliceRef.ElementClrType);
                _ctx.IL.Emit(OpCodes.Newobj, ctor);
                return;
            }

            if (_ctx.Locals.TryGetValue(symbol, out var local))
            {
                _ctx.IL.Emit(OpCodes.Ldloc, local);
                if ((_ctx.CapturedSymbols.Contains(symbol) || IsCaptured(symbol))
                    && local.LocalType.IsGenericType
                    && local.LocalType.GetGenericTypeDefinition() == typeof(Box<>))
                {
                    _ctx.IL.Emit(OpCodes.Ldfld, _ctx.Definitions.GetField(local.LocalType, "Value"));
                }
                return;
            }

            if (_ctx.Parameters.TryGetValue(symbol, out var paramIndex))
            {
                _ctx.IL.Emit(OpCodes.Ldarg, paramIndex);
                return;
            }

            // Fallback: look up parameter by name (handles symbol identity mismatches
            // across re-analysis passes in EmitDependencyFromSource)
            if (symbol.Kind == SymbolKind.Parameter || symbol is ParameterSymbol)
            {
                foreach (var kvp in _ctx.Parameters)
                {
                    if (kvp.Key.Name == symbol.Name)
                    {
                        _ctx.IL.Emit(OpCodes.Ldarg, kvp.Value);
                        return;
                    }
                }
            }

            if (_ctx.PackageFields.TryGetValue(symbol, out var field))
            {
                _ctx.IL.Emit(OpCodes.Ldsfld, field.AsFieldInfo());
                return;
            }

            if (symbol is ConstantSymbol constant)
            {
                EmitConstantValue(constant);
                return;
            }

            if (symbol is PackageVarSymbol pkgVar && pkgVar.RuntimeType != null)
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

            if (symbol is FunctionSymbol funcSym)
            {
                MethodInfo? clrMethod = null;

                if (_ctx.Methods.TryGetValue(funcSym, out var funcMethod))
                {
                    clrMethod = funcMethod.AsMethodInfo();
                }
                else if (_ctx.CachedMethods.TryGetValue(funcSym, out var cachedMethod))
                {
                    clrMethod = cachedMethod;
                }
                else if (!string.IsNullOrEmpty(funcSym.PackageName))
                {
                    clrMethod = ResolveRuntimeFunction(funcSym.PackageName, funcSym.Name, null!);
                }

                if (clrMethod != null)
                {
                    var paramTypes = new List<TypeSymbol>();
                    foreach (var p in funcSym.Parameters)
                    {
                        paramTypes.Add(p.Type);
                    }
                    var funcType = new FunctionTypeSymbol(paramTypes, funcSym.ReturnTypes);
                    var delegateType = _ctx.Mapper.Map(funcType);
                    var delegateCtor = _ctx.Definitions.GetConstructor(delegateType, new[] { typeof(object), typeof(IntPtr) });
                    _ctx.IL.Emit(OpCodes.Ldnull);
                    _ctx.IL.Emit(OpCodes.Ldftn, clrMethod);
                    _ctx.IL.Emit(OpCodes.Newobj, delegateCtor);
                    return;
                }
            }

            // Cross-package variable: find in linked field builders
            if (symbol is PackageVarSymbol crossPkgVar)
            {
                foreach (var (fieldKey, fieldBuilder) in _ctx.LinkedFields)
                {
                    if (fieldKey.EndsWith("." + crossPkgVar.Name))
                    {
                        _ctx.IL.Emit(OpCodes.Ldsfld, fieldBuilder);
                        return;
                    }
                }
            }

            if (_ctx.IsDependencyEmit)
            {
                Type symbolClrType = typeof(object);
                if (symbol is Symbols.LocalSymbol ls)
                {
                    symbolClrType = _ctx.Mapper.Map(ls.Type);
                }
                else if (symbol is Symbols.PackageVarSymbol pv)
                {
                    symbolClrType = _ctx.Mapper.Map(pv.Type);
                }
                if (symbolClrType.IsValueType)
                {
                    var tempLocal = _ctx.IL.DeclareLocal(symbolClrType);
                    _ctx.IL.Emit(OpCodes.Ldloca, tempLocal);
                    _ctx.IL.Emit(OpCodes.Initobj, symbolClrType);
                    _ctx.IL.Emit(OpCodes.Ldloc, tempLocal);
                }
                else
                {
                    _ctx.IL.Emit(OpCodes.Ldnull);
                }
                return;
            }

            throw new NotSupportedException($"Cannot load symbol: {symbol.Name} ({symbol.Kind}, type={symbol.GetType().Name}, params={_ctx.Parameters.Count}, locals={_ctx.Locals.Count})");
        }

        internal void EmitStore(Symbol symbol)
        {
            if (_ctx.Locals.TryGetValue(symbol, out var local))
            {
                bool isBoxed = _ctx.CapturedSymbols.Contains(symbol) || IsCaptured(symbol);
                if (isBoxed && local.LocalType.IsGenericType
                    && local.LocalType.GetGenericTypeDefinition() == typeof(Box<>))
                {
                    var valueField = _ctx.Definitions.GetField(local.LocalType, "Value");
                    var elementType = local.LocalType.GetGenericArguments()[0];
                    var temp = _ctx.IL.DeclareLocal(elementType);
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

            if (TryResolveParameterIndex(symbol, out var paramIndex))
            {
                _ctx.IL.Emit(OpCodes.Starg, paramIndex);
                return;
            }

            // Name-based local fallback for symbol identity mismatches
            foreach (var kvp in _ctx.Locals)
            {
                if (kvp.Key.Name == symbol.Name && kvp.Key.Kind == symbol.Kind)
                {
                    _ctx.IL.Emit(OpCodes.Stloc, kvp.Value);
                    return;
                }
            }

            if (_ctx.PackageFields.TryGetValue(symbol, out var field))
            {
                _ctx.IL.Emit(OpCodes.Stsfld, field.AsFieldInfo());
                return;
            }

            if (_ctx.IsDependencyEmit)
            {
                _ctx.IL.Emit(OpCodes.Pop);
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
                case TypeKind.Uint:
                case TypeKind.Uint64:
                case TypeKind.Uintptr:
                case TypeKind.UntypedInt:
                    _ctx.IL.Emit(OpCodes.Ldc_I8, Convert.ToInt64(constant.Value));
                    break;
                case TypeKind.Int32:
                case TypeKind.UntypedRune:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, Convert.ToInt32(constant.Value));
                    break;
                case TypeKind.Uint32:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, unchecked((int)Convert.ToUInt32(constant.Value)));
                    break;
                case TypeKind.Int8:
                case TypeKind.Uint8:
                case TypeKind.Int16:
                case TypeKind.Uint16:
                    _ctx.IL.Emit(OpCodes.Ldc_I4, Convert.ToInt32(constant.Value));
                    break;
                case TypeKind.String:
                case TypeKind.UntypedString:
                    _ctx.IL.Emit(OpCodes.Ldstr, (string)constant.Value);
                    _ctx.IL.Emit(OpCodes.Call, typeof(GoString).GetMethod("FromNetString")!);
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
                case TypeKind.Uint:
                case TypeKind.Uint64:
                    _ctx.IL.Emit(OpCodes.Ldc_I8, (long)value);
                    break;
                case TypeKind.Int32:
                case TypeKind.Uint32:
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
                case TypeKind.Int64:
                case TypeKind.UntypedInt: _ctx.IL.Emit(OpCodes.Conv_I8); break;
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
                // Slice-element pointer: get ref directly into the slice element
                if (_ctx.SliceElementPointers.TryGetValue(id.Symbol, out var sliceRef))
                {
                    EmitSliceElementRef(sliceRef);
                    return;
                }

                // Pointer type: load the Ptr<T> and get address of the Value field
                if (id.Type is PointerTypeSymbol ptrSym)
                {
                    var elemType = _ctx.Mapper.Map(ptrSym.ElementType);
                    if (elemType != null && elemType.IsValueType)
                    {
                        EmitExpression(id);
                        var ptrType = typeof(Ptr<>).MakeGenericType(elemType);
                        _ctx.IL.Emit(OpCodes.Ldflda, _ctx.Definitions.GetField(ptrType, "Value"));
                        return;
                    }
                }

                if (_ctx.Locals.TryGetValue(id.Symbol, out var local))
                {
                    if (_ctx.CapturedSymbols.Contains(id.Symbol))
                    {
                        // Captured variable: local is Box<T>, need address of Box<T>.Value
                        _ctx.IL.Emit(OpCodes.Ldloc, local);
                        _ctx.IL.Emit(OpCodes.Ldflda, _ctx.Definitions.GetField(local.LocalType, "Value"));
                        return;
                    }
                    // If the local is Ptr<T> but the semantic type is the struct (auto-deref),
                    // load the Ptr and get address of the Value field
                    if (local.LocalType.IsGenericType
                        && local.LocalType.GetGenericTypeDefinition() == typeof(Ptr<>))
                    {
                        _ctx.IL.Emit(OpCodes.Ldloc, local);
                        _ctx.IL.Emit(OpCodes.Ldflda, _ctx.Definitions.GetField(local.LocalType, "Value"));
                        return;
                    }
                    _ctx.IL.Emit(OpCodes.Ldloca, local);
                    return;
                }

                if (_ctx.Parameters.TryGetValue(id.Symbol, out var pIdx))
                {
                    var paramSymType = id.Symbol is Symbols.ParameterSymbol paramSym ? paramSym.Type : null;
                    if (paramSymType is PointerTypeSymbol ptrParamSym)
                    {
                        var elemClrType = _ctx.Mapper.Map(ptrParamSym.ElementType);
                        if (elemClrType != null && elemClrType.IsValueType)
                        {
                            var ptrType = typeof(Ptr<>).MakeGenericType(elemClrType);
                            _ctx.IL.Emit(OpCodes.Ldarg, pIdx);
                            _ctx.IL.Emit(OpCodes.Ldflda, _ctx.Definitions.GetField(ptrType, "Value"));
                            return;
                        }
                    }
                    _ctx.IL.Emit(OpCodes.Ldarga, pIdx);
                    return;
                }
            }

            // Slice/array index: sl[i] → ref to element (so sl[i].Field = val works)
            if (target is IndexExpression indexTarget)
            {
                var resolvedTargetType = ResolveTypeParamConstraint(indexTarget.Target.Type.Resolved());
                if (resolvedTargetType is SliceTypeSymbol)
                {
                    var sliceClrType = _ctx.Mapper.Map(resolvedTargetType);
                    EmitExpressionAddress(indexTarget.Target, sliceClrType);
                    EmitExpression(indexTarget.Index);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    var indexerGetter = _ctx.Definitions.GetPropertyGetter(sliceClrType, "Item");
                    _ctx.IL.Emit(OpCodes.Call, indexerGetter);
                    return;
                }
                if (resolvedTargetType is ArrayTypeSymbol)
                {
                    var elemClrType = _ctx.Mapper.Map(indexTarget.Type);
                    EmitExpression(indexTarget.Target);
                    EmitExpression(indexTarget.Index);
                    _ctx.IL.Emit(OpCodes.Conv_I4);
                    _ctx.IL.Emit(OpCodes.Ldelema, elemClrType);
                    return;
                }
                // For map or other types, fall through to value load
            }

            // Deref pointer: *p → address of Ptr<T>.Value (for mutation through pointers)
            if (target is DerefExpression deref)
            {
                // Slice-element pointer: get ref directly into the slice's backing array
                if (deref.Operand is IdentifierExpression derefId
                    && _ctx.SliceElementPointers.TryGetValue(derefId.Symbol, out var sliceRef))
                {
                    EmitSliceElementRef(sliceRef);
                    return;
                }

                EmitExpression(deref.Operand);
                var innerType = _ctx.Mapper.Map(deref.Type);
                if (innerType == null || innerType == typeof(object))
                {
                    // Can't determine pointer element type — skip store
                    return;
                }
                var ptrType = typeof(Ptr<>).MakeGenericType(innerType);
                var valueField = _ctx.Definitions.GetField(ptrType, "Value");
                _ctx.IL.Emit(OpCodes.Ldflda, valueField);
                return;
            }

            // Last resort for identifiers: scan Parameters by name (symbol identity may differ
            // between the AST and what was registered in ctx.Parameters during archive creation)
            if (target is IdentifierExpression fallbackId)
            {
                foreach (var (paramSym, paramIdx) in _ctx.Parameters)
                {
                    if (paramSym.Name == fallbackId.Symbol.Name && paramSym.Kind == fallbackId.Symbol.Kind)
                    {
                        if (paramSym is Symbols.ParameterSymbol ps && ps.Type is PointerTypeSymbol fallbackPtr)
                        {
                            var elemClrType = _ctx.Mapper.Map(fallbackPtr.ElementType);
                            if (elemClrType != null && elemClrType.IsValueType)
                            {
                                var ptrType = typeof(Ptr<>).MakeGenericType(elemClrType);
                                _ctx.IL.Emit(OpCodes.Ldarg, paramIdx);
                                _ctx.IL.Emit(OpCodes.Ldflda, _ctx.Definitions.GetField(ptrType, "Value"));
                                return;
                            }
                        }
                        _ctx.IL.Emit(OpCodes.Ldarga, paramIdx);
                        return;
                    }
                }
            }

            // Nested struct field: s.Field where Field is a value type struct.
            // We need Ldflda to get the address of the field for subsequent Stfld.
            if (target is SelectorExpression selectorTarget && selectorTarget.Field != null)
            {
                if (_ctx.StructFields.TryGetValue(selectorTarget.Field, out var selectorFb))
                {
                    var fieldInfo = selectorFb.AsFieldInfo();
                    if (fieldInfo.FieldType.IsValueType)
                    {
                        EmitAddressForStore(selectorTarget.Target);
                        _ctx.IL.Emit(OpCodes.Ldflda, fieldInfo);
                        return;
                    }
                }
                // For reference type fields, loading the value is sufficient
                EmitExpression(target);
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
                        // Captured variable: local is Box<T>, get address of .Value field.
                        // Use the mapped element type to construct the correct Box<T> type,
                        // because NgoWriter's DeclareLocal may have substituted the local
                        // type to typeof(object) for TypeDelegator-based types.
                        var elemType = id.Symbol is Symbols.LocalSymbol localSym
                            ? _ctx.Mapper.Map(localSym.Type)
                            : _ctx.Mapper.Map(id.Type);
                        var boxType = typeof(Box<>).MakeGenericType(elemType);
                        _ctx.IL.Emit(OpCodes.Ldloc, local);
                        _ctx.IL.Emit(OpCodes.Ldflda, _ctx.Definitions.GetField(boxType, "Value"));
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
            if (elemType == typeof(int) || elemType == typeof(uint)) _ctx.IL.Emit(OpCodes.Stelem_I4);
            else if (elemType == typeof(long) || elemType == typeof(ulong)) _ctx.IL.Emit(OpCodes.Stelem_I8);
            else if (elemType == typeof(float)) _ctx.IL.Emit(OpCodes.Stelem_R4);
            else if (elemType == typeof(double)) _ctx.IL.Emit(OpCodes.Stelem_R8);
            else if (elemType == typeof(byte) || elemType == typeof(sbyte)) _ctx.IL.Emit(OpCodes.Stelem_I1);
            else if (elemType == typeof(short) || elemType == typeof(ushort)) _ctx.IL.Emit(OpCodes.Stelem_I2);
            else if (!elemType.IsValueType) _ctx.IL.Emit(OpCodes.Stelem_Ref);
            else _ctx.IL.Emit(OpCodes.Stelem, elemType);
        }

        private void EmitLdelem(Type elemType)
        {
            if (elemType == typeof(int) || elemType == typeof(uint)) _ctx.IL.Emit(OpCodes.Ldelem_I4);
            else if (elemType == typeof(long) || elemType == typeof(ulong)) _ctx.IL.Emit(OpCodes.Ldelem_I8);
            else if (elemType == typeof(float)) _ctx.IL.Emit(OpCodes.Ldelem_R4);
            else if (elemType == typeof(double)) _ctx.IL.Emit(OpCodes.Ldelem_R8);
            else if (elemType == typeof(byte) || elemType == typeof(sbyte)) _ctx.IL.Emit(OpCodes.Ldelem_U1);
            else if (elemType == typeof(short) || elemType == typeof(ushort)) _ctx.IL.Emit(OpCodes.Ldelem_I2);
            else if (!elemType.IsValueType) _ctx.IL.Emit(OpCodes.Ldelem_Ref);
            else _ctx.IL.Emit(OpCodes.Ldelem, elemType);
        }

        internal void EmitInlineArrayFieldAddress(SelectorExpression sel)
        {
            var structType = sel.Target!.Type!.Resolved();
            bool isPointer = structType is PointerTypeSymbol;
            // Go auto-dereferences pointers in selectors: d.d where d is *decimal
            // has sel.Target.Type resolved to decimal. Check the variable's declared type.
            if (!isPointer && sel.Target is DerefExpression deref)
            {
                isPointer = true;
                structType = deref.Operand.Type ?? structType;
            }
            if (isPointer)
            {
                var ptrExpr = sel.Target is DerefExpression de ? de.Operand : sel.Target;
                EmitExpression(ptrExpr);
                var elemClrType = _ctx.Mapper.Map(((PointerTypeSymbol)structType).ElementType!);
                var ptrType = typeof(Ptr<>).MakeGenericType(elemClrType);
                FieldInfo valueField;
                if (EmitContext.HasTypeBuilderArgs(ptrType))
                {
                    valueField = System.Reflection.Emit.TypeBuilder.GetField(
                        ptrType, typeof(Ptr<>).GetField("Value")!);
                }
                else
                {
                    valueField = ptrType.GetField("Value")!;
                }
                _ctx.IL.Emit(OpCodes.Ldflda, valueField);
            }
            else
            {
                EmitExpressionAddress(sel.Target, _ctx.Mapper.Map(structType));
            }
            _ctx.IL.Emit(OpCodes.Ldflda, _ctx.StructFields[sel.Field!].AsFieldInfo());
        }

        internal void EmitInlineArrayAsArray(SelectorExpression sel, ArrayTypeSymbol arrayType, Type elemClrType)
        {
            EmitInlineArrayFieldAddress(sel);
            _ctx.IL.Emit(OpCodes.Ldc_I4, arrayType.Length);
            var bufferType = _ctx.StructFields[sel.Field!].AsFieldInfo().FieldType;
            var spanMethod = typeof(BuiltIn).GetMethod("InlineArrayAsSpan")!
                .MakeGenericMethod(bufferType, elemClrType);
            _ctx.IL.Emit(OpCodes.Call, spanMethod);
            var spanType = typeof(Span<>).MakeGenericType(elemClrType);
            var spanLocal = _ctx.IL.DeclareLocal(spanType);
            _ctx.IL.Emit(OpCodes.Stloc, spanLocal);
            _ctx.IL.Emit(OpCodes.Ldloca, spanLocal);
            MethodInfo toArrayMethod;
            if (EmitContext.HasTypeBuilderArgs(spanType))
            {
                toArrayMethod = System.Reflection.Emit.TypeBuilder.GetMethod(
                    spanType, typeof(Span<>).GetMethod("ToArray")!);
            }
            else
            {
                toArrayMethod = spanType.GetMethod("ToArray")!;
            }
            _ctx.IL.Emit(OpCodes.Call, toArrayMethod);
        }

        private void EmitLdconv(TypeSymbol? type)
        {
            if (type == null)
            {
                return;
            }

            // Convert 64-bit index types to int32 for array/string indexing
            var kind = type.TypeKind;
            if (kind == TypeKind.Int || kind == TypeKind.UntypedInt
                || kind == TypeKind.Int64 || kind == TypeKind.Uint
                || kind == TypeKind.Uint64 || kind == TypeKind.Uintptr)
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
                var uniqueName = _ctx.QualifyName(structType.Name + "_" + structType.GetHashCode().ToString("X8"));
                var typeBuilder = _ctx.Module.DefineType(
                    uniqueName,
                    TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
                    typeof(ValueType));
                _ctx.Definitions.RegisterType(uniqueName, typeBuilder);

                foreach (var field in structType.Fields)
                {
                    var fieldType = _ctx.Mapper.Map(field.Type);
                    var fb = typeBuilder.DefineField(field.Name, fieldType, FieldAttributes.Public);
                    _ctx.StructFields[field] = fb;
                    _ctx.Definitions.RegisterField(uniqueName, field.Name, fb);
                }

                var runtimeType = typeBuilder.CreateType()!;
                _ctx.Mapper.Register(structType, runtimeType);
                _ctx.StructTypes[structType] = typeBuilder;
                _ctx.FinalizedTypes.Add(structType);
            }
            else if (typeDecl.Symbol is InterfaceTypeSymbol ifaceType)
            {
                var ifaceTypeName = ifaceType.Name;
                var typeBuilder = _ctx.Module.DefineType(
                    ifaceTypeName,
                    TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
                    null!, Type.EmptyTypes);
                _ctx.Definitions.RegisterType(ifaceTypeName, typeBuilder);

                foreach (var method in ifaceType.Methods)
                {
                    var paramTypes = new Type[method.Parameters.Count];
                    for (int i = 0; i < method.Parameters.Count; i++)
                    {
                        paramTypes[i] = _ctx.Mapper.Map(method.Parameters[i].Type);
                    }
                    var returnType = _ctx.Mapper.Map(method.ReturnType);
                    var ifaceMethod = typeBuilder.DefineMethod(method.Name,
                        MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
                        returnType, paramTypes);
                    _ctx.Definitions.RegisterMethod(ifaceTypeName, method.Name, paramTypes, ifaceMethod);
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

// -----------------------------------------------------------------------
// <copyright file="CallResolver.cs" company="Ziad">
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
using Ngo.Compiler.Ast;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    public sealed class CallResolver
    {
        private readonly AnalysisContext _context;
        private readonly TypeResolver _typeResolver;
        private readonly BuiltinResolver _builtinResolver;
        private Func<ExpressionSyntax, Expression> _resolveExpression;

        public CallResolver(AnalysisContext context, TypeResolver typeResolver, BuiltinResolver builtinResolver)
        {
            _context = context;
            _typeResolver = typeResolver;
            _builtinResolver = builtinResolver;
            _resolveExpression = _ => throw new InvalidOperationException(
                "Expression resolver has not been set. Call SetExpressionResolver first.");
        }

        public void SetExpressionResolver(Func<ExpressionSyntax, Expression> resolveExpression)
        {
            _resolveExpression = resolveExpression;
        }

        public Expression ResolveCallExpression(CallExpressionSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            // Explicit type arguments: Max[int](3, 5) or Pair[int, string](1, "a")
            if (syntax.Function is TypeArgumentListSyntax typeArgListSyntax)
            {
                return ResolveGenericCallWithExplicitArgs(typeArgListSyntax, syntax, span);
            }

            if (syntax.Function is IndexExpressionSyntax indexSyntax)
            {
                var result = TryResolveGenericCallWithSingleArg(indexSyntax, syntax, span);
                if (result != null)
                {
                    return result;
                }
            }

            // Check if this is a type conversion or builtin function
            if (syntax.Function is IdentifierNameSyntax idSyntax)
            {
                var name = idSyntax.Identifier.Text;

                // Type conversion: int(x), float64(x), etc.
                var targetType = BuiltinTypes.Resolve(name);
                if (targetType != null && syntax.Arguments.Count == 1)
                {
                    return ResolveConversion(syntax.Arguments[0], targetType, span);
                }

                // User-defined type conversion: MyType(x)
                var typeSym = _context.Scope.Lookup(name);
                if (typeSym is TypeSymbol userType && syntax.Arguments.Count == 1)
                {
                    return ResolveConversion(syntax.Arguments[0], userType, span);
                }

                // Check if a local variable/parameter/function shadows a builtin.
                // In Go, any identifier can shadow a builtin (close, panic, new, etc.).
                if (name is "clear" or "min" or "max" or "close" or "panic"
                    or "recover" or "new" or "copy" or "delete" or "len" or "cap"
                    or "append" or "make" or "print" or "println"
                    or "complex" or "real" or "imag")
                {
                    var scopeSym = _context.Scope.Lookup(name);
                    if (scopeSym is FunctionSymbol || scopeSym is LocalSymbol
                        || scopeSym is ParameterSymbol)
                        goto resolveAsFunction;
                }

                // Builtin functions
                switch (name)
                {
                    case "len": return _builtinResolver.ResolveLen(syntax, span);
                    case "cap": return _builtinResolver.ResolveCap(syntax, span);
                    case "append": return _builtinResolver.ResolveAppend(syntax, span);
                    case "make": return _builtinResolver.ResolveMake(syntax, span);
                    case "delete": return _builtinResolver.ResolveDelete(syntax, span);
                    case "println":
                    case "print": return _builtinResolver.ResolvePrint(syntax, span);
                    case "complex": return _builtinResolver.ResolveComplex(syntax, span);
                    case "real": return _builtinResolver.ResolveReal(syntax, span);
                    case "imag": return _builtinResolver.ResolveImag(syntax, span);
                    case "min": return _builtinResolver.ResolveMin(syntax, span);
                    case "max": return _builtinResolver.ResolveMax(syntax, span);
                    case "clear": return _builtinResolver.ResolveClear(syntax, span);
                    case "close":
                    case "panic":
                    case "recover":
                    case "new":
                    case "copy":
                        return ResolveSimpleBuiltin(name, syntax, span);
                }

                resolveAsFunction:;
            }

            // Composite type conversion: []byte(s), []int(s), etc.
            if (syntax.Function is SliceTypeSyntax || syntax.Function is ArrayTypeSyntax
                || syntax.Function is MapTypeSyntax || syntax.Function is ChannelTypeSyntax
                || syntax.Function is PointerTypeSyntax || syntax.Function is InterfaceTypeSyntax
                || syntax.Function is StructTypeSyntax)
            {
                if (syntax.Arguments.Count == 1)
                {
                    var targetType = _typeResolver.ResolveType(syntax.Function);
                    return ResolveConversion(syntax.Arguments[0], targetType, span);
                }
            }

            // Parenthesized type conversion: (*Type)(value), ([]byte)(value), etc.
            // Or parenthesized dereference call: (*ptr)(args)
            if (syntax.Function is ParenthesizedExpressionSyntax parenSyntax && syntax.Arguments.Count == 1)
            {
                // Speculatively try type resolution — roll back errors if it fails
                var errorsBefore = _context.Errors.Count;
                var innerType = _typeResolver.ResolveType(parenSyntax.Expression);
                if (innerType != null)
                {
                    return ResolveConversion(syntax.Arguments[0], innerType, span);
                }
                // Not a type — roll back errors and fall through to resolve as expression call
                _context.Errors.TruncateTo(errorsBefore);
            }

            // Method or package function call: x.Foo(args) or pkg.Func(args)
            if (syntax.Function is SelectorExpressionSyntax selectorSyntax)
            {
                var target = _resolveExpression(selectorSyntax.Expression);
                var methodName = selectorSyntax.Name.Text;

                // Method expression call: Type.Method(receiver, args...) or (*Type).Method(receiver, args...)
                // When the target is a type (not a package, not a value), the first argument is the receiver.
                if (target is IdentifierExpression typeTargetExpr && typeTargetExpr.Symbol is TypeSymbol typeTarget
                    && !(typeTargetExpr.Symbol is PackageSymbol))
                {
                    TypeSymbol lookupTypeForMethod = typeTarget;
                    if (typeTarget is PointerTypeSymbol ptrTarget)
                    {
                        lookupTypeForMethod = ptrTarget.ElementType;
                    }

                    var methodForExpr = lookupTypeForMethod.LookupMethod(methodName);
                    if (methodForExpr != null)
                    {
                        var allArguments = BindArguments(syntax);
                        if (allArguments.Count == methodForExpr.Parameters.Count + 1)
                        {
                            var receiverArg = allArguments[0];
                            var methodArguments = new List<Expression>(methodForExpr.Parameters.Count);
                            for (int i = 1; i < allArguments.Count; i++)
                            {
                                methodArguments.Add(allArguments[i]);
                            }
                            return new MethodCallExpression(receiverArg, methodForExpr, methodArguments, span);
                        }
                    }
                }

                // Package function call: fmt.Println(args)
                if (target is IdentifierExpression pkgIdExpr && pkgIdExpr.Symbol is PackageSymbol pkg)
                {
                    _context.UsedPackages.Add(pkg.Name);
                    var export = pkg.LookupExport(methodName);
                    if (export is FunctionSymbol pkgFunc)
                    {
                        // unsafe.Slice/SliceData/String are polymorphic — infer types from args
                        if (pkg.ImportPath == "unsafe" && (methodName is "Slice" or "SliceData" or "String"))
                        {
                            var result = ResolveUnsafePolymorphic(methodName, pkgFunc, syntax, span);
                            if (result != null) return result;
                        }

                        return ResolvePackageFunctionCall(pkgFunc, syntax, span);
                    }

                    if (export is TypeSymbol exportType && syntax.Arguments.Count == 1)
                    {
                        return ResolveConversion(syntax.Arguments[0], exportType, span);
                    }

                    // Package variable with function type: bufio.ScanLines(data, atEOF)
                    FunctionTypeSymbol? pkgFuncType = null;
                    if (export is PackageVarSymbol pkgVar && pkgVar.Type is FunctionTypeSymbol pvft)
                    {
                        pkgFuncType = pvft;
                    }
                    else if (export is LocalSymbol localVar && localVar.Type is FunctionTypeSymbol lvft)
                    {
                        pkgFuncType = lvft;
                    }
                    if (pkgFuncType != null)
                    {
                        var arguments = BindArguments(syntax);
                        var paramSymbols = new List<ParameterSymbol>();
                        for (int i = 0; i < pkgFuncType.ParameterTypes.Count; i++)
                        {
                            paramSymbols.Add(new ParameterSymbol("_", pkgFuncType.ParameterTypes[i], i));
                        }
                        var syntheticFunc = new FunctionSymbol(methodName, paramSymbols, pkgFuncType.ReturnTypes,
                            isVariadic: pkgFuncType.IsVariadic, packageName: pkg.Name);
                        return new CallExpression(syntheticFunc, arguments, span);
                    }

                    _context.Errors.ReportError(span, ErrorCode.UndeclaredName,
                        $"Package '{pkg.Name}' has no exported function '{methodName}'");
                    return new ErrorExpression($"Undefined: {pkg.Name}.{methodName}", span);
                }

                if (target.Type != TypeSymbol.Error)
                {
                    var targetType = target.Type;

                    // Look up methods on the base type (deref pointer if needed)
                    var lookupType = targetType;
                    bool isPointerTarget = targetType is PointerTypeSymbol;
                    if (isPointerTarget)
                    {
                        lookupType = ((PointerTypeSymbol)targetType).ElementType;
                    }

                    var method = lookupType.LookupMethod(methodName);

                    // Extract type param substitution for instantiated generic types
                    IReadOnlyList<TypeParameterSymbol>? methodInstTypeParams = null;
                    IReadOnlyList<TypeSymbol>? methodInstTypeArgs = null;
                    if (lookupType is InstantiatedTypeSymbol methodInst)
                    {
                        methodInstTypeParams = methodInst.GenericType.TypeParameters;
                        methodInstTypeArgs = methodInst.TypeArguments;
                    }

                    // Check for interface method calls (InterfaceTypeSymbol hides
                    // the base LookupMethod with 'new', so we must cast explicitly)
                    if (method == null && lookupType is InterfaceTypeSymbol ifaceType)
                    {
                        method = ifaceType.LookupMethod(methodName);
                    }

                    // Check on resolved type (handles type aliases, instantiated generics, etc.)
                    var resolvedLookup = lookupType.Resolved();
                    if (method == null && resolvedLookup != lookupType)
                    {
                        method = resolvedLookup.LookupMethod(methodName);
                        if (method == null && resolvedLookup is InterfaceTypeSymbol resolvedIface)
                        {
                            method = resolvedIface.LookupMethod(methodName);
                        }
                    }

                    // Check promoted methods from embedded structs
                    if (method == null)
                    {
                        var structForMethod = resolvedLookup as StructTypeSymbol
                            ?? lookupType as StructTypeSymbol;
                        if (structForMethod != null)
                        {
                            foreach (var f in structForMethod.Fields)
                            {
                                if (!f.IsEmbedded) continue;
                                var promoted = f.Type.LookupMethod(methodName);
                                if (promoted != null)
                                {
                                    method = promoted;
                                    // Rewrite target to access embedded field first
                                    var embType = methodInstTypeParams != null
                                        ? TypeSubstituter.Substitute(f.Type, methodInstTypeParams, methodInstTypeArgs!)
                                        : f.Type;
                                    target = new SelectorExpression(target, f, embType, target.Span);
                                    break;
                                }
                            }
                        }
                    }

                    if (method != null)
                    {
                        // Substitute type parameters for instantiated generic types
                        if (methodInstTypeParams != null)
                        {
                            var substParams = TypeSubstituter.SubstituteParams(
                                method.Parameters, methodInstTypeParams, methodInstTypeArgs!);
                            var substReturnTypes = TypeSubstituter.SubstituteTypes(
                                method.ReturnTypes, methodInstTypeParams, methodInstTypeArgs!);
                            method = new MethodSymbol(method.Name, method.ReceiverType,
                                method.IsPointerReceiver, method.TypeParameters,
                                substParams, substReturnTypes, method.IsVariadic);
                        }

                        // Adjust target to match receiver type
                        if (method.IsPointerReceiver)
                        {
                            // Pointer-receiver method: target must be a pointer
                            if (!isPointerTarget)
                            {
                                target = new AddressOfExpression(target,
                                    new PointerTypeSymbol(targetType), target.Span);
                            }
                        }
                        else
                        {
                            // Value-receiver method: target must be a value
                            if (isPointerTarget)
                            {
                                target = new DerefExpression(target, lookupType, target.Span);
                            }
                        }

                        var result = ResolveMethodCall(target, method, methodName, syntax, span);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
            }

            // Regular function call
            var funcExpr = _resolveExpression(syntax.Function);
            if (funcExpr is ErrorExpression)
            {
                return funcExpr;
            }

            if (funcExpr is IdentifierExpression idExpr && idExpr.Symbol is FunctionSymbol funcSymbol)
            {
                var arguments = BindArguments(syntax);

                // Generic function with type inference: Max(3, 5) → infer T=int
                if (funcSymbol.IsGeneric)
                {
                    return ResolveGenericCallWithInference(funcSymbol, arguments, span);
                }

                if (funcSymbol.IsVariadic)
                {
                    // Variadic: last parameter is the slice, required params are all except last
                    int requiredCount = funcSymbol.Parameters.Count - 1;
                    if (arguments.Count < requiredCount)
                    {
                        _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                            $"Function '{funcSymbol.Name}' expects at least {requiredCount} arguments, got {arguments.Count}");
                        return new ErrorExpression("Wrong argument count", span);
                    }

                    // Type-check required parameters
                    for (int i = 0; i < requiredCount; i++)
                    {
                        if (!TypeChecker.IsAssignable(arguments[i].Type, funcSymbol.Parameters[i].Type))
                        {
                            _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                                $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{funcSymbol.Parameters[i].Type.Name}'");
                        }
                    }

                    // Type-check variadic args against the slice element type
                    if (funcSymbol.Parameters.Count > 0)
                    {
                        var lastParamType = funcSymbol.Parameters[funcSymbol.Parameters.Count - 1].Type;
                        if (lastParamType is SliceTypeSymbol sliceType)
                        {
                            // If exactly one variadic arg and it's a slice of the element type,
                            // treat as spread (Go's f(slice...) syntax)
                            int varArgCount = arguments.Count - requiredCount;
                            bool isSpread = varArgCount == 1
                                && arguments[requiredCount].Type is SliceTypeSymbol argSlice
                                && TypeChecker.IsAssignable(argSlice.ElementType, sliceType.ElementType);

                            if (!isSpread)
                            {
                                for (int i = requiredCount; i < arguments.Count; i++)
                                {
                                    if (!TypeChecker.IsAssignable(arguments[i].Type, sliceType.ElementType)
                                        && !TypeChecker.IsAssignable(arguments[i].Type, lastParamType))
                                    {
                                        _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                                            $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{sliceType.ElementType.Name}'");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!ValidateArguments(arguments, funcSymbol.Parameters, $"Function '{funcSymbol.Name}'", span))
                    {
                        if (arguments.Count != funcSymbol.Parameters.Count)
                        {
                            return new ErrorExpression("Wrong argument count", span);
                        }
                    }
                }

                return new CallExpression(funcSymbol, arguments, span);
            }

            // Type conversion for function types: anyFieldC[bool](funcValue)
            // When funcExpr is a type identifier (not a value), treat as conversion
            if (syntax.Arguments.Count == 1
                && funcExpr is IdentifierExpression typeIdExpr
                && typeIdExpr.Symbol is TypeSymbol typeConvSym
                && typeConvSym != TypeSymbol.Error)
            {
                var resolvedConvType = typeConvSym.Resolved();
                if (resolvedConvType is FunctionTypeSymbol
                    || typeConvSym is InstantiatedTypeSymbol
                    || typeConvSym is FunctionTypeSymbol)
                {
                    return ResolveConversion(syntax.Arguments[0], typeConvSym, span);
                }
            }

            var resolvedCallType = funcExpr.Type is FunctionTypeSymbol ? funcExpr.Type
                : funcExpr.Type?.UnderlyingType is FunctionTypeSymbol ? funcExpr.Type?.UnderlyingType
                : funcExpr.Type?.Resolved() is FunctionTypeSymbol ? funcExpr.Type?.Resolved()
                : funcExpr.Type?.Resolved()?.UnderlyingType is FunctionTypeSymbol ? funcExpr.Type?.Resolved()?.UnderlyingType
                : funcExpr.Type?.Resolved();
            if (resolvedCallType is FunctionTypeSymbol funcTypeSymbol)
            {
                var arguments = BindArguments(syntax);

                int requiredParams = funcTypeSymbol.IsVariadic
                    ? funcTypeSymbol.ParameterTypes.Count - 1
                    : funcTypeSymbol.ParameterTypes.Count;

                if (funcTypeSymbol.IsVariadic)
                {
                    if (arguments.Count < requiredParams)
                    {
                        _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                            $"Function value expects at least {requiredParams} arguments, got {arguments.Count}");
                        return new ErrorExpression("Wrong argument count", span);
                    }
                }
                else if (arguments.Count != funcTypeSymbol.ParameterTypes.Count)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"Function value expects {funcTypeSymbol.ParameterTypes.Count} arguments, got {arguments.Count}");
                    return new ErrorExpression("Wrong argument count", span);
                }

                for (int i = 0; i < arguments.Count; i++)
                {
                    TypeSymbol expectedType;
                    if (i < funcTypeSymbol.ParameterTypes.Count)
                    {
                        expectedType = funcTypeSymbol.ParameterTypes[i];
                        // For the variadic param, check against element type if extra args,
                        // or accept either slice or element type if exactly one arg at variadic position
                        if (funcTypeSymbol.IsVariadic && i == funcTypeSymbol.ParameterTypes.Count - 1
                            && expectedType is SliceTypeSymbol variadicSlice)
                        {
                            if (syntax.Ellipsis != null)
                            {
                                // fn(slice...) — spread: accept the slice type directly
                            }
                            else if (arguments.Count > funcTypeSymbol.ParameterTypes.Count)
                            {
                                expectedType = variadicSlice.ElementType;
                            }
                            else if (!TypeChecker.IsAssignable(arguments[i].Type, expectedType))
                            {
                                expectedType = variadicSlice.ElementType;
                            }
                        }
                    }
                    else if (funcTypeSymbol.IsVariadic
                        && funcTypeSymbol.ParameterTypes[funcTypeSymbol.ParameterTypes.Count - 1]
                            is SliceTypeSymbol varSlice)
                    {
                        expectedType = varSlice.ElementType;
                    }
                    else
                    {
                        break;
                    }

                    if (!TypeChecker.IsAssignable(arguments[i].Type, expectedType))
                    {
                        _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                            $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{expectedType.Name}'");
                    }
                }

                var paramSymbols = new List<ParameterSymbol>();
                for (int i = 0; i < funcTypeSymbol.ParameterTypes.Count; i++)
                {
                    paramSymbols.Add(new ParameterSymbol("_", funcTypeSymbol.ParameterTypes[i], i));
                }

                var syntheticFunc = new FunctionSymbol("$$anon", paramSymbols, funcTypeSymbol.ReturnTypes,
                    isVariadic: funcTypeSymbol.IsVariadic);
                return new CallExpression(syntheticFunc, arguments, funcExpr, span);
            }

            // Type conversion via parenthesized type: ([]error)(nil), (*T)(ptr),
            // anyFieldC[bool](funcName), etc.
            if (syntax.Arguments.Count == 1 && funcExpr.Type is TypeSymbol convType
                && convType != TypeSymbol.Error)
            {
                var resolvedConv = convType.Resolved();
                if (convType is SliceTypeSymbol || convType is ArrayTypeSymbol
                    || convType is MapTypeSymbol || convType is PointerTypeSymbol
                    || convType is ChannelTypeSymbol || convType is StructTypeSymbol
                    || convType is InterfaceTypeSymbol || convType is FunctionTypeSymbol
                    || resolvedConv is FunctionTypeSymbol)
                {
                    return ResolveConversion(syntax.Arguments[0], convType, span);
                }
            }

            // interface{}/any values are dynamically typed and may hold callable values at runtime
            if (funcExpr.Type is InterfaceTypeSymbol ifaceCall && ifaceCall.Methods.Count == 0
                || funcExpr.Type?.Resolved() is InterfaceTypeSymbol ifaceCall2 && ifaceCall2.Methods.Count == 0)
            {
                var arguments = BindArguments(syntax);
                var paramSymbols = new List<ParameterSymbol>();
                for (int i = 0; i < arguments.Count; i++)
                {
                    paramSymbols.Add(new ParameterSymbol("_", arguments[i].Type ?? BuiltinTypes.EmptyInterface, i));
                }

                var syntheticFunc = new FunctionSymbol("$$dynamic_call", paramSymbols,
                    new List<TypeSymbol> { BuiltinTypes.EmptyInterface });
                return new CallExpression(syntheticFunc, arguments, funcExpr, span);
            }

            _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                "Expression is not callable");
            return new ErrorExpression("Not callable", span);
        }

        public Expression ResolveConversion(ExpressionSyntax syntax, TypeSymbol targetType, TextSpan span)
        {
            var operand = _resolveExpression(syntax);

            if (operand.Type == null || targetType == null)
            {
                return new ErrorExpression("Invalid conversion", span);
            }

            if (!TypeChecker.CanConvert(operand.Type, targetType))
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidConversion,
                    $"Cannot convert '{operand.Type.Name}' to '{targetType.Name}'");
                return new ErrorExpression("Invalid conversion", span);
            }

            return new ConversionExpression(operand, targetType, span);
        }

        private Expression? ResolveUnsafePolymorphic(
            string name, FunctionSymbol funcSymbol, CallExpressionSyntax syntax, TextSpan span)
        {
            var arguments = BindArguments(syntax);

            if (name == "Slice" && arguments.Count == 2)
            {
                // unsafe.Slice(ptr *T, len IntegerType) []T
                var ptrType = arguments[0].Type;
                TypeSymbol elemType;
                if (ptrType is PointerTypeSymbol ptr)
                    elemType = ptr.ElementType;
                else
                    elemType = BuiltinTypes.Byte; // fallback

                var returnType = new SliceTypeSymbol(elemType);
                return new CallExpression(funcSymbol, arguments, span)
                {
                    SubstitutedReturnType = returnType,
                    SubstitutedReturnTypes = new TypeSymbol[] { returnType }
                };
            }

            if (name == "SliceData" && arguments.Count == 1)
            {
                // unsafe.SliceData(s []T) *T
                var sliceType = arguments[0].Type;
                TypeSymbol elemType;
                if (sliceType is SliceTypeSymbol slice)
                    elemType = slice.ElementType;
                else
                    elemType = BuiltinTypes.Byte; // fallback

                var returnType = new PointerTypeSymbol(elemType);
                return new CallExpression(funcSymbol, arguments, span)
                {
                    SubstitutedReturnType = returnType,
                    SubstitutedReturnTypes = new TypeSymbol[] { returnType }
                };
            }

            if (name == "String" && arguments.Count == 2)
            {
                // unsafe.String(ptr *byte, len IntegerType) string — already correct, just accept any int
                var returnType = BuiltinTypes.String;
                return new CallExpression(funcSymbol, arguments, span)
                {
                    SubstitutedReturnType = returnType,
                    SubstitutedReturnTypes = new TypeSymbol[] { returnType }
                };
            }

            return null;
        }

        private Expression ResolveSimpleBuiltin(string name, CallExpressionSyntax syntax, TextSpan span)
        {
            var args = new List<Expression>();
            for (int i = 0; i < syntax.Arguments.Count; i++)
            {
                args.Add(_resolveExpression(syntax.Arguments[i]));
            }

            // Determine return type
            TypeSymbol returnType = BuiltinTypes.Void;
            if (name == "recover")
            {
                returnType = BuiltinTypes.Resolve("interface{}") ?? TypeSymbol.Error;
            }
            else if (name == "new" && args.Count == 1)
            {
                returnType = new PointerTypeSymbol(args[0].Type);
            }
            else if (name == "copy")
            {
                returnType = BuiltinTypes.Int;
            }

            var funcSymbol = new FunctionSymbol(name,
                Array.Empty<ParameterSymbol>(), returnType);
            return new CallExpression(funcSymbol, args, span);
        }

        private Expression ResolvePackageFunctionCall(
            FunctionSymbol func,
            CallExpressionSyntax syntax,
            TextSpan span)
        {
            var arguments = BindArguments(syntax);

            if (func.IsVariadic)
            {
                // Variadic: last parameter is the variadic container (slice or GoParam-typed).
                // Required count is always params - 1 since we're inside IsVariadic.
                int requiredCount = func.Parameters.Count - 1;

                if (arguments.Count < requiredCount)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"Function '{func.Name}' expects at least {requiredCount} arguments, got {arguments.Count}");
                    return new ErrorExpression("Wrong argument count", span);
                }

                // Type-check required (non-variadic) parameters
                for (int i = 0; i < requiredCount && i < arguments.Count; i++)
                {
                    if (!TypeChecker.IsAssignable(arguments[i].Type, func.Parameters[i].Type))
                    {
                        _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                            $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{func.Parameters[i].Type.Name}'");
                    }
                }

                // Type-check variadic args against element type or slice type
                if (func.Parameters.Count > 0 && func.Parameters[func.Parameters.Count - 1].Type is SliceTypeSymbol)
                {
                    var lastParamType = func.Parameters[func.Parameters.Count - 1].Type;
                    if (lastParamType is SliceTypeSymbol sliceType)
                    {
                        for (int i = requiredCount; i < arguments.Count; i++)
                        {
                            if (!TypeChecker.IsAssignable(arguments[i].Type, sliceType.ElementType)
                                && !TypeChecker.IsAssignable(arguments[i].Type, lastParamType))
                            {
                                _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                                    $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{sliceType.ElementType.Name}'");
                            }
                        }
                    }
                }

                // Extra variadic args (registry-style) are not type-checked (they accept interface{})
            }
            else
            {
                if (!ValidateArguments(arguments, func.Parameters, $"Function '{func.Name}'", span))
                {
                    if (arguments.Count != func.Parameters.Count)
                    {
                        return new ErrorExpression("Wrong argument count", span);
                    }
                }
            }

            return new CallExpression(func, arguments, span);
        }

        private Expression ResolveMethodCall(
            Expression target,
            MethodSymbol method,
            string methodName,
            CallExpressionSyntax syntax,
            TextSpan span)
        {
            var arguments = BindArguments(syntax);

            if (method.IsVariadic)
            {
                int requiredCount = method.Parameters.Count - 1;
                if (arguments.Count < requiredCount)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"Method '{methodName}' expects at least {requiredCount} arguments, got {arguments.Count}");
                    return new ErrorExpression("Wrong argument count", span);
                }

                for (int i = 0; i < requiredCount; i++)
                {
                    if (!TypeChecker.IsAssignable(arguments[i].Type, method.Parameters[i].Type))
                    {
                        _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                            $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{method.Parameters[i].Type.Name}'");
                    }
                }

                if (method.Parameters.Count > 0)
                {
                    var lastParamType = method.Parameters[method.Parameters.Count - 1].Type;
                    if (lastParamType is SliceTypeSymbol sliceType)
                    {
                        for (int i = requiredCount; i < arguments.Count; i++)
                        {
                            if (!TypeChecker.IsAssignable(arguments[i].Type, sliceType.ElementType)
                                && !TypeChecker.IsAssignable(arguments[i].Type, lastParamType))
                            {
                                _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                                    $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{sliceType.ElementType.Name}'");
                            }
                        }
                    }
                }
            }
            else
            {
                if (!ValidateArguments(arguments, method.Parameters, $"Method '{methodName}'", span))
                {
                    if (arguments.Count != method.Parameters.Count)
                    {
                        return new ErrorExpression("Wrong argument count", span);
                    }
                }
            }

            return new MethodCallExpression(target, method, arguments, span);
        }

        private bool ValidateArguments(
            IReadOnlyList<Expression> arguments,
            IReadOnlyList<ParameterSymbol> parameters,
            string callableName,
            TextSpan span)
        {
            if (arguments.Count != parameters.Count)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    $"{callableName} expects {parameters.Count} arguments, got {arguments.Count}");
                return false;
            }

            bool valid = true;
            for (int i = 0; i < arguments.Count; i++)
            {
                if (!TypeChecker.IsAssignable(arguments[i].Type, parameters[i].Type))
                {
                    _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                        $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{parameters[i].Type.Name}'");
                    valid = false;
                }
            }

            return valid;
        }

        private List<Expression> BindArguments(CallExpressionSyntax syntax)
        {
            var arguments = new List<Expression>();
            for (int i = 0; i < syntax.Arguments.Count; i++)
            {
                arguments.Add(_resolveExpression(syntax.Arguments[i]));
            }

            // Multi-return spread: f(g()) where g returns multiple values
            if (arguments.Count == 1)
            {
                IReadOnlyList<TypeSymbol>? returnTypes = null;

                if (arguments[0] is CallExpression innerCall
                    && innerCall.Function.ReturnTypes.Count > 1)
                {
                    innerCall.IsSpreadArg = true;
                    returnTypes = innerCall.Function.ReturnTypes;
                }
                else if (arguments[0] is MethodCallExpression innerMethodCall
                    && innerMethodCall.Method.ReturnTypes.Count > 1)
                {
                    innerMethodCall.IsSpreadArg = true;
                    returnTypes = innerMethodCall.Method.ReturnTypes;
                }

                if (returnTypes != null)
                {
                    var spread = new List<Expression> { arguments[0] };
                    for (int i = 1; i < returnTypes.Count; i++)
                    {
                        spread.Add(new SpreadElement(arguments[0], i,
                            returnTypes[i], arguments[0].Span));
                    }

                    return spread;
                }
            }

            return arguments;
        }

        private Expression ResolveGenericCallWithExplicitArgs(
            TypeArgumentListSyntax typeArgListSyntax,
            CallExpressionSyntax syntax,
            TextSpan span)
        {
            // Resolve the base function
            var funcExpr = _resolveExpression(typeArgListSyntax.Expression);
            if (funcExpr is ErrorExpression)
            {
                return funcExpr;
            }

            if (!(funcExpr is IdentifierExpression idExpr && idExpr.Symbol is FunctionSymbol funcSymbol))
            {
                // Check if this is a generic type conversion: TypeName[K, V](value)
                if (funcExpr is IdentifierExpression typeIdExpr
                    && typeIdExpr.Symbol is TypeSymbol typeConv
                    && syntax.Arguments.Count == 1)
                {
                    // Resolve the full generic type from the TypeArgumentListSyntax
                    var resolvedType = _typeResolver.ResolveType(typeArgListSyntax);
                    if (resolvedType != null)
                    {
                        return ResolveConversion(syntax.Arguments[0], resolvedType, span);
                    }
                }

                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    "Type arguments can only be applied to generic functions");
                return new ErrorExpression("Not a generic function", span);
            }

            if (!funcSymbol.IsGeneric)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongTypeArgumentCount,
                    $"Function '{funcSymbol.Name}' is not generic");
                return new ErrorExpression("Not generic", span);
            }

            // Resolve type arguments
            var typeArgs = new List<TypeSymbol>();
            for (int i = 0; i < typeArgListSyntax.TypeArguments.Count; i++)
            {
                var resolved = _typeResolver.ResolveType(typeArgListSyntax.TypeArguments[i]);
                typeArgs.Add(resolved ?? TypeSymbol.Error);
            }

            return ResolveGenericCallWithTypeArgs(funcSymbol, typeArgs, syntax, span);
        }

        private Expression? TryResolveGenericCallWithSingleArg(
            IndexExpressionSyntax indexSyntax,
            CallExpressionSyntax syntax,
            TextSpan span)
        {
            // Check if the base expression is a generic function
            if (indexSyntax.Expression is IdentifierNameSyntax idSyntax)
            {
                var symbol = _context.Scope.Lookup(idSyntax.Identifier.Text);
                if (symbol is FunctionSymbol funcSymbol && funcSymbol.IsGeneric)
                {
                    var typeArg = _typeResolver.ResolveType(indexSyntax.Index);
                    if (typeArg == null)
                    {
                        typeArg = TypeSymbol.Error;
                    }

                    return ResolveGenericCallWithTypeArgs(funcSymbol, new[] { typeArg }, syntax, span);
                }
            }

            // Check if the base expression is a qualified generic function: pkg.Func[Type](args)
            if (indexSyntax.Expression is SelectorExpressionSyntax selectorSyntax
                && selectorSyntax.Expression is IdentifierNameSyntax pkgIdSyntax)
            {
                var pkgSymbol = _context.Scope.Lookup(pkgIdSyntax.Identifier.Text);
                if (pkgSymbol is PackageSymbol pkg)
                {
                    _context.UsedPackages.Add(pkg.Name);
                    var export = pkg.LookupExport(selectorSyntax.Name.Text);
                    if (export is FunctionSymbol funcSymbol && funcSymbol.IsGeneric)
                    {
                        var typeArg = _typeResolver.ResolveType(indexSyntax.Index);
                        if (typeArg == null)
                        {
                            typeArg = TypeSymbol.Error;
                        }

                        return ResolveGenericCallWithTypeArgs(funcSymbol, new[] { typeArg }, syntax, span);
                    }
                }
            }

            return null;
        }

        private Expression ResolveGenericCallWithTypeArgs(
            FunctionSymbol funcSymbol,
            IReadOnlyList<TypeSymbol> typeArgs,
            CallExpressionSyntax syntax,
            TextSpan span)
        {
            // Validate type argument count
            if (typeArgs.Count > funcSymbol.TypeParameters.Count)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongTypeArgumentCount,
                    $"Function '{funcSymbol.Name}' expects {funcSymbol.TypeParameters.Count} type arguments, got {typeArgs.Count}");
                return new ErrorExpression("Wrong type argument count", span);
            }

            // Partial type arguments: infer remaining from arguments
            IReadOnlyList<TypeSymbol> fullTypeArgs = typeArgs;
            if (typeArgs.Count < funcSymbol.TypeParameters.Count)
            {
                var partialArgs = BindArguments(syntax);
                // Create a partially-substituted function for inference
                var partialSubstParams = TypeSubstituter.SubstituteParams(
                    funcSymbol.Parameters, funcSymbol.TypeParameters, typeArgs);

                var partialFunc = new FunctionSymbol(funcSymbol.Name,
                    funcSymbol.TypeParameters, partialSubstParams,
                    funcSymbol.ReturnTypes, funcSymbol.IsVariadic, funcSymbol.PackageName);

                var merged = new TypeSymbol[funcSymbol.TypeParameters.Count];
                for (int i = 0; i < typeArgs.Count; i++)
                    merged[i] = typeArgs[i];

                // Try to infer remaining type args from arguments
                var inferredAll = TypeInferrer.InferTypeArguments(partialFunc, partialArgs);
                if (inferredAll != null)
                {
                    for (int i = typeArgs.Count; i < funcSymbol.TypeParameters.Count; i++)
                        merged[i] = inferredAll[i];
                }

                // For any still-missing type params, try to infer from constraint
                // relationships with already-known type params. E.g., S ~[]E with S known
                // implies E = element type of S.
                for (int i = 0; i < merged.Length; i++)
                {
                    if (merged[i] != null) continue;
                    merged[i] = InferTypeParamFromConstraints(
                        funcSymbol.TypeParameters[i], funcSymbol.TypeParameters, merged);
                }

                bool allResolved = true;
                for (int i = 0; i < merged.Length; i++)
                {
                    if (merged[i] == null) { allResolved = false; break; }
                }

                if (allResolved)
                {
                    fullTypeArgs = merged;
                }
                else
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongTypeArgumentCount,
                        $"Function '{funcSymbol.Name}' expects {funcSymbol.TypeParameters.Count} type arguments, got {typeArgs.Count}");
                    return new ErrorExpression("Wrong type argument count", span);
                }
            }

            // Validate constraints
            for (int i = 0; i < fullTypeArgs.Count; i++)
            {
                if (fullTypeArgs[i] != TypeSymbol.Error &&
                    !ConstraintChecker.Satisfies(fullTypeArgs[i], funcSymbol.TypeParameters[i].Constraint))
                {
                    _context.Errors.ReportError(span, ErrorCode.ConstraintNotSatisfied,
                        $"Type '{fullTypeArgs[i].Name}' does not satisfy constraint '{funcSymbol.TypeParameters[i].Constraint.Name}'");
                }
            }

            // Substitute type parameters in parameter and return types
            var substParams = TypeSubstituter.SubstituteParams(
                funcSymbol.Parameters, funcSymbol.TypeParameters, fullTypeArgs);
            var substReturnTypes = TypeSubstituter.SubstituteTypes(
                funcSymbol.ReturnTypes, funcSymbol.TypeParameters, fullTypeArgs);

            // Bind and validate arguments
            var arguments = BindArguments(syntax);
            var substFunc = new FunctionSymbol(funcSymbol.Name, funcSymbol.TypeParameters,
                substParams, substReturnTypes, funcSymbol.IsVariadic, funcSymbol.PackageName);

            if (funcSymbol.IsVariadic)
            {
                int requiredCount = substParams.Count - 1;
                if (arguments.Count < requiredCount)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"Function '{funcSymbol.Name}' expects at least {requiredCount} arguments, got {arguments.Count}");
                    return new ErrorExpression("Wrong argument count", span);
                }
            }
            else if (!ValidateArguments(arguments, substParams, $"Function '{funcSymbol.Name}'", span))
            {
                if (arguments.Count != substParams.Count)
                {
                    return new ErrorExpression("Wrong argument count", span);
                }
            }

            var substReturnType = substReturnTypes.Count > 0 ? substReturnTypes[0] : BuiltinTypes.Void;
            return new CallExpression(funcSymbol, arguments, span)
            {
                TypeArguments = typeArgs,
                SubstitutedReturnType = substReturnType,
                SubstitutedReturnTypes = substReturnTypes
            };
        }

        private static TypeSymbol? InferTypeParamFromConstraints(
            TypeParameterSymbol target,
            IReadOnlyList<TypeParameterSymbol> allParams,
            TypeSymbol?[] known)
        {
            // Look through constraints of already-known type params to find the target.
            // E.g., S ~[]E: if S is known as []int, then E = int.
            for (int i = 0; i < allParams.Count; i++)
            {
                if (known[i] == null) continue;
                var constraint = allParams[i].Constraint;
                foreach (var elem in constraint.TypeElements)
                {
                    var extracted = ExtractTypeParam(elem.Type, target, known[i]);
                    if (extracted != null) return extracted;
                }
            }
            return null;
        }

        private static TypeSymbol? ExtractTypeParam(TypeSymbol pattern, TypeParameterSymbol target, TypeSymbol concrete)
        {
            if (pattern == target) return concrete;

            var resolvedConcrete = concrete.Resolved();
            if (resolvedConcrete == concrete && concrete.UnderlyingType != null)
                resolvedConcrete = concrete.UnderlyingType;

            // For type parameter concrete types, look at their structural constraint
            if (resolvedConcrete is TypeParameterSymbol concreteTp)
            {
                var structural = TypeChecker.GetConstraintStructuralType(concreteTp);
                if (structural != null)
                    resolvedConcrete = structural;
            }

            if (pattern is SliceTypeSymbol patSlice && resolvedConcrete is SliceTypeSymbol conSlice)
                return ExtractTypeParam(patSlice.ElementType, target, conSlice.ElementType);
            if (pattern is ArrayTypeSymbol patArr && resolvedConcrete is ArrayTypeSymbol conArr)
                return ExtractTypeParam(patArr.ElementType, target, conArr.ElementType);
            if (pattern is MapTypeSymbol patMap && resolvedConcrete is MapTypeSymbol conMap)
            {
                return ExtractTypeParam(patMap.KeyType, target, conMap.KeyType)
                    ?? ExtractTypeParam(patMap.ValueType, target, conMap.ValueType);
            }
            if (pattern is PointerTypeSymbol patPtr && resolvedConcrete is PointerTypeSymbol conPtr)
                return ExtractTypeParam(patPtr.ElementType, target, conPtr.ElementType);
            if (pattern is ChannelTypeSymbol patChan && resolvedConcrete is ChannelTypeSymbol conChan)
                return ExtractTypeParam(patChan.ElementType, target, conChan.ElementType);
            return null;
        }

        private Expression ResolveGenericCallWithInference(
            FunctionSymbol funcSymbol,
            IReadOnlyList<Expression> arguments,
            TextSpan span)
        {
            var typeArgs = TypeInferrer.InferTypeArguments(funcSymbol, arguments);
            if (typeArgs == null)
            {
                _context.Errors.ReportError(span, ErrorCode.CannotInferTypeArguments,
                    $"Cannot infer type arguments for generic function '{funcSymbol.Name}'");
                return new ErrorExpression("Cannot infer type args", span);
            }

            // Validate constraints (substitute inferred type args into constraint types first)
            for (int i = 0; i < typeArgs.Count; i++)
            {
                var constraint = funcSymbol.TypeParameters[i].Constraint;
                // Substitute type parameters in constraint type elements
                if (constraint.TypeElements.Count > 0)
                {
                    var substElements = new System.Collections.Generic.List<Symbols.TypeElement>();
                    bool changed = false;
                    foreach (var elem in constraint.TypeElements)
                    {
                        var substType = TypeSubstituter.Substitute(elem.Type,
                            funcSymbol.TypeParameters, typeArgs);
                        substElements.Add(new Symbols.TypeElement(substType, elem.IsTilde));
                        if (substType != elem.Type) changed = true;
                    }
                    if (changed)
                    {
                        constraint = new Symbols.ConstraintInfo(constraint.Name,
                            constraint.Methods, substElements, constraint.IsComparable);
                    }
                }

                if (!ConstraintChecker.Satisfies(typeArgs[i], constraint))
                {
                    _context.Errors.ReportError(span, ErrorCode.ConstraintNotSatisfied,
                        $"Type '{typeArgs[i].Name}' does not satisfy constraint '{funcSymbol.TypeParameters[i].Constraint.Name}'");
                }
            }

            // Substitute and validate
            var substParams = TypeSubstituter.SubstituteParams(
                funcSymbol.Parameters, funcSymbol.TypeParameters, typeArgs);
            var substReturnTypes = TypeSubstituter.SubstituteTypes(
                funcSymbol.ReturnTypes, funcSymbol.TypeParameters, typeArgs);

            if (funcSymbol.IsVariadic)
            {
                int requiredCount = substParams.Count - 1;
                if (arguments.Count < requiredCount)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"Function '{funcSymbol.Name}' expects at least {requiredCount} arguments, got {arguments.Count}");
                    return new ErrorExpression("Wrong argument count", span);
                }
            }
            else if (!ValidateArguments(arguments, substParams, $"Function '{funcSymbol.Name}'", span))
            {
                if (arguments.Count != substParams.Count)
                {
                    return new ErrorExpression("Wrong argument count", span);
                }
            }

            var substReturnType = substReturnTypes.Count > 0 ? substReturnTypes[0] : BuiltinTypes.Void;
            return new CallExpression(funcSymbol, arguments, span)
            {
                TypeArguments = typeArgs,
                SubstitutedReturnType = substReturnType,
                SubstitutedReturnTypes = substReturnTypes
            };
        }
    }
}

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
                    case "close":
                    case "panic":
                    case "recover":
                    case "new":
                    case "copy":
                        return ResolveSimpleBuiltin(name, syntax, span);
                }
            }

            // Composite type conversion: []byte(s), []int(s), etc.
            if (syntax.Function is SliceTypeSyntax || syntax.Function is ArrayTypeSyntax)
            {
                if (syntax.Arguments.Count == 1)
                {
                    var targetType = _typeResolver.ResolveType(syntax.Function);
                    return ResolveConversion(syntax.Arguments[0], targetType, span);
                }
            }

            // Method or package function call: x.Foo(args) or pkg.Func(args)
            if (syntax.Function is SelectorExpressionSyntax selectorSyntax)
            {
                var target = _resolveExpression(selectorSyntax.Expression);
                var methodName = selectorSyntax.Name.Text;

                // Package function call: fmt.Println(args)
                if (target is IdentifierExpression pkgIdExpr && pkgIdExpr.Symbol is PackageSymbol pkg)
                {
                    _context.UsedPackages.Add(pkg.Name);
                    var export = pkg.LookupExport(methodName);
                    if (export is FunctionSymbol pkgFunc)
                    {
                        return ResolvePackageFunctionCall(pkgFunc, syntax, span);
                    }

                    if (export is TypeSymbol exportType && syntax.Arguments.Count == 1)
                    {
                        return ResolveConversion(syntax.Arguments[0], exportType, span);
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

                    // Check for interface method calls (InterfaceTypeSymbol hides
                    // the base LookupMethod with 'new', so we must cast explicitly)
                    if (method == null && lookupType is InterfaceTypeSymbol ifaceType)
                    {
                        method = ifaceType.LookupMethod(methodName);
                    }

                    // Check promoted methods from embedded structs
                    if (method == null && lookupType is StructTypeSymbol structForMethod)
                    {
                        foreach (var f in structForMethod.Fields)
                        {
                            if (!f.IsEmbedded) continue;
                            var promoted = f.Type.LookupMethod(methodName);
                            if (promoted != null)
                            {
                                method = promoted;
                                // Rewrite target to access embedded field first
                                target = new SelectorExpression(target, f, f.Type, target.Span);
                                break;
                            }
                        }
                    }

                    if (method != null)
                    {
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
                            for (int i = requiredCount; i < arguments.Count; i++)
                            {
                                if (!TypeChecker.IsAssignable(arguments[i].Type, sliceType.ElementType))
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

            if (funcExpr.Type is FunctionTypeSymbol funcTypeSymbol)
            {
                var arguments = BindArguments(syntax);

                if (arguments.Count != funcTypeSymbol.ParameterTypes.Count)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"Function value expects {funcTypeSymbol.ParameterTypes.Count} arguments, got {arguments.Count}");
                    return new ErrorExpression("Wrong argument count", span);
                }

                for (int i = 0; i < arguments.Count; i++)
                {
                    if (!TypeChecker.IsAssignable(arguments[i].Type, funcTypeSymbol.ParameterTypes[i]))
                    {
                        _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                            $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{funcTypeSymbol.ParameterTypes[i].Name}'");
                    }
                }

                var paramSymbols = new List<ParameterSymbol>();
                for (int i = 0; i < funcTypeSymbol.ParameterTypes.Count; i++)
                {
                    paramSymbols.Add(new ParameterSymbol("_", funcTypeSymbol.ParameterTypes[i], i));
                }

                var syntheticFunc = new FunctionSymbol("$$anon", paramSymbols, funcTypeSymbol.ReturnTypes);
                return new CallExpression(syntheticFunc, arguments, funcExpr, span);
            }

            _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                "Expression is not callable");
            return new ErrorExpression("Not callable", span);
        }

        public Expression ResolveConversion(ExpressionSyntax syntax, TypeSymbol targetType, TextSpan span)
        {
            var operand = _resolveExpression(syntax);

            if (!TypeChecker.CanConvert(operand.Type, targetType))
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidConversion,
                    $"Cannot convert '{operand.Type.Name}' to '{targetType.Name}'");
                return new ErrorExpression("Invalid conversion", span);
            }

            return new ConversionExpression(operand, targetType, span);
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
                // For variadic functions, validate the required (non-variadic) parameters
                if (arguments.Count < func.Parameters.Count)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"Function '{func.Name}' expects at least {func.Parameters.Count} arguments, got {arguments.Count}");
                    return new ErrorExpression("Wrong argument count", span);
                }

                // Type-check required parameters
                for (int i = 0; i < func.Parameters.Count; i++)
                {
                    if (!TypeChecker.IsAssignable(arguments[i].Type, func.Parameters[i].Type))
                    {
                        _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                            $"Argument {i + 1}: cannot pass '{arguments[i].Type.Name}' as '{func.Parameters[i].Type.Name}'");
                    }
                }

                // Variadic args are not type-checked (they accept interface{})
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

            if (!ValidateArguments(arguments, method.Parameters, $"Method '{methodName}'", span))
            {
                if (arguments.Count != method.Parameters.Count)
                {
                    return new ErrorExpression("Wrong argument count", span);
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

            return arguments;
        }
    }
}

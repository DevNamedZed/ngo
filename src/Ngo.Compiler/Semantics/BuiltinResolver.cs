// -----------------------------------------------------------------------
// <copyright file="BuiltinResolver.cs" company="Ziad">
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
    public sealed class BuiltinResolver
    {
        private readonly AnalysisContext _context;
        private readonly TypeResolver _typeResolver;
        private Func<ExpressionSyntax, Expression> _resolveExpression;

        public BuiltinResolver(AnalysisContext context, TypeResolver typeResolver)
        {
            _context = context;
            _typeResolver = typeResolver;
            _resolveExpression = _ => throw new InvalidOperationException(
                "Expression resolver has not been set. Call SetExpressionResolver first.");
        }

        public void SetExpressionResolver(Func<ExpressionSyntax, Expression> resolveExpression)
        {
            _resolveExpression = resolveExpression;
        }

        public Expression ResolveLen(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count != 1)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    $"len expects 1 argument, got {syntax.Arguments.Count}");
                return new ErrorExpression("Wrong arg count", span);
            }

            var arg = _resolveExpression(syntax.Arguments[0]);

            var argResolved = ResolveUnderlying(arg.Type);
            // In Go, len(*[N]T) is valid — unwrap pointer to array
            if (argResolved is PointerTypeSymbol ptrForLen)
            {
                var ptrBase = ResolveUnderlying(ptrForLen.ElementType);
                if (ptrBase is ArrayTypeSymbol)
                    argResolved = ptrBase;
            }
            if (arg.Type != TypeSymbol.Error
                && argResolved is not SliceTypeSymbol
                && argResolved is not ArrayTypeSymbol
                && argResolved is not MapTypeSymbol
                && argResolved is not ChannelTypeSymbol
                && argResolved.TypeKind != TypeKind.String
                && argResolved.TypeKind != TypeKind.UntypedString
                && argResolved.TypeKind != TypeKind.Interface)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"Invalid argument type '{arg.Type.Name}' for len");
            }

            var lenSymbol = new FunctionSymbol("len",
                new[] { new ParameterSymbol("v", arg.Type, 0) }, BuiltinTypes.Int);
            return new CallExpression(lenSymbol, new[] { arg }, span);
        }

        public Expression ResolveCap(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count != 1)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    $"cap expects 1 argument, got {syntax.Arguments.Count}");
                return new ErrorExpression("Wrong arg count", span);
            }

            var arg = _resolveExpression(syntax.Arguments[0]);

            var capResolved = ResolveUnderlying(arg.Type);
            // In Go, cap(*[N]T) is valid — unwrap pointer to array
            if (capResolved is PointerTypeSymbol ptrForCap)
            {
                var ptrBase = ResolveUnderlying(ptrForCap.ElementType);
                if (ptrBase is ArrayTypeSymbol)
                    capResolved = ptrBase;
            }
            if (arg.Type != TypeSymbol.Error
                && capResolved is not SliceTypeSymbol
                && capResolved is not ArrayTypeSymbol
                && capResolved is not ChannelTypeSymbol)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"Invalid argument type '{arg.Type.Name}' for cap");
            }

            var capSymbol = new FunctionSymbol("cap",
                new[] { new ParameterSymbol("v", arg.Type, 0) }, BuiltinTypes.Int);
            return new CallExpression(capSymbol, new[] { arg }, span);
        }

        public Expression ResolveAppend(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count < 2)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    $"append expects at least 2 arguments, got {syntax.Arguments.Count}");
                return new ErrorExpression("Wrong arg count", span);
            }

            var sliceArg = _resolveExpression(syntax.Arguments[0]);

            var resolvedSliceType = ResolveUnderlying(sliceArg.Type);
            if (sliceArg.Type != TypeSymbol.Error && resolvedSliceType is not SliceTypeSymbol)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"First argument to append must be a slice, got '{sliceArg.Type.Name}'");
                return new ErrorExpression("Not a slice", span);
            }

            var args = new List<Expression> { sliceArg };
            var elemType = resolvedSliceType is SliceTypeSymbol st ? st.ElementType : TypeSymbol.Error;

            for (int i = 1; i < syntax.Arguments.Count; i++)
            {
                var elem = _resolveExpression(syntax.Arguments[i]);
                if (!TypeChecker.IsAssignable(elem.Type, elemType) && elem.Type != TypeSymbol.Error
                    && elemType != TypeSymbol.Error)
                {
                    // Allow append(s1, s2...) where s2 is a slice of the same element type (spread)
                    var resolvedElemType = elem.Type.Resolved();
                    bool isSpread = (elem.Type is SliceTypeSymbol elemSlice
                        && TypeChecker.IsAssignable(elemSlice.ElementType, elemType))
                        || (resolvedElemType is SliceTypeSymbol resolvedElemSlice
                        && TypeChecker.IsAssignable(resolvedElemSlice.ElementType, elemType));
                    // Allow append([]byte, string...) — special Go feature
                    bool isByteStringAppend = elemType == BuiltinTypes.Byte
                        && (elem.Type.TypeKind == TypeKind.String || elem.Type.TypeKind == TypeKind.UntypedString);
                    if (!isSpread && !isByteStringAppend)
                    {
                        _context.Errors.ReportError(_context.SpanOf(syntax.Arguments[i]), ErrorCode.TypeMismatch,
                            $"Cannot use '{elem.Type.Name}' as element type '{elemType.Name}' in append");
                    }
                }

                args.Add(elem);
            }

            var appendSymbol = new FunctionSymbol("append",
                new[] { new ParameterSymbol("slice", sliceArg.Type, 0) }, sliceArg.Type);
            return new CallExpression(appendSymbol, args, span);
        }

        public Expression ResolveMake(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count < 1)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    "make expects at least 1 argument");
                return new ErrorExpression("Wrong arg count", span);
            }

            // First argument is a type, not an expression
            var type = _typeResolver.ResolveType(syntax.Arguments[0]);
            if (type == null)
            {
                return new ErrorExpression("Invalid type", span);
            }

            var args = new List<Expression>();
            var resolved = type.Resolved();
            // Also unwrap UnderlyingType for named types (e.g. sort.StringSlice → []string)
            if (resolved == type && type.UnderlyingType != null)
                resolved = type.UnderlyingType;

            // For type parameters with structural constraints (e.g. ~[]E, ~map[K]V),
            // resolve to the constraint's structural type so make() works.
            var structural = TypeChecker.GetConstraintStructuralType(resolved);
            if (structural != null)
                resolved = structural;

            if (resolved is SliceTypeSymbol sliceType)
            {
                if (syntax.Arguments.Count < 2 || syntax.Arguments.Count > 3)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"make(slice) expects 2 or 3 arguments, got {syntax.Arguments.Count}");
                    return new ErrorExpression("Wrong arg count", span);
                }

                var lenArg = _resolveExpression(syntax.Arguments[1]);
                args.Add(lenArg);
                if (syntax.Arguments.Count == 3)
                {
                    var capArg = _resolveExpression(syntax.Arguments[2]);
                    args.Add(capArg);
                }

                var makeSymbol = new FunctionSymbol("make",
                    Array.Empty<ParameterSymbol>(), type);
                return new CallExpression(makeSymbol, args, span);
            }

            if (resolved is MapTypeSymbol mapType)
            {
                if (syntax.Arguments.Count > 2)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"make(map) expects 1 or 2 arguments, got {syntax.Arguments.Count}");
                    return new ErrorExpression("Wrong arg count", span);
                }

                if (syntax.Arguments.Count == 2)
                {
                    var hintArg = _resolveExpression(syntax.Arguments[1]);
                    args.Add(hintArg);
                }

                var makeSymbol = new FunctionSymbol("make",
                    Array.Empty<ParameterSymbol>(), type);
                return new CallExpression(makeSymbol, args, span);
            }

            if (resolved is ChannelTypeSymbol chanType)
            {
                if (syntax.Arguments.Count > 2)
                {
                    _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                        $"make(chan) expects 1 or 2 arguments, got {syntax.Arguments.Count}");
                    return new ErrorExpression("Wrong arg count", span);
                }

                if (syntax.Arguments.Count == 2)
                {
                    var capArg = _resolveExpression(syntax.Arguments[1]);
                    args.Add(capArg);
                }

                var makeSymbol = new FunctionSymbol("make",
                    Array.Empty<ParameterSymbol>(), type);
                return new CallExpression(makeSymbol, args, span);
            }

            _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                $"Cannot make type '{type.Name}'");
            return new ErrorExpression("Invalid make", span);
        }

        public Expression ResolveDelete(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count != 2)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    $"delete expects 2 arguments, got {syntax.Arguments.Count}");
                return new ErrorExpression("Wrong arg count", span);
            }

            var mapArg = _resolveExpression(syntax.Arguments[0]);
            var keyArg = _resolveExpression(syntax.Arguments[1]);

            var deleteResolved = ResolveUnderlying(mapArg.Type);
            if (mapArg.Type != TypeSymbol.Error && deleteResolved is not MapTypeSymbol)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"First argument to delete must be a map, got '{mapArg.Type.Name}'");
            }
            else if (deleteResolved is MapTypeSymbol mapType)
            {
                if (!TypeChecker.IsAssignable(keyArg.Type, mapType.KeyType) && keyArg.Type != TypeSymbol.Error)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax.Arguments[1]), ErrorCode.TypeMismatch,
                        $"Cannot use '{keyArg.Type.Name}' as key type '{mapType.KeyType.Name}' in delete");
                }
            }

            var deleteSymbol = new FunctionSymbol("delete",
                new[] { new ParameterSymbol("m", mapArg.Type, 0), new ParameterSymbol("key", keyArg.Type, 1) },
                BuiltinTypes.Void);
            return new CallExpression(deleteSymbol, new Expression[] { mapArg, keyArg }, span);
        }

        public Expression ResolvePrint(CallExpressionSyntax syntax, TextSpan span)
        {
            var args = new List<Expression>();
            for (int i = 0; i < syntax.Arguments.Count; i++)
            {
                args.Add(_resolveExpression(syntax.Arguments[i]));
            }

            var name = ((IdentifierNameSyntax)syntax.Function).Identifier.Text;
            var printSymbol = new FunctionSymbol(name,
                Array.Empty<ParameterSymbol>(), BuiltinTypes.Void);
            return new CallExpression(printSymbol, args, span);
        }

        public Expression ResolveComplex(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count != 2)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    "complex() requires exactly 2 arguments");
                return new ErrorExpression("Wrong argument count", span);
            }

            var realArg = _resolveExpression(syntax.Arguments[0]);
            var imagArg = _resolveExpression(syntax.Arguments[1]);

            // Determine result type: both float32 → complex64, else complex128
            TypeSymbol resultType;
            if (realArg.Type.TypeKind == TypeKind.Float32 && imagArg.Type.TypeKind == TypeKind.Float32)
            {
                resultType = BuiltinTypes.Complex64;
            }
            else if (TypeChecker.IsNumeric(realArg.Type) && TypeChecker.IsNumeric(imagArg.Type))
            {
                resultType = BuiltinTypes.Complex128;
            }
            else
            {
                _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                    "complex() arguments must be numeric");
                return new ErrorExpression("Type mismatch", span);
            }

            var funcSymbol = new FunctionSymbol("complex",
                Array.Empty<ParameterSymbol>(), resultType);
            return new CallExpression(funcSymbol, new List<Expression> { realArg, imagArg }, span);
        }

        public Expression ResolveReal(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count != 1)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    "real() requires exactly 1 argument");
                return new ErrorExpression("Wrong argument count", span);
            }

            var arg = _resolveExpression(syntax.Arguments[0]);

            TypeSymbol resultType;
            if (arg.Type.TypeKind == TypeKind.Complex64)
            {
                resultType = BuiltinTypes.Float32;
            }
            else if (TypeChecker.IsComplex(arg.Type))
            {
                resultType = BuiltinTypes.Float64;
            }
            else
            {
                _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                    "real() argument must be complex");
                return new ErrorExpression("Type mismatch", span);
            }

            var funcSymbol = new FunctionSymbol("real",
                Array.Empty<ParameterSymbol>(), resultType);
            return new CallExpression(funcSymbol, new List<Expression> { arg }, span);
        }

        public Expression ResolveImag(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count != 1)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    "imag() requires exactly 1 argument");
                return new ErrorExpression("Wrong argument count", span);
            }

            var arg = _resolveExpression(syntax.Arguments[0]);

            TypeSymbol resultType;
            if (arg.Type.TypeKind == TypeKind.Complex64)
            {
                resultType = BuiltinTypes.Float32;
            }
            else if (TypeChecker.IsComplex(arg.Type))
            {
                resultType = BuiltinTypes.Float64;
            }
            else
            {
                _context.Errors.ReportError(span, ErrorCode.TypeMismatch,
                    "imag() argument must be complex");
                return new ErrorExpression("Type mismatch", span);
            }

            var funcSymbol = new FunctionSymbol("imag",
                Array.Empty<ParameterSymbol>(), resultType);
            return new CallExpression(funcSymbol, new List<Expression> { arg }, span);
        }

        public Expression ResolveMin(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count < 2)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    $"min expects at least 2 arguments, got {syntax.Arguments.Count}");
                return new ErrorExpression("Wrong arg count", span);
            }

            var args = new List<Expression>();
            for (int i = 0; i < syntax.Arguments.Count; i++)
            {
                args.Add(_resolveExpression(syntax.Arguments[i]));
            }

            // Result type is the common type across all arguments
            var resultType = args[0].Type;
            for (int i = 1; i < args.Count; i++)
            {
                var common = TypeChecker.CommonType(resultType, args[i].Type);
                if (common != null) resultType = common;
            }
            if (resultType.TypeKind == TypeKind.UntypedInt) resultType = BuiltinTypes.Int;
            if (resultType.TypeKind == TypeKind.UntypedRune) resultType = BuiltinTypes.Rune;
            if (resultType.TypeKind == TypeKind.UntypedFloat) resultType = BuiltinTypes.Float64;
            if (resultType.TypeKind == TypeKind.UntypedString) resultType = BuiltinTypes.String;

            var funcSymbol = new FunctionSymbol("min",
                Array.Empty<ParameterSymbol>(), resultType);
            return new CallExpression(funcSymbol, args, span);
        }

        public Expression ResolveMax(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count < 2)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    $"max expects at least 2 arguments, got {syntax.Arguments.Count}");
                return new ErrorExpression("Wrong arg count", span);
            }

            var args = new List<Expression>();
            for (int i = 0; i < syntax.Arguments.Count; i++)
            {
                args.Add(_resolveExpression(syntax.Arguments[i]));
            }

            var resultType = args[0].Type;
            for (int i = 1; i < args.Count; i++)
            {
                var common = TypeChecker.CommonType(resultType, args[i].Type);
                if (common != null) resultType = common;
            }
            if (resultType.TypeKind == TypeKind.UntypedInt) resultType = BuiltinTypes.Int;
            if (resultType.TypeKind == TypeKind.UntypedRune) resultType = BuiltinTypes.Rune;
            if (resultType.TypeKind == TypeKind.UntypedFloat) resultType = BuiltinTypes.Float64;
            if (resultType.TypeKind == TypeKind.UntypedString) resultType = BuiltinTypes.String;

            var funcSymbol = new FunctionSymbol("max",
                Array.Empty<ParameterSymbol>(), resultType);
            return new CallExpression(funcSymbol, args, span);
        }

        public Expression ResolveClear(CallExpressionSyntax syntax, TextSpan span)
        {
            if (syntax.Arguments.Count != 1)
            {
                _context.Errors.ReportError(span, ErrorCode.WrongArgumentCount,
                    $"clear expects 1 argument, got {syntax.Arguments.Count}");
                return new ErrorExpression("Wrong arg count", span);
            }

            var arg = _resolveExpression(syntax.Arguments[0]);

            var clearResolved = ResolveUnderlying(arg.Type);
            if (arg.Type != TypeSymbol.Error
                && clearResolved is not MapTypeSymbol
                && clearResolved is not SliceTypeSymbol)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"Invalid argument type '{arg.Type.Name}' for clear");
            }

            var clearSymbol = new FunctionSymbol("clear",
                new[] { new ParameterSymbol("m", arg.Type, 0) }, BuiltinTypes.Void);
            return new CallExpression(clearSymbol, new[] { arg }, span);
        }

        private static TypeSymbol ResolveUnderlying(TypeSymbol type)
        {
            // For type parameters with structural constraints (e.g. ~[]E, ~map[K]V),
            // resolve to the constraint's structural type so builtins work correctly.
            var structural = TypeChecker.GetConstraintStructuralType(type);
            if (structural != null)
            {
                return structural;
            }

            // Unwrap chains of named types to reach the concrete type
            // (e.g. sortableSlice → []T, pallocBits → pageBits → [N]uint64)
            var current = type;
            for (int i = 0; i < 10; i++)
            {
                var resolved = current.Resolved();
                if (resolved != current)
                {
                    current = resolved;
                    continue;
                }
                if (current.UnderlyingType != null)
                {
                    current = current.UnderlyingType;
                    continue;
                }
                break;
            }
            return current;
        }
    }
}

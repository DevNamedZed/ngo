// -----------------------------------------------------------------------
// <copyright file="ExpressionResolver.cs" company="Ziad">
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
using System.Globalization;
using Ngo.Compiler.Ast;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    public sealed class ExpressionResolver
    {
        private readonly AnalysisContext _context;
        private readonly TypeResolver _typeResolver;
        private readonly CallResolver _callResolver;
        private Func<BlockSyntax, BlockStatement> _resolveBlock;

        public ExpressionResolver(AnalysisContext context, TypeResolver typeResolver, CallResolver callResolver)
        {
            _context = context;
            _typeResolver = typeResolver;
            _callResolver = callResolver;
            _resolveBlock = _ => throw new InvalidOperationException(
                "Block resolver has not been set. Call SetBlockResolver first.");
        }

        public void SetBlockResolver(Func<BlockSyntax, BlockStatement> resolveBlock)
        {
            _resolveBlock = resolveBlock;
        }

        public Expression ResolveExpression(ExpressionSyntax syntax)
        {
            switch (syntax.Kind)
            {
                case SyntaxKind.LiteralExpression:
                    return ResolveLiteral((LiteralExpressionSyntax)syntax);
                case SyntaxKind.IdentifierName:
                    return ResolveIdentifier((IdentifierNameSyntax)syntax);
                case SyntaxKind.BinaryExpression:
                    return ResolveBinaryExpression((BinaryExpressionSyntax)syntax);
                case SyntaxKind.UnaryExpression:
                    return ResolveUnaryExpression((UnaryExpressionSyntax)syntax);
                case SyntaxKind.ParenthesizedExpression:
                    return ResolveExpression(((ParenthesizedExpressionSyntax)syntax).Expression);
                case SyntaxKind.CallExpression:
                    return _callResolver.ResolveCallExpression((CallExpressionSyntax)syntax);
                case SyntaxKind.SelectorExpression:
                    return ResolveSelectorExpression((SelectorExpressionSyntax)syntax);
                case SyntaxKind.CompositeLiteral:
                    return ResolveCompositeLiteral((CompositeLiteralSyntax)syntax);
                case SyntaxKind.IndexExpression:
                    return ResolveIndexExpression((IndexExpressionSyntax)syntax);
                case SyntaxKind.SliceExpression:
                    return ResolveSliceExpression((SliceExpressionSyntax)syntax);
                case SyntaxKind.TypeAssertExpression:
                    return ResolveTypeAssertExpression((TypeAssertExpressionSyntax)syntax);
                case SyntaxKind.FunctionLiteral:
                    return ResolveFunctionLiteral((FunctionLiteralSyntax)syntax);

                // Type expressions in expression position (conversions, composite literals, type args)
                case SyntaxKind.SliceType:
                case SyntaxKind.ArrayType:
                case SyntaxKind.MapType:
                case SyntaxKind.PointerType:
                case SyntaxKind.ChannelType:
                case SyntaxKind.InterfaceType:
                case SyntaxKind.StructType:
                {
                    var resolvedType = _typeResolver.ResolveType(syntax);
                    if (resolvedType != null)
                    {
                        return new IdentifierExpression(
                            new LocalSymbol(resolvedType.Name, resolvedType),
                            resolvedType, _context.SpanOf(syntax));
                    }
                    return new ErrorExpression($"Cannot resolve type: {syntax.Kind}", _context.SpanOf(syntax));
                }

                default:
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UnsupportedSyntax,
                        $"Expression kind '{syntax.Kind}' is not yet supported");
                    return new ErrorExpression($"Unsupported: {syntax.Kind}", _context.SpanOf(syntax));
            }
        }

        private Expression ResolveFunctionLiteral(FunctionLiteralSyntax syntax)
        {
            var span = _context.SpanOf(syntax);
            var parameters = _typeResolver.ResolveParameterList(syntax.Parameters);
            var returnTypes = _typeResolver.ResolveResultTypes(syntax.Result);

            var paramTypes = new List<TypeSymbol>();
            for (int i = 0; i < parameters.Count; i++)
            {
                paramTypes.Add(parameters[i].Type);
            }

            bool isVariadic = syntax.Parameters.Parameters.Count > 0
                && syntax.Parameters.Parameters[syntax.Parameters.Parameters.Count - 1].Ellipsis != null;
            var funcType = new FunctionTypeSymbol(paramTypes, returnTypes, isVariadic);

            _context.PushScope("closure");
            var previousReturnTypes = _context.CurrentReturnTypes;
            var previousNamedReturns = _context.CurrentNamedReturns;
            _context.CurrentReturnTypes = returnTypes;

            foreach (var param in parameters)
            {
                _context.Scope.TryDeclare(param);
            }

            // Declare named return variables in the closure scope
            var namedReturns = ResolveNamedReturns(syntax.Result, returnTypes);
            _context.CurrentNamedReturns = namedReturns;
            foreach (var nr in namedReturns)
            {
                _context.Scope.TryDeclare(nr);
                _context.TrackLocal(nr, _context.SpanOf(syntax));
            }

            var body = _resolveBlock(syntax.Body);

            if (returnTypes.Count > 0 && !FlowAnalyzer.AllPathsReturn(body))
            {
                _context.Errors.ReportError(span, ErrorCode.MissingReturn,
                    "Function literal missing return at end of function");
            }

            _context.CurrentReturnTypes = previousReturnTypes;
            _context.CurrentNamedReturns = previousNamedReturns;
            _context.PopScope();

            return new FunctionLiteralExpression(parameters, returnTypes, body, funcType, span);
        }

        private IReadOnlyList<LocalSymbol> ResolveNamedReturns(
            SyntaxNode? result, IReadOnlyList<TypeSymbol> returnTypes)
        {
            if (result is ParameterListSyntax paramList && paramList.Parameters.Count > 0)
            {
                var namedReturns = new List<LocalSymbol>();
                int typeIndex = 0;
                for (int i = 0; i < paramList.Parameters.Count; i++)
                {
                    var param = paramList.Parameters[i];
                    if (param.Names.HasValue)
                    {
                        for (int j = 0; j < param.Names.Value.Count; j++)
                        {
                            var name = param.Names.Value[j].Text;
                            if (name != "_" && typeIndex < returnTypes.Count)
                                namedReturns.Add(new LocalSymbol(name, returnTypes[typeIndex]));
                            typeIndex++;
                        }
                    }
                    else
                    {
                        typeIndex++;
                    }
                }
                return namedReturns;
            }
            return System.Array.Empty<LocalSymbol>();
        }

        private Expression ResolveLiteral(LiteralExpressionSyntax syntax)
        {
            var token = syntax.Token;
            var span = _context.SpanOf(syntax);

            switch (token.Kind)
            {
                case SyntaxKind.IntLiteralToken:
                {
                    var value = ParseIntLiteral(token.Text);
                    return new LiteralExpression(value, BuiltinTypes.UntypedInt, span);
                }

                case SyntaxKind.FloatLiteralToken:
                {
                    if (double.TryParse(token.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    {
                        return new LiteralExpression(value, BuiltinTypes.UntypedFloat, span);
                    }

                    return new LiteralExpression(0.0, BuiltinTypes.UntypedFloat, span);
                }

                case SyntaxKind.StringLiteralToken:
                case SyntaxKind.RawStringLiteralToken:
                {
                    var text = token.Text;
                    if (text.Length >= 2 && text[0] == '`')
                    {
                        // Raw string literal — no escape interpretation
                        text = text.Substring(1, text.Length - 2);
                    }
                    else if (text.Length >= 2)
                    {
                        // Interpreted string literal — process escape sequences
                        text = InterpretStringEscapes(text.Substring(1, text.Length - 2));
                    }

                    return new LiteralExpression(text, BuiltinTypes.UntypedString, span);
                }

                case SyntaxKind.RuneLiteralToken:
                {
                    var text = token.Text;
                    // Strip quotes and interpret escape
                    var inner = text.Length >= 3 ? text.Substring(1, text.Length - 2) : "";
                    inner = InterpretStringEscapes(inner);
                    char value = inner.Length > 0 ? inner[0] : '\0';
                    // Go rune literals are untyped rune constants, default type is rune (int32)
                    return new LiteralExpression((long)(int)value, BuiltinTypes.UntypedRune, span);
                }

                case SyntaxKind.ImaginaryLiteralToken:
                {
                    // Strip trailing 'i' and parse the numeric part
                    var text = token.Text;
                    var numText = text.Substring(0, text.Length - 1);
                    if (double.TryParse(numText, NumberStyles.Any, CultureInfo.InvariantCulture, out var imagValue))
                    {
                        return new LiteralExpression(imagValue, BuiltinTypes.UntypedComplex, span);
                    }
                    return new LiteralExpression(0.0, BuiltinTypes.UntypedComplex, span);
                }

                default:
                    return new ErrorExpression($"Unexpected literal token: {token.Kind}", span);
            }
        }

        private Expression ResolveIdentifier(IdentifierNameSyntax syntax)
        {
            var name = syntax.Identifier.Text;
            var span = _context.SpanOf(syntax);

            // Handle predeclared identifiers
            if (name == "true")
            {
                return new LiteralExpression(true, BuiltinTypes.UntypedBool, span);
            }

            if (name == "false")
            {
                return new LiteralExpression(false, BuiltinTypes.UntypedBool, span);
            }

            if (name == "nil")
            {
                return new LiteralExpression(null, BuiltinTypes.UntypedNil, span);
            }

            if (name == "_")
            {
                var blankSymbol = new LocalSymbol("_", TypeSymbol.Error);
                return new IdentifierExpression(blankSymbol, TypeSymbol.Error, span);
            }

            if (name == "iota")
            {
                if (_context.IotaCounter < 0)
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                        "'iota' is only valid inside a const block");
                    return new ErrorExpression("iota outside const", span);
                }

                return new LiteralExpression((long)_context.IotaCounter, BuiltinTypes.UntypedInt, span);
            }

            var symbol = _context.Scope.Lookup(name);
            if (symbol == null)
            {
                _context.Errors.ReportError(span, ErrorCode.UndeclaredName,
                    $"Undefined name '{name}'");
                return new ErrorExpression($"Undefined: {name}", span);
            }

            if (symbol is LocalSymbol local && local.Name != "_" && !_context.SuppressUsageMarking)
            {
                local.IsUsed = true;
            }

            // Determine the type based on symbol kind
            var type = _context.GetSymbolType(symbol);
            return new IdentifierExpression(symbol, type, span);
        }

        private Expression ResolveBinaryExpression(BinaryExpressionSyntax syntax)
        {
            var left = ResolveExpression(syntax.Left);
            var right = ResolveExpression(syntax.Right);
            var span = _context.SpanOf(syntax);
            var op = ResolveBinaryOperator(syntax.OperatorToken.Kind);

            if (op == null)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"Unknown binary operator '{syntax.OperatorToken.Text}'");
                return new ErrorExpression($"Unknown operator: {syntax.OperatorToken.Text}", span);
            }

            var resultType = ResolveBinaryType(op.Value, left.Type, right.Type);
            if (resultType == null)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"Operator '{syntax.OperatorToken.Text}' cannot be applied to types '{left.Type.Name}' and '{right.Type.Name}'");
                return new ErrorExpression("Type error", span);
            }

            return new BinaryExpression(left, op.Value, right, resultType, span);
        }

        private BinaryOperator? ResolveBinaryOperator(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.PlusToken => BinaryOperator.Add,
                SyntaxKind.MinusToken => BinaryOperator.Subtract,
                SyntaxKind.StarToken => BinaryOperator.Multiply,
                SyntaxKind.SlashToken => BinaryOperator.Divide,
                SyntaxKind.PercentToken => BinaryOperator.Remainder,
                SyntaxKind.AmpersandToken => BinaryOperator.BitwiseAnd,
                SyntaxKind.PipeToken => BinaryOperator.BitwiseOr,
                SyntaxKind.CaretToken => BinaryOperator.BitwiseXor,
                SyntaxKind.LessThanLessThanToken => BinaryOperator.ShiftLeft,
                SyntaxKind.GreaterThanGreaterThanToken => BinaryOperator.ShiftRight,
                SyntaxKind.AmpersandCaretToken => BinaryOperator.AndNot,
                SyntaxKind.EqualsEqualsToken => BinaryOperator.Equal,
                SyntaxKind.ExclamationEqualsToken => BinaryOperator.NotEqual,
                SyntaxKind.LessThanToken => BinaryOperator.Less,
                SyntaxKind.GreaterThanToken => BinaryOperator.Greater,
                SyntaxKind.LessThanEqualsToken => BinaryOperator.LessOrEqual,
                SyntaxKind.GreaterThanEqualsToken => BinaryOperator.GreaterOrEqual,
                SyntaxKind.AmpersandAmpersandToken => BinaryOperator.LogicalAnd,
                SyntaxKind.PipePipeToken => BinaryOperator.LogicalOr,
                _ => null,
            };
        }

        private TypeSymbol? ResolveBinaryType(BinaryOperator op, TypeSymbol left, TypeSymbol right)
        {
            if (left == TypeSymbol.Error || right == TypeSymbol.Error)
            {
                return TypeSymbol.Error;
            }

            switch (op)
            {
                // Arithmetic: both numeric, result is common type
                case BinaryOperator.Add:
                    // + also works on strings
                    if (left.TypeKind == TypeKind.String || left.TypeKind == TypeKind.UntypedString)
                    {
                        if (right.TypeKind == TypeKind.String || right.TypeKind == TypeKind.UntypedString)
                        {
                            return TypeChecker.CommonType(left, right);
                        }

                        return null;
                    }

                    goto case BinaryOperator.Subtract;

                case BinaryOperator.Subtract:
                case BinaryOperator.Multiply:
                case BinaryOperator.Divide:
                    if (TypeChecker.IsNumeric(left) && TypeChecker.IsNumeric(right))
                    {
                        return TypeChecker.CommonType(left, right);
                    }

                    return null;

                case BinaryOperator.Remainder:
                case BinaryOperator.BitwiseAnd:
                case BinaryOperator.BitwiseOr:
                case BinaryOperator.BitwiseXor:
                case BinaryOperator.AndNot:
                    if (TypeChecker.IsInteger(left) && TypeChecker.IsInteger(right))
                    {
                        return TypeChecker.CommonType(left, right);
                    }

                    // Allow untyped float constants (e.g. 1e3) in integer context
                    if (TypeChecker.IsInteger(left) && right.TypeKind == TypeKind.UntypedFloat)
                        return left;
                    if (left.TypeKind == TypeKind.UntypedFloat && TypeChecker.IsInteger(right))
                        return right;

                    return null;

                case BinaryOperator.ShiftLeft:
                case BinaryOperator.ShiftRight:
                    if (TypeChecker.IsInteger(left) && TypeChecker.IsInteger(right))
                    {
                        return left;
                    }

                    return null;

                // Comparison: both comparable, result is bool
                case BinaryOperator.Equal:
                case BinaryOperator.NotEqual:
                    if (TypeChecker.CommonType(left, right) != null)
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    // Struct equality: same named type
                    if (left is StructTypeSymbol && right is StructTypeSymbol
                        && left.Name == right.Name)
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    // Array equality: same element type and length
                    if (left is ArrayTypeSymbol leftArr && right is ArrayTypeSymbol rightArr
                        && leftArr.Length == rightArr.Length
                        && TypeChecker.CommonType(leftArr.ElementType, rightArr.ElementType) != null)
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    // Nil comparison: nilable types can be compared with nil
                    // Also allow nil comparison with any named type (may be interface in Go)
                    if (left.TypeKind == TypeKind.UntypedNil || right.TypeKind == TypeKind.UntypedNil)
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    // Interface comparison: interfaces can always be compared with == / !=
                    if (left is InterfaceTypeSymbol || right is InterfaceTypeSymbol)
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    // Pointer comparison: pointers of the same type can be compared
                    if (left is PointerTypeSymbol && right is PointerTypeSymbol)
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    // Channel comparison
                    if (left is ChannelTypeSymbol && right is ChannelTypeSymbol)
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    return null;

                case BinaryOperator.Less:
                case BinaryOperator.Greater:
                case BinaryOperator.LessOrEqual:
                case BinaryOperator.GreaterOrEqual:
                    // Complex numbers are not ordered in Go
                    if ((TypeChecker.IsNumeric(left) && !TypeChecker.IsComplex(left))
                        && (TypeChecker.IsNumeric(right) && !TypeChecker.IsComplex(right)))
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    if ((left.TypeKind == TypeKind.String || left.TypeKind == TypeKind.UntypedString) &&
                        (right.TypeKind == TypeKind.String || right.TypeKind == TypeKind.UntypedString))
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    return null;

                // Logical: both bool, result is bool
                case BinaryOperator.LogicalAnd:
                case BinaryOperator.LogicalOr:
                    if ((left.TypeKind == TypeKind.Bool || left.TypeKind == TypeKind.UntypedBool) &&
                        (right.TypeKind == TypeKind.Bool || right.TypeKind == TypeKind.UntypedBool))
                    {
                        return BuiltinTypes.UntypedBool;
                    }

                    return null;

                default:
                    return null;
            }
        }

        private Expression ResolveUnaryExpression(UnaryExpressionSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            // Address-of: &x
            if (syntax.OperatorToken.Kind == SyntaxKind.AmpersandToken)
            {
                var operand = ResolveExpression(syntax.Operand);
                if (operand.Type == TypeSymbol.Error)
                {
                    return operand;
                }

                // Operand must be addressable (identifier, selector, index, or composite literal)
                if (operand is not IdentifierExpression && operand is not SelectorExpression
                    && operand is not CompositeLiteralExpression && operand is not IndexExpression
                    && operand is not DerefExpression)
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidAddressOf,
                        "Cannot take address of expression");
                    return new ErrorExpression("Invalid address-of", span);
                }

                var pointerType = new PointerTypeSymbol(operand.Type);
                return new AddressOfExpression(operand, pointerType, span);
            }

            // Dereference: *p
            if (syntax.OperatorToken.Kind == SyntaxKind.StarToken)
            {
                var operand = ResolveExpression(syntax.Operand);
                if (operand.Type == TypeSymbol.Error)
                {
                    return operand;
                }

                if (operand.Type is not PointerTypeSymbol pointerType)
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                        $"Cannot dereference non-pointer type '{operand.Type.Name}'");
                    return new ErrorExpression("Invalid deref", span);
                }

                return new DerefExpression(operand, pointerType.ElementType, span);
            }

            // Receive: <-ch
            if (syntax.OperatorToken.Kind == SyntaxKind.LessThanMinusToken)
            {
                var operand = ResolveExpression(syntax.Operand);
                if (operand.Type == TypeSymbol.Error)
                {
                    // Still create a ReceiveExpression so select case handlers
                    // recognize this as a receive operation even when the channel
                    // expression has errors.
                    return new ReceiveExpression(operand, TypeSymbol.Error, span);
                }

                if (operand.Type is not ChannelTypeSymbol chanType)
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                        $"Cannot receive from non-channel type '{operand.Type.Name}'");
                    return new ReceiveExpression(operand, TypeSymbol.Error, span);
                }

                return new ReceiveExpression(operand, chanType.ElementType, span);
            }

            var boundOperand = ResolveExpression(syntax.Operand);
            var op = ResolveUnaryOperator(syntax.OperatorToken.Kind);

            if (op == null)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"Unknown unary operator '{syntax.OperatorToken.Text}'");
                return new ErrorExpression($"Unknown operator: {syntax.OperatorToken.Text}", span);
            }

            var resultType = ResolveUnaryType(op.Value, boundOperand.Type);
            if (resultType == null)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"Operator '{syntax.OperatorToken.Text}' cannot be applied to type '{boundOperand.Type.Name}'");
                return new ErrorExpression("Type error", span);
            }

            return new UnaryExpression(op.Value, boundOperand, resultType, span);
        }

        private UnaryOperator? ResolveUnaryOperator(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.MinusToken => UnaryOperator.Negate,
                SyntaxKind.PlusToken => UnaryOperator.Plus,
                SyntaxKind.CaretToken => UnaryOperator.BitwiseNot,
                SyntaxKind.ExclamationToken => UnaryOperator.LogicalNot,
                _ => null,
            };
        }

        private TypeSymbol? ResolveUnaryType(UnaryOperator op, TypeSymbol operand)
        {
            if (operand == TypeSymbol.Error)
            {
                return TypeSymbol.Error;
            }

            switch (op)
            {
                case UnaryOperator.Negate:
                case UnaryOperator.Plus:
                    return TypeChecker.IsNumeric(operand) ? operand : null;

                case UnaryOperator.BitwiseNot:
                    return TypeChecker.IsInteger(operand) ? operand : null;

                case UnaryOperator.LogicalNot:
                    return (operand.TypeKind == TypeKind.Bool || operand.TypeKind == TypeKind.UntypedBool)
                        ? operand : null;

                default:
                    return null;
            }
        }

        private Expression ResolveSelectorExpression(SelectorExpressionSyntax syntax)
        {
            var target = ResolveExpression(syntax.Expression);
            var span = _context.SpanOf(syntax);
            var fieldName = syntax.Name.Text;

            // Package member access: pkg.Name
            if (target is IdentifierExpression idExpr && idExpr.Symbol is PackageSymbol pkg)
            {
                _context.UsedPackages.Add(pkg.Name);
                var export = pkg.LookupExport(fieldName);
                if (export == null)
                {
                    _context.Errors.ReportError(span, ErrorCode.UndeclaredName,
                        $"Package '{pkg.Name}' has no exported member '{fieldName}'");
                    return new ErrorExpression($"Undefined: {pkg.Name}.{fieldName}", span);
                }

                var exportType = _context.GetSymbolType(export);
                return new IdentifierExpression(export, exportType, span);
            }

            if (target.Type == TypeSymbol.Error)
            {
                return new ErrorExpression("Error target", span);
            }

            // Method expression: Type.Method → func(receiver, args...) returns
            if (target is IdentifierExpression typeId && typeId.Symbol is TypeSymbol typeSymbol
                && typeSymbol is StructTypeSymbol methodExprStruct)
            {
                var method = methodExprStruct.LookupMethod(fieldName);
                if (method != null)
                {
                    // Include receiver type as first parameter
                    var paramTypes = new TypeSymbol[method.Parameters.Count + 1];
                    paramTypes[0] = method.IsPointerReceiver
                        ? new PointerTypeSymbol(typeSymbol) : typeSymbol;
                    for (int i = 0; i < method.Parameters.Count; i++)
                        paramTypes[i + 1] = method.Parameters[i].Type;
                    var funcType = new FunctionTypeSymbol(paramTypes, method.ReturnTypes);
                    return new MethodValueExpression(target, method, funcType, span,
                        isMethodExpression: true);
                }

                // Check promoted methods from embedded structs
                var promoted = methodExprStruct.LookupPromotedMethod(fieldName);
                if (promoted.HasValue)
                {
                    var (_, promotedMethod) = promoted.Value;
                    var paramTypes = new TypeSymbol[promotedMethod.Parameters.Count + 1];
                    paramTypes[0] = typeSymbol;
                    for (int i = 0; i < promotedMethod.Parameters.Count; i++)
                        paramTypes[i + 1] = promotedMethod.Parameters[i].Type;
                    var funcType = new FunctionTypeSymbol(paramTypes, promotedMethod.ReturnTypes);
                    return new MethodValueExpression(target, promotedMethod, funcType, span,
                        isMethodExpression: true);
                }
            }

            var targetType = target.Type;

            // Auto-deref: if target is a pointer to a struct, dereference it
            if (targetType is PointerTypeSymbol pointerType)
            {
                targetType = pointerType.ElementType;
                target = new DerefExpression(target, targetType, target.Span);
            }

            // Unwrap named type to access underlying struct/slice/etc fields and methods
            var resolvedTargetType = targetType.Resolved();

            if (resolvedTargetType is StructTypeSymbol structType)
            {
                var field = structType.LookupField(fieldName);
                if (field != null)
                {
                    return new SelectorExpression(target, field, field.Type, span);
                }

                // Check promoted fields from embedded structs
                var promoted = structType.LookupPromotedField(fieldName);
                if (promoted.HasValue)
                {
                    var (embeddedField, innerField) = promoted.Value;
                    var embeddedAccess = new SelectorExpression(target, embeddedField, embeddedField.Type, span);
                    return new SelectorExpression(embeddedAccess, innerField, innerField.Type, span);
                }

                // Method value: check named type methods first, then underlying struct methods
                var method = targetType.LookupMethod(fieldName)
                    ?? structType.LookupMethod(fieldName);
                if (method != null)
                {
                    var paramTypes = new TypeSymbol[method.Parameters.Count];
                    for (int i = 0; i < method.Parameters.Count; i++)
                        paramTypes[i] = method.Parameters[i].Type;
                    var funcType = new FunctionTypeSymbol(paramTypes, method.ReturnTypes);
                    return new MethodValueExpression(target, method, funcType, span);
                }

                // Check promoted methods from embedded structs
                var promotedMethod = structType.LookupPromotedMethod(fieldName);
                if (promotedMethod.HasValue)
                {
                    var (_, pm) = promotedMethod.Value;
                    var paramTypes = new TypeSymbol[pm.Parameters.Count];
                    for (int i = 0; i < pm.Parameters.Count; i++)
                        paramTypes[i] = pm.Parameters[i].Type;
                    var funcType = new FunctionTypeSymbol(paramTypes, pm.ReturnTypes);
                    return new MethodValueExpression(target, pm, funcType, span);
                }

                _context.Errors.ReportError(span, ErrorCode.UndefinedField,
                    $"Type '{structType.Name}' has no field or method '{fieldName}'");
                return new ErrorExpression($"Undefined field: {fieldName}", span);
            }

            // Method value on non-struct types (including interfaces)
            var typeMethod = targetType.LookupMethod(fieldName);

            // Also check resolved type for methods (handles named type aliases)
            if (typeMethod == null && resolvedTargetType != targetType)
            {
                typeMethod = resolvedTargetType.LookupMethod(fieldName);
            }

            if (typeMethod != null)
            {
                var paramTypes = new TypeSymbol[typeMethod.Parameters.Count];
                for (int i = 0; i < typeMethod.Parameters.Count; i++)
                    paramTypes[i] = typeMethod.Parameters[i].Type;
                var funcType = new FunctionTypeSymbol(paramTypes, typeMethod.ReturnTypes);
                return new MethodValueExpression(target, typeMethod, funcType, span);
            }

            _context.Errors.ReportError(span, ErrorCode.InvalidSelector,
                $"Type '{target.Type.Name}' does not support field access");
            return new ErrorExpression("Invalid selector", span);
        }

        private Expression ResolveCompositeLiteral(CompositeLiteralSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            if (syntax.Type == null)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidCompositeLiteral,
                    "Composite literal requires a type");
                return new ErrorExpression("Missing type", span);
            }

            var type = _typeResolver.ResolveType(syntax.Type);
            if (type == null)
            {
                return new ErrorExpression("Invalid type", span);
            }

            // Check the type directly, or its underlying type for named types
            var resolvedType = type.Resolved();

            if (resolvedType is StructTypeSymbol structType)
            {
                return ResolveStructCompositeLiteral(structType, syntax, span, type);
            }

            if (resolvedType is SliceTypeSymbol sliceType)
            {
                return ResolveSliceCompositeLiteral(sliceType, syntax, span, type);
            }

            if (resolvedType is ArrayTypeSymbol arrayType)
            {
                return ResolveArrayCompositeLiteral(arrayType, syntax, span, type);
            }

            if (resolvedType is MapTypeSymbol mapType)
            {
                return ResolveMapCompositeLiteral(mapType, syntax, span, type);
            }

            _context.Errors.ReportError(span, ErrorCode.InvalidCompositeLiteral,
                $"Type '{type.Name}' does not support composite literals");
            return new ErrorExpression("Invalid composite literal", span);
        }

        private Expression ResolveStructCompositeLiteral(StructTypeSymbol structType,
            CompositeLiteralSyntax syntax, TextSpan span, TypeSymbol namedType)
        {
            var initializers = new List<FieldInitializer>();

            if (syntax.Elements.Count == 0)
            {
                // Empty literal: Point{} — zero value
                return new CompositeLiteralExpression(namedType, initializers, span);
            }

            // Determine if keyed or positional by checking the first element
            bool isKeyed = syntax.Elements[0] is KeyValueExpressionSyntax;

            if (isKeyed)
            {
                for (int i = 0; i < syntax.Elements.Count; i++)
                {
                    var element = syntax.Elements[i];
                    if (element is not KeyValueExpressionSyntax kvSyntax)
                    {
                        _context.Errors.ReportError(_context.SpanOf(element), ErrorCode.InvalidCompositeLiteral,
                            "Cannot mix keyed and positional fields in struct literal");
                        return new ErrorExpression("Mixed fields", span);
                    }

                    // Key must be an identifier (field name)
                    if (kvSyntax.Key is not IdentifierNameSyntax keyId)
                    {
                        _context.Errors.ReportError(_context.SpanOf(kvSyntax.Key), ErrorCode.InvalidCompositeLiteral,
                            "Field name must be an identifier");
                        return new ErrorExpression("Invalid field name", span);
                    }

                    var field = structType.LookupField(keyId.Identifier.Text);
                    if (field == null)
                    {
                        _context.Errors.ReportError(_context.SpanOf(kvSyntax.Key), ErrorCode.UndefinedField,
                            $"Type '{structType.Name}' has no field '{keyId.Identifier.Text}'");
                        return new ErrorExpression("Undefined field", span);
                    }

                    var value = ResolveElementWithHint(kvSyntax.Value, field.Type);
                    if (!TypeChecker.IsAssignable(value.Type, field.Type))
                    {
                        _context.Errors.ReportError(_context.SpanOf(kvSyntax.Value), ErrorCode.TypeMismatch,
                            $"Cannot assign '{value.Type.Name}' to field '{field.Name}' of type '{field.Type.Name}'");
                    }

                    initializers.Add(new FieldInitializer(field, value));
                }
            }
            else
            {
                // Positional: match elements to fields by ordinal
                if (syntax.Elements.Count != structType.Fields.Count)
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidCompositeLiteral,
                        $"Too {(syntax.Elements.Count < structType.Fields.Count ? "few" : "many")} values in struct literal (expected {structType.Fields.Count}, got {syntax.Elements.Count})");
                    return new ErrorExpression("Wrong field count", span);
                }

                for (int i = 0; i < syntax.Elements.Count; i++)
                {
                    var element = syntax.Elements[i];
                    if (element is KeyValueExpressionSyntax)
                    {
                        _context.Errors.ReportError(_context.SpanOf(element), ErrorCode.InvalidCompositeLiteral,
                            "Cannot mix keyed and positional fields in struct literal");
                        return new ErrorExpression("Mixed fields", span);
                    }

                    var field = structType.Fields[i];
                    var value = ResolveElementWithHint(element, field.Type);

                    if (!TypeChecker.IsAssignable(value.Type, field.Type))
                    {
                        _context.Errors.ReportError(_context.SpanOf(element), ErrorCode.TypeMismatch,
                            $"Cannot assign '{value.Type.Name}' to field '{field.Name}' of type '{field.Type.Name}'");
                    }

                    initializers.Add(new FieldInitializer(field, value));
                }
            }

            return new CompositeLiteralExpression(namedType, initializers, span);
        }

        private Expression ResolveElementWithHint(ExpressionSyntax element, TypeSymbol elementType)
        {
            // Handle composite literal type elision: []T{{a, b}} → inner {} gets type T
            if (element is CompositeLiteralSyntax innerLit && innerLit.Type == null)
            {
                var resolvedElemType = elementType.UnderlyingType ?? elementType;
                var innerSpan = _context.SpanOf(element);

                if (resolvedElemType is StructTypeSymbol structType)
                    return ResolveStructCompositeLiteral(structType, innerLit, innerSpan, elementType);
                if (resolvedElemType is SliceTypeSymbol sliceElem)
                    return ResolveSliceCompositeLiteral(sliceElem, innerLit, innerSpan, elementType);
                if (resolvedElemType is ArrayTypeSymbol arrayElem)
                    return ResolveArrayCompositeLiteral(arrayElem, innerLit, innerSpan, elementType);
                if (resolvedElemType is MapTypeSymbol mapElem)
                    return ResolveMapCompositeLiteral(mapElem, innerLit, innerSpan, elementType);
            }

            return ResolveExpression(element);
        }

        private Expression ResolveSliceCompositeLiteral(SliceTypeSymbol sliceType,
            CompositeLiteralSyntax syntax, TextSpan span, TypeSymbol namedType)
        {
            var elements = new List<ElementInitializer>();
            int nextIndex = 0;
            int maxIndex = -1;
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < syntax.Elements.Count; i++)
            {
                var element = syntax.Elements[i];
                int index;
                Expression value;

                if (element is KeyValueExpressionSyntax kv)
                {
                    var keyExpr = ResolveExpression(kv.Key);
                    var keyVal = _context.TryEvaluateConstant(keyExpr);
                    if (keyVal is not long keyLong || keyLong < 0)
                    {
                        _context.Errors.ReportError(_context.SpanOf(kv.Key), ErrorCode.InvalidIndex,
                            "Index in composite literal must be a non-negative integer constant");
                        return new ErrorExpression("Invalid key", span);
                    }
                    index = (int)keyLong;
                    value = ResolveElementWithHint(kv.Value, sliceType.ElementType);
                    nextIndex = index + 1;
                }
                else
                {
                    index = nextIndex;
                    value = ResolveElementWithHint(element, sliceType.ElementType);
                    nextIndex++;
                }

                if (!usedIndices.Add(index))
                {
                    _context.Errors.ReportError(_context.SpanOf(element), ErrorCode.InvalidCompositeLiteral,
                        $"Duplicate index {index} in slice literal");
                    return new ErrorExpression("Duplicate index", span);
                }

                if (!TypeChecker.IsAssignable(value.Type, sliceType.ElementType))
                {
                    _context.Errors.ReportError(_context.SpanOf(element), ErrorCode.TypeMismatch,
                        $"Cannot use '{value.Type.Name}' as element type '{sliceType.ElementType.Name}' in slice literal");
                }

                if (index > maxIndex) maxIndex = index;
                var keyLit = new LiteralExpression(index, BuiltinTypes.UntypedInt, _context.SpanOf(element));
                elements.Add(new ElementInitializer(keyLit, value));
            }

            return new CompositeLiteralExpression(namedType, elements, span);
        }

        private Expression ResolveArrayCompositeLiteral(ArrayTypeSymbol arrayType,
            CompositeLiteralSyntax syntax, TextSpan span, TypeSymbol namedType)
        {
            bool hasKeys = false;
            for (int i = 0; i < syntax.Elements.Count; i++)
            {
                if (syntax.Elements[i] is KeyValueExpressionSyntax)
                {
                    hasKeys = true;
                    break;
                }
            }

            var elements = new List<ElementInitializer>();
            int nextIndex = 0;
            int maxIndex = -1;
            var usedIndices = new HashSet<int>();

            for (int i = 0; i < syntax.Elements.Count; i++)
            {
                var element = syntax.Elements[i];
                int index;
                Expression value;

                if (element is KeyValueExpressionSyntax kv)
                {
                    var keyExpr = ResolveExpression(kv.Key);
                    var keyVal = _context.TryEvaluateConstant(keyExpr);
                    if (keyVal is not long keyLong || keyLong < 0)
                    {
                        _context.Errors.ReportError(_context.SpanOf(kv.Key), ErrorCode.InvalidIndex,
                            "Index in composite literal must be a non-negative integer constant");
                        return new ErrorExpression("Invalid key", span);
                    }
                    index = (int)keyLong;
                    value = ResolveElementWithHint(kv.Value, arrayType.ElementType);
                    nextIndex = index + 1;
                }
                else
                {
                    index = nextIndex;
                    value = ResolveElementWithHint(element, arrayType.ElementType);
                    nextIndex++;
                }

                if (!usedIndices.Add(index))
                {
                    _context.Errors.ReportError(_context.SpanOf(element), ErrorCode.InvalidCompositeLiteral,
                        $"Duplicate index {index} in array literal");
                    return new ErrorExpression("Duplicate index", span);
                }

                if (!TypeChecker.IsAssignable(value.Type, arrayType.ElementType))
                {
                    _context.Errors.ReportError(_context.SpanOf(element), ErrorCode.TypeMismatch,
                        $"Cannot use '{value.Type.Name}' as element type '{arrayType.ElementType.Name}' in array literal");
                }

                if (index > maxIndex) maxIndex = index;
                var keyLit = new LiteralExpression(index, BuiltinTypes.UntypedInt, _context.SpanOf(element));
                elements.Add(new ElementInitializer(keyLit, value));
            }

            var finalType = arrayType;
            if (arrayType.Length == -1)
            {
                // [...]T — infer length from max index + 1
                finalType = new ArrayTypeSymbol(arrayType.ElementType, maxIndex + 1);
            }
            else if (!hasKeys && syntax.Elements.Count > arrayType.Length)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidCompositeLiteral,
                    $"Array literal has {syntax.Elements.Count} elements, exceeds array length {arrayType.Length}");
                return new ErrorExpression("Too many elements", span);
            }
            else if (hasKeys && maxIndex >= arrayType.Length)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidIndex,
                    $"Index {maxIndex} out of bounds for array of length {arrayType.Length}");
                return new ErrorExpression("Index out of bounds", span);
            }

            var resultType = namedType.UnderlyingType != null ? namedType : (TypeSymbol)finalType;
            return new CompositeLiteralExpression(resultType, elements, span);
        }

        private Expression ResolveMapCompositeLiteral(MapTypeSymbol mapType,
            CompositeLiteralSyntax syntax, TextSpan span, TypeSymbol namedType)
        {
            var elements = new List<ElementInitializer>();

            for (int i = 0; i < syntax.Elements.Count; i++)
            {
                var element = syntax.Elements[i];

                if (element is not KeyValueExpressionSyntax kvSyntax)
                {
                    _context.Errors.ReportError(_context.SpanOf(element), ErrorCode.InvalidCompositeLiteral,
                        "Map literal elements must be key:value pairs");
                    return new ErrorExpression("Not key:value", span);
                }

                var key = ResolveElementWithHint(kvSyntax.Key, mapType.KeyType);
                var value = ResolveElementWithHint(kvSyntax.Value, mapType.ValueType);

                if (!TypeChecker.IsAssignable(key.Type, mapType.KeyType))
                {
                    _context.Errors.ReportError(_context.SpanOf(kvSyntax.Key), ErrorCode.TypeMismatch,
                        $"Cannot use '{key.Type.Name}' as map key type '{mapType.KeyType.Name}'");
                }

                if (!TypeChecker.IsAssignable(value.Type, mapType.ValueType))
                {
                    _context.Errors.ReportError(_context.SpanOf(kvSyntax.Value), ErrorCode.TypeMismatch,
                        $"Cannot use '{value.Type.Name}' as map value type '{mapType.ValueType.Name}'");
                }

                elements.Add(new ElementInitializer(key, value));
            }

            return new CompositeLiteralExpression(namedType, elements, span);
        }

        private Expression ResolveIndexExpression(IndexExpressionSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            // Check if this is a generic function instantiation: Max[int]
            if (syntax.Expression is IdentifierNameSyntax idSyntax)
            {
                var symbol = _context.Scope.Lookup(idSyntax.Identifier.Text);
                if (symbol is FunctionSymbol funcSymbol && funcSymbol.IsGeneric)
                {
                    var typeArg = _typeResolver.ResolveType(syntax.Index);
                    if (typeArg == null)
                    {
                        typeArg = TypeSymbol.Error;
                    }

                    // Substitute type params in the function signature to produce a function type
                    var typeArgs = new[] { typeArg };
                    var substParams = TypeSubstituter.SubstituteParams(
                        funcSymbol.Parameters, funcSymbol.TypeParameters, typeArgs);
                    var substReturnTypes = TypeSubstituter.SubstituteTypes(
                        funcSymbol.ReturnTypes, funcSymbol.TypeParameters, typeArgs);

                    var paramTypes = new List<TypeSymbol>();
                    for (int i = 0; i < substParams.Count; i++)
                    {
                        paramTypes.Add(substParams[i].Type);
                    }

                    var funcType = new FunctionTypeSymbol(paramTypes, substReturnTypes);
                    return new IdentifierExpression(funcSymbol, funcType, span);
                }
            }

            var target = ResolveExpression(syntax.Expression);
            var index = ResolveExpression(syntax.Index);

            if (target.Type == TypeSymbol.Error)
            {
                return new ErrorExpression("Error target", span);
            }

            var resolvedTargetType = target.Type.Resolved();
            // Also unwrap named types with underlying composite types
            if (resolvedTargetType == target.Type && resolvedTargetType.UnderlyingType != null)
                resolvedTargetType = resolvedTargetType.UnderlyingType;

            // Auto-dereference pointer to array/slice: (*t)[i] when t is *[N]T or *[]T
            if (resolvedTargetType is PointerTypeSymbol ptrForIndex)
            {
                var inner = ptrForIndex.ElementType.Resolved();
                if (inner is ArrayTypeSymbol || inner is SliceTypeSymbol)
                {
                    target = new DerefExpression(target, inner, span);
                    resolvedTargetType = inner;
                }
            }

            if (resolvedTargetType is SliceTypeSymbol sliceType)
            {
                if (!TypeChecker.IsInteger(index.Type) && index.Type != TypeSymbol.Error)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax.Index), ErrorCode.InvalidIndex,
                        $"Index must be an integer, got '{index.Type.Name}'");
                }

                return new Ast.IndexExpression(target, index, sliceType.ElementType, span);
            }

            if (resolvedTargetType is ArrayTypeSymbol arrayType)
            {
                if (!TypeChecker.IsInteger(index.Type) && index.Type != TypeSymbol.Error)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax.Index), ErrorCode.InvalidIndex,
                        $"Index must be an integer, got '{index.Type.Name}'");
                }

                return new Ast.IndexExpression(target, index, arrayType.ElementType, span);
            }

            if (resolvedTargetType is MapTypeSymbol mapType)
            {
                if (!TypeChecker.IsAssignable(index.Type, mapType.KeyType) && index.Type != TypeSymbol.Error)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax.Index), ErrorCode.InvalidIndex,
                        $"Map key type mismatch: cannot use '{index.Type.Name}' as key type '{mapType.KeyType.Name}'");
                }

                return new Ast.IndexExpression(target, index, mapType.ValueType, span);
            }

            if (target.Type.TypeKind == TypeKind.String || target.Type.TypeKind == TypeKind.UntypedString)
            {
                if (!TypeChecker.IsInteger(index.Type) && index.Type != TypeSymbol.Error)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax.Index), ErrorCode.InvalidIndex,
                        $"Index must be an integer, got '{index.Type.Name}'");
                }

                return new Ast.IndexExpression(target, index, BuiltinTypes.Byte, span);
            }

            // Type parameter with union constraint: check if all type elements are indexable
            if (resolvedTargetType is TypeParameterSymbol typeParam &&
                typeParam.Constraint.TypeElements.Count > 0)
            {
                TypeSymbol? commonElement = null;
                bool allIndexable = true;
                foreach (var elem in typeParam.Constraint.TypeElements)
                {
                    var elemType = elem.Type;
                    if (elemType is ArrayTypeSymbol arrElem)
                    {
                        commonElement ??= arrElem.ElementType;
                    }
                    else if (elemType is SliceTypeSymbol sliceElem)
                    {
                        commonElement ??= sliceElem.ElementType;
                    }
                    else if (elemType is MapTypeSymbol mapElem)
                    {
                        commonElement ??= mapElem.ValueType;
                    }
                    else
                    {
                        allIndexable = false;
                        break;
                    }
                }

                if (allIndexable && commonElement != null)
                {
                    return new Ast.IndexExpression(target, index, commonElement, span);
                }
            }

            _context.Errors.ReportError(span, ErrorCode.InvalidIndex,
                $"Cannot index type '{target.Type.Name}'");
            return new ErrorExpression("Invalid index", span);
        }

        private Expression ResolveSliceExpression(SliceExpressionSyntax syntax)
        {
            var span = _context.SpanOf(syntax);
            var operand = ResolveExpression(syntax.Expression);

            if (operand.Type == TypeSymbol.Error)
            {
                return new ErrorExpression("Error operand", span);
            }

            Expression? low = syntax.Low != null ? ResolveExpression(syntax.Low) : null;
            Expression? high = syntax.High != null ? ResolveExpression(syntax.High) : null;
            Expression? max = syntax.Max != null ? ResolveExpression(syntax.Max) : null;

            if (low != null && !TypeChecker.IsInteger(low.Type) && low.Type != TypeSymbol.Error)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax.Low!), ErrorCode.InvalidSlice,
                    $"Slice index must be an integer, got '{low.Type.Name}'");
            }

            if (high != null && !TypeChecker.IsInteger(high.Type) && high.Type != TypeSymbol.Error)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax.High!), ErrorCode.InvalidSlice,
                    $"Slice index must be an integer, got '{high.Type.Name}'");
            }

            if (max != null && !TypeChecker.IsInteger(max.Type) && max.Type != TypeSymbol.Error)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax.Max!), ErrorCode.InvalidSlice,
                    $"Slice index must be an integer, got '{max.Type.Name}'");
            }

            TypeSymbol resultType;

            var resolvedOpType = operand.Type.Resolved();
            if (resolvedOpType == operand.Type && resolvedOpType.UnderlyingType != null)
                resolvedOpType = resolvedOpType.UnderlyingType;
            if (resolvedOpType is SliceTypeSymbol)
            {
                // Preserve named type: slicing `type chain []error` returns `chain`, not `[]error`
                resultType = operand.Type;
            }
            else if (resolvedOpType is ArrayTypeSymbol arrayType)
            {
                resultType = new SliceTypeSymbol(arrayType.ElementType);
            }
            else if (resolvedOpType is PointerTypeSymbol ptrType
                && ptrType.ElementType.Resolved() is ArrayTypeSymbol ptrArrayType)
            {
                // Go allows slicing a pointer to an array
                resultType = new SliceTypeSymbol(ptrArrayType.ElementType);
            }
            else if (operand.Type.TypeKind == TypeKind.String || operand.Type.TypeKind == TypeKind.UntypedString)
            {
                if (max != null)
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidSlice,
                        "3-index slice not supported on strings");
                }

                resultType = BuiltinTypes.String;
            }
            else
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidSlice,
                    $"Cannot slice type '{operand.Type.Name}'");
                return new ErrorExpression("Invalid slice", span);
            }

            return new Ast.SliceExpression(operand, low, high, max, resultType, span);
        }

        private Expression ResolveTypeAssertExpression(TypeAssertExpressionSyntax syntax)
        {
            var span = _context.SpanOf(syntax);
            var expr = ResolveExpression(syntax.Expression);

            if (expr.Type == TypeSymbol.Error)
            {
                return new ErrorExpression("Error expression", span);
            }

            // The expression must be an interface type
            if (expr.Type is not InterfaceTypeSymbol)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidTypeAssert,
                    $"Cannot type assert on non-interface type '{expr.Type.Name}'");
                return new ErrorExpression("Invalid type assert", span);
            }

            // Resolve the asserted type
            if (syntax.TypeOrKeyword is not ExpressionSyntax typeExpr)
            {
                _context.Errors.ReportError(span, ErrorCode.UnsupportedSyntax,
                    "Invalid type assertion syntax");
                return new ErrorExpression("Invalid type assert", span);
            }

            var assertedType = _typeResolver.ResolveType(typeExpr);
            if (assertedType == null)
            {
                return new ErrorExpression("Unknown type", span);
            }

            return new TypeAssertExpression(expr, assertedType, span);
        }

        private static string InterpretStringEscapes(string raw)
        {
            if (raw.IndexOf('\\') < 0) return raw;

            var sb = new System.Text.StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '\\' && i + 1 < raw.Length)
                {
                    i++;
                    switch (raw[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case '\'': sb.Append('\''); break;
                        case 'a': sb.Append('\a'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'v': sb.Append('\v'); break;
                        case 'x':
                            if (i + 2 < raw.Length)
                            {
                                sb.Append((char)System.Convert.ToInt32(raw.Substring(i + 1, 2), 16));
                                i += 2;
                            }
                            break;
                        case 'u':
                            if (i + 4 < raw.Length)
                            {
                                sb.Append((char)System.Convert.ToInt32(raw.Substring(i + 1, 4), 16));
                                i += 4;
                            }
                            break;
                        case 'U':
                            if (i + 8 < raw.Length)
                            {
                                int cp = System.Convert.ToInt32(raw.Substring(i + 1, 8), 16);
                                sb.Append(char.ConvertFromUtf32(cp));
                                i += 8;
                            }
                            break;
                        default:
                            if (raw[i] >= '0' && raw[i] <= '7')
                            {
                                int val = raw[i] - '0';
                                if (i + 1 < raw.Length && raw[i + 1] >= '0' && raw[i + 1] <= '7')
                                {
                                    val = val * 8 + (raw[++i] - '0');
                                    if (i + 1 < raw.Length && raw[i + 1] >= '0' && raw[i + 1] <= '7')
                                        val = val * 8 + (raw[++i] - '0');
                                }
                                sb.Append((char)val);
                            }
                            else
                            {
                                sb.Append('\\');
                                sb.Append(raw[i]);
                            }
                            break;
                    }
                }
                else
                {
                    sb.Append(raw[i]);
                }
            }

            return sb.ToString();
        }

        private static long ParseIntLiteral(string text)
        {
            // Strip digit separators
            var clean = text.Replace("_", "");

            if (clean.Length > 2)
            {
                char prefix = clean[1];
                if (prefix == 'x' || prefix == 'X')
                    return unchecked((long)Convert.ToUInt64(clean.Substring(2), 16));
                if (prefix == 'b' || prefix == 'B')
                    return unchecked((long)Convert.ToUInt64(clean.Substring(2), 2));
                if (prefix == 'o' || prefix == 'O')
                    return unchecked((long)Convert.ToUInt64(clean.Substring(2), 8));
            }

            // Legacy octal: 0777
            if (clean.Length > 1 && clean[0] == '0')
            {
                bool allOctal = true;
                for (int i = 0; i < clean.Length; i++)
                {
                    if (clean[i] < '0' || clean[i] > '7') { allOctal = false; break; }
                }
                if (allOctal)
                    return unchecked((long)Convert.ToUInt64(clean, 8));
            }

            if (ulong.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var uval))
                return unchecked((long)uval);
            return long.Parse(clean, NumberStyles.Any, CultureInfo.InvariantCulture);
        }
    }
}

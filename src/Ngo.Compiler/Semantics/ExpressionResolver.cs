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
                case SyntaxKind.FunctionType:
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

                case SyntaxKind.TypeArgumentList:
                    return ResolveTypeArgumentListExpression((TypeArgumentListSyntax)syntax);

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
                if (nr.Name != "_")
                {
                    _context.Scope.TryDeclare(nr);
                    _context.TrackLocal(nr, _context.SpanOf(syntax));
                }
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
                if (_context.IotaCounter >= 0)
                {
                    return new LiteralExpression((long)_context.IotaCounter, BuiltinTypes.UntypedInt, span);
                }

                // Outside a const block, 'iota' might be a regular variable name —
                // fall through to normal scope lookup
            }

            var symbol = _context.Scope.Lookup(name);
            if (symbol == null)
            {
                // Check if this is a builtin type name used as an expression
                // (e.g., 'any' used in new(any), map[any]T, etc.)
                var builtinType = BuiltinTypes.Resolve(name);
                if (builtinType != null)
                {
                    return new IdentifierExpression(builtinType, builtinType, span);
                }

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

            // Type parameters and pointer-to-type-parameters: allow operations based on constraints.
            // In generic code, *T may be used with comparisons when T is a numeric type.
            bool leftIsTypeParam = left is TypeParameterSymbol
                || (left is PointerTypeSymbol lp && lp.ElementType is TypeParameterSymbol)
                || (left.Name.StartsWith("*") && left.Name.Length <= 3); // *T, *K, *V etc.
            bool rightIsTypeParam = right is TypeParameterSymbol
                || (right is PointerTypeSymbol rp && rp.ElementType is TypeParameterSymbol)
                || (right.Name.StartsWith("*") && right.Name.Length <= 3);
            if (leftIsTypeParam || rightIsTypeParam)
            {
                TypeParameterSymbol? tp = (left as TypeParameterSymbol)
                    ?? (right as TypeParameterSymbol)
                    ?? ((left as PointerTypeSymbol)?.ElementType as TypeParameterSymbol)
                    ?? ((right as PointerTypeSymbol)?.ElementType as TypeParameterSymbol);
                switch (op)
                {
                    case BinaryOperator.Equal:
                    case BinaryOperator.NotEqual:
                        // comparable constraint or any type param (Go allows == if constrained)
                        return BuiltinTypes.UntypedBool;
                    case BinaryOperator.Less:
                    case BinaryOperator.Greater:
                    case BinaryOperator.LessOrEqual:
                    case BinaryOperator.GreaterOrEqual:
                        // ordered constraints
                        return BuiltinTypes.UntypedBool;
                    case BinaryOperator.LogicalAnd:
                    case BinaryOperator.LogicalOr:
                        return BuiltinTypes.UntypedBool;
                    default:
                        // Arithmetic/bitwise: return the type parameter type
                        return tp ?? left;
                }
            }

            // interface{} fallback: when a selector returns interface{} (unresolved package method),
            // allow all operators — the concrete type is unknown at compile time.
            if (left is InterfaceTypeSymbol || right is InterfaceTypeSymbol)
            {
                var iface = (left as InterfaceTypeSymbol) ?? (right as InterfaceTypeSymbol)!;
                switch (op)
                {
                    case BinaryOperator.Equal:
                    case BinaryOperator.NotEqual:
                    case BinaryOperator.Less:
                    case BinaryOperator.Greater:
                    case BinaryOperator.LessOrEqual:
                    case BinaryOperator.GreaterOrEqual:
                    case BinaryOperator.LogicalAnd:
                    case BinaryOperator.LogicalOr:
                        return BuiltinTypes.UntypedBool;
                    default:
                        return iface;
                }
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

                    // Go allows untyped float constants in shift (e.g. 1.0 << 10)
                    // when the float is representable as an integer
                    if (left.TypeKind == TypeKind.UntypedFloat && TypeChecker.IsInteger(right))
                    {
                        return BuiltinTypes.UntypedInt;
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

                    // Pointer compared to int/uintptr (unsafe pointer arithmetic)
                    if ((left is PointerTypeSymbol || left.TypeKind == TypeKind.Uintptr
                        || left.Name.StartsWith("*"))
                        && (TypeChecker.IsNumeric(right) || right.TypeKind == TypeKind.UntypedInt))
                    {
                        return BuiltinTypes.UntypedBool;
                    }
                    if ((right is PointerTypeSymbol || right.TypeKind == TypeKind.Uintptr
                        || right.Name.StartsWith("*"))
                        && (TypeChecker.IsNumeric(left) || left.TypeKind == TypeKind.UntypedInt))
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
                        // Named bool types: preserve the named type (e.g., boolVal && boolVal → boolVal)
                        if (left.TypeKind == TypeKind.Bool && left == right)
                            return left;
                        if (left.TypeKind == TypeKind.Bool && right.TypeKind == TypeKind.UntypedBool)
                            return left;
                        if (right.TypeKind == TypeKind.Bool && left.TypeKind == TypeKind.UntypedBool)
                            return right;
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

            // Dereference: *p — or pointer type expression: (*T)
            if (syntax.OperatorToken.Kind == SyntaxKind.StarToken)
            {
                var operand = ResolveExpression(syntax.Operand);
                if (operand.Type == TypeSymbol.Error)
                {
                    return operand;
                }

                // Method expression: (*T).Method — operand is a type, not a value
                // In Go, (*T) in expression context means "pointer to type T"
                if (operand is IdentifierExpression typeExpr && typeExpr.Symbol is TypeSymbol ptrTargetType)
                {
                    var ptrType = new PointerTypeSymbol(ptrTargetType);
                    return new IdentifierExpression(ptrType, ptrType, span);
                }

                var derefType = operand.Type;
                if (derefType.IsAlias && derefType.UnderlyingType != null)
                {
                    derefType = derefType.UnderlyingType;
                }
                if (derefType is PointerTypeSymbol pointerType)
                {
                    return new DerefExpression(operand, pointerType.ElementType, span);
                }
                if (derefType.Resolved() is PointerTypeSymbol resolvedPtrType)
                {
                    return new DerefExpression(operand, resolvedPtrType.ElementType, span);
                }

                // Type parameter with pointer constraint: *P where P ~*T
                if (operand.Type is TypeParameterSymbol tpDeref)
                {
                    foreach (var te in tpDeref.Constraint.TypeElements)
                    {
                        if (te.Type is PointerTypeSymbol constraintPtr)
                            return new DerefExpression(operand, constraintPtr.ElementType, span);
                    }
                }

                // Workaround: allow *interface{} when generic types (e.g. atomic.Pointer[T])
                // resolve Load() as interface{} instead of *T
                if (operand.Type is InterfaceTypeSymbol ifaceDeref && ifaceDeref.Methods.Count == 0)
                {
                    return new DerefExpression(operand, BuiltinTypes.EmptyInterface, span);
                }

                // Allow dereferencing struct types backed by classes (reference types).
                // Go functions like sync.NewCond return *Cond, but runtime returns Cond (class).
                // Dereferencing a class-backed struct is a no-op.
                if (operand.Type is StructTypeSymbol || operand.Type.TypeKind == TypeKind.Struct)
                {
                    return new DerefExpression(operand, operand.Type, span);
                }

                _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                    $"Cannot dereference non-pointer type '{operand.Type.Name}'");
                return new ErrorExpression("Invalid deref", span);
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

                // Unwrap named channel types (e.g. type control chan bool)
                var chanResolved = operand.Type;
                for (int depth = 0; depth < 10 && chanResolved is not ChannelTypeSymbol; depth++)
                {
                    if (chanResolved.UnderlyingType != null && chanResolved.UnderlyingType != chanResolved)
                        chanResolved = chanResolved.UnderlyingType;
                    else
                        break;
                }
                if (chanResolved is not ChannelTypeSymbol chanType)
                {
                    // Workaround: allow <-interface{} when generic types resolve
                    // channel types as interface{} (e.g. atomic.Pointer[chan T])
                    if (chanResolved is InterfaceTypeSymbol ifaceRecv && ifaceRecv.Methods.Count == 0)
                    {
                        return new ReceiveExpression(operand, BuiltinTypes.EmptyInterface, span);
                    }
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

            // Type parameters: allow operations based on constraints
            if (operand is TypeParameterSymbol)
            {
                switch (op)
                {
                    case UnaryOperator.Negate:
                    case UnaryOperator.Plus:
                    case UnaryOperator.BitwiseNot:
                    case UnaryOperator.LogicalNot:
                        return operand;
                    default:
                        return null;
                }
            }

            // interface{} fallback: when a selector returns interface{} (unresolved package method),
            // allow all operators — the concrete type is unknown at compile time.
            if (operand is InterfaceTypeSymbol)
                return operand;

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

            if (target.Type == TypeSymbol.Error || target.Type.TypeKind == TypeKind.Error
                || target.Type == BuiltinTypes.Void)
            {
                return new ErrorExpression("Error target", span);
            }

            // Method expression: Type.Method → func(receiver, args...) returns
            if (target is IdentifierExpression typeId && typeId.Symbol is TypeSymbol typeSymbol)
            {
                // (*T).Method — pointer type method expression
                StructTypeSymbol methodExprStruct = null;
                TypeSymbol receiverType = typeSymbol;
                if (typeSymbol is PointerTypeSymbol ptrTypeExpr)
                {
                    var baseType = ptrTypeExpr.ElementType;
                    while (baseType.UnderlyingType != null && baseType is not StructTypeSymbol)
                        baseType = baseType.UnderlyingType;
                    if (baseType is StructTypeSymbol ptrStruct)
                        methodExprStruct = ptrStruct;
                }
                else if (typeSymbol is StructTypeSymbol directStruct)
                {
                    methodExprStruct = directStruct;
                }

                if (methodExprStruct != null)
                {
                    var method = methodExprStruct.LookupMethod(fieldName);
                    if (method != null)
                    {
                        // Include receiver type as first parameter
                        var paramTypes = new TypeSymbol[method.Parameters.Count + 1];
                        paramTypes[0] = (typeSymbol is PointerTypeSymbol || method.IsPointerReceiver)
                            ? (typeSymbol is PointerTypeSymbol ? typeSymbol : new PointerTypeSymbol(typeSymbol))
                            : typeSymbol;
                        for (int i = 0; i < method.Parameters.Count; i++)
                            paramTypes[i + 1] = method.Parameters[i].Type;
                        var funcType = new FunctionTypeSymbol(paramTypes, method.ReturnTypes, method.IsVariadic);
                        return new MethodValueExpression(target, method, funcType, span,
                            isMethodExpression: true);
                    }

                    // Check promoted methods from embedded structs
                    var promoted = methodExprStruct.LookupPromotedMethod(fieldName);
                    if (promoted != null)
                    {
                        var promotedMethod = promoted.Method;
                        var paramTypes = new TypeSymbol[promotedMethod.Parameters.Count + 1];
                        paramTypes[0] = receiverType;
                        for (int i = 0; i < promotedMethod.Parameters.Count; i++)
                            paramTypes[i + 1] = promotedMethod.Parameters[i].Type;
                        var funcType = new FunctionTypeSymbol(paramTypes, promotedMethod.ReturnTypes, promotedMethod.IsVariadic);
                        return new MethodValueExpression(target, promotedMethod, funcType, span,
                            isMethodExpression: true);
                    }
                }
            }

            var targetType = target.Type;

            // Auto-deref: if target is a pointer to a struct, dereference it
            if (targetType is PointerTypeSymbol pointerType)
            {
                targetType = pointerType.ElementType;
                target = new DerefExpression(target, targetType, target.Span);
            }

            // After deref, check for error sentinel again (pointer to error type)
            if (targetType == TypeSymbol.Error || targetType.TypeKind == TypeKind.Error
                || targetType == BuiltinTypes.Void)
            {
                return new ErrorExpression("Error target", span);
            }

            // Unwrap named type to access underlying struct/slice/etc fields and methods
            var resolvedTargetType = targetType.Resolved();
            if (resolvedTargetType == targetType && resolvedTargetType.UnderlyingType != null
                && resolvedTargetType.GetType() == typeof(TypeSymbol))
            {
                resolvedTargetType = resolvedTargetType.UnderlyingType;
            }

            // Extract type parameter substitution info from instantiated generic types
            IReadOnlyList<TypeParameterSymbol>? instTypeParams = null;
            IReadOnlyList<TypeSymbol>? instTypeArgs = null;
            if (targetType is InstantiatedTypeSymbol inst)
            {
                instTypeParams = inst.GenericType.TypeParameters;
                instTypeArgs = inst.TypeArguments;
            }

            if (resolvedTargetType is StructTypeSymbol structType)
            {
                var field = structType.LookupField(fieldName);
                if (field != null)
                {
                    var fieldType = instTypeParams != null
                        ? TypeSubstituter.Substitute(field.Type, instTypeParams, instTypeArgs!)
                        : field.Type;
                    return new SelectorExpression(target, field, fieldType, span);
                }

                // Check promoted fields from embedded structs
                var promoted = structType.LookupPromotedField(fieldName);
                if (promoted != null)
                {
                    var embeddedType = instTypeParams != null
                        ? TypeSubstituter.Substitute(promoted.EmbeddedField.Type, instTypeParams, instTypeArgs!)
                        : promoted.EmbeddedField.Type;
                    var innerType = instTypeParams != null
                        ? TypeSubstituter.Substitute(promoted.PromotedField.Type, instTypeParams, instTypeArgs!)
                        : promoted.PromotedField.Type;
                    var embeddedAccess = new SelectorExpression(target, promoted.EmbeddedField, embeddedType, span);
                    return new SelectorExpression(embeddedAccess, promoted.PromotedField, innerType, span);
                }

                // Method value: check named type methods first, then underlying struct methods
                var method = targetType.LookupMethod(fieldName)
                    ?? structType.LookupMethod(fieldName);
                if (method != null)
                {
                    var paramTypes = new TypeSymbol[method.Parameters.Count];
                    for (int i = 0; i < method.Parameters.Count; i++)
                    {
                        paramTypes[i] = instTypeParams != null
                            ? TypeSubstituter.Substitute(method.Parameters[i].Type, instTypeParams, instTypeArgs!)
                            : method.Parameters[i].Type;
                    }
                    var returnTypes = instTypeParams != null
                        ? TypeSubstituter.SubstituteTypes(method.ReturnTypes, instTypeParams, instTypeArgs!)
                        : method.ReturnTypes;
                    var funcType = new FunctionTypeSymbol(paramTypes, returnTypes, method.IsVariadic);
                    return new MethodValueExpression(target, method, funcType, span);
                }

                // Check promoted methods from embedded structs
                var promotedMethod = structType.LookupPromotedMethod(fieldName);
                if (promotedMethod != null)
                {
                    var pm = promotedMethod.Method;
                    var paramTypes = new TypeSymbol[pm.Parameters.Count];
                    for (int i = 0; i < pm.Parameters.Count; i++)
                    {
                        paramTypes[i] = instTypeParams != null
                            ? TypeSubstituter.Substitute(pm.Parameters[i].Type, instTypeParams, instTypeArgs!)
                            : pm.Parameters[i].Type;
                    }
                    var returnTypes = instTypeParams != null
                        ? TypeSubstituter.SubstituteTypes(pm.ReturnTypes, instTypeParams, instTypeArgs!)
                        : pm.ReturnTypes;
                    var funcType = new FunctionTypeSymbol(paramTypes, returnTypes, pm.IsVariadic);
                    return new MethodValueExpression(target, pm, funcType, span);
                }

                _context.Errors.ReportError(span, ErrorCode.UndefinedField,
                    $"Type '{structType.Name}' has no field or method '{fieldName}'");
                return new ErrorExpression($"Undefined field: {fieldName}", span);
            }

            // Type parameters: resolve selectors against constraint methods
            if (resolvedTargetType is TypeParameterSymbol typeParam && typeParam.Constraint != null)
            {
                foreach (var cm in typeParam.Constraint.Methods)
                {
                    if (cm.Name == fieldName)
                    {
                        var paramTypes = new TypeSymbol[cm.Parameters.Count];
                        for (int i = 0; i < cm.Parameters.Count; i++)
                            paramTypes[i] = cm.Parameters[i].Type;
                        var funcType = new FunctionTypeSymbol(paramTypes, cm.ReturnTypes, cm.IsVariadic);
                        return new MethodValueExpression(target, cm, funcType, span);
                    }
                }
            }

            // Method value on non-struct types (including interfaces)
            var typeMethod = targetType.LookupMethod(fieldName);

            // For type aliases, check the aliased type's methods (e.g., type X = pkg.Y)
            if (typeMethod == null && targetType.IsAlias && targetType.UnderlyingType != null)
            {
                typeMethod = targetType.UnderlyingType.LookupMethod(fieldName);
            }

            // Also check resolved type for methods (handles named type definitions)
            if (typeMethod == null && resolvedTargetType != targetType)
            {
                typeMethod = resolvedTargetType.LookupMethod(fieldName);
            }

            if (typeMethod != null)
            {
                var paramTypes = new TypeSymbol[typeMethod.Parameters.Count];
                for (int i = 0; i < typeMethod.Parameters.Count; i++)
                    paramTypes[i] = typeMethod.Parameters[i].Type;
                var funcType = new FunctionTypeSymbol(paramTypes, typeMethod.ReturnTypes, typeMethod.IsVariadic);
                return new MethodValueExpression(target, typeMethod, funcType, span);
            }

            // Interface types allow any selector — the concrete type is unknown at compile time.
            if (IsInterfaceType(targetType) || IsInterfaceType(resolvedTargetType))
            {
                var syntheticField = new FieldSymbol(fieldName, targetType, 0);
                return new SelectorExpression(target, syntheticField, targetType, span);
            }

            // Named slice/map/channel types can have methods — check them
            if (targetType.TypeKind == TypeKind.Slice || targetType.TypeKind == TypeKind.Map ||
                targetType.TypeKind == TypeKind.Channel)
            {
                var namedMethod = targetType.LookupMethod(fieldName);
                if (namedMethod != null)
                {
                    var paramTypes = new TypeSymbol[namedMethod.Parameters.Count];
                    for (int i = 0; i < namedMethod.Parameters.Count; i++)
                    {
                        paramTypes[i] = namedMethod.Parameters[i].Type;
                    }
                    var funcType = new FunctionTypeSymbol(paramTypes, namedMethod.ReturnTypes, namedMethod.IsVariadic);
                    return new MethodValueExpression(target, namedMethod, funcType, span);
                }
            }

            // Type parameter with structural constraint (e.g. ~struct{...}): look up fields
            if (resolvedTargetType is TypeParameterSymbol fieldTypeParam)
            {
                // Check constraint methods first
                var constraintMethods = fieldTypeParam.Constraint.Methods;

                // For generic interface constraints (e.g., nistPoint[Point]),
                // lazily resolve methods from the interface type with substitution
                if (constraintMethods.Count == 0
                    && fieldTypeParam.Constraint.InterfaceType is InterfaceTypeSymbol constraintIface
                    && constraintIface.Methods.Count > 0)
                {
                    var typeArgs = fieldTypeParam.Constraint.InterfaceTypeArgs;
                    var substituted = new List<MethodSymbol>();
                    foreach (var m in constraintIface.Methods)
                    {
                        var newParams = new List<ParameterSymbol>();
                        int pOrd = 0;
                        foreach (var p in m.Parameters)
                        {
                            var pType = SubstituteConstraintTypeParam(p.Type, constraintIface.TypeParameters, typeArgs);
                            newParams.Add(new ParameterSymbol(p.Name, pType, pOrd++));
                        }
                        var newReturns = new List<TypeSymbol>();
                        foreach (var r in m.ReturnTypes)
                            newReturns.Add(SubstituteConstraintTypeParam(r, constraintIface.TypeParameters, typeArgs));
                        substituted.Add(new MethodSymbol(m.Name, m.ReceiverType, m.IsPointerReceiver, newParams, newReturns));
                    }
                    constraintMethods = substituted;
                }

                foreach (var method in constraintMethods)
                {
                    if (method.Name == fieldName)
                    {
                        var paramTypes = new TypeSymbol[method.Parameters.Count];
                        for (int i = 0; i < method.Parameters.Count; i++)
                            paramTypes[i] = method.Parameters[i].Type;
                        var funcType = new FunctionTypeSymbol(paramTypes, method.ReturnTypes, method.IsVariadic);
                        return new MethodValueExpression(target, method, funcType, span);
                    }
                }

                // Check structural type element for struct fields
                var structural = TypeChecker.GetConstraintStructuralType(fieldTypeParam);
                if (structural != null)
                {
                    var resolvedStructural = structural.Resolved();
                    if (resolvedStructural == structural && structural.UnderlyingType != null)
                        resolvedStructural = structural.UnderlyingType;
                    if (resolvedStructural is StructTypeSymbol constraintStruct)
                    {
                        var field = constraintStruct.LookupField(fieldName);
                        if (field != null)
                        {
                            return new SelectorExpression(target, field, field.Type, span);
                        }
                        var promoted = constraintStruct.LookupPromotedField(fieldName);
                        if (promoted != null)
                        {
                            var embeddedAccess = new SelectorExpression(target, promoted.EmbeddedField, promoted.EmbeddedField.Type, span);
                            return new SelectorExpression(embeddedAccess, promoted.PromotedField, promoted.PromotedField.Type, span);
                        }
                    }
                }
            }

            // Type parameters: allow any field access (checked at instantiation, not definition)
            if (resolvedTargetType is TypeParameterSymbol
                || targetType is TypeParameterSymbol)
            {
                var syntheticField = new FieldSymbol(fieldName, BuiltinTypes.EmptyInterface, 0);
                return new SelectorExpression(target, syntheticField, BuiltinTypes.EmptyInterface, span);
            }

            _context.Errors.ReportError(span, ErrorCode.InvalidSelector,
                $"Type '{target.Type.Name}' does not support field access");
            return new ErrorExpression("Invalid selector", span);
        }

        private static bool IsInterfaceType(TypeSymbol type)
        {
            if (type is InterfaceTypeSymbol)
            {
                return true;
            }
            if (type.TypeKind == TypeKind.Interface)
            {
                return true;
            }
            // Check underlying type (for type aliases like crypto.PrivateKey = any)
            var underlying = type.UnderlyingType;
            if (underlying != null && underlying != type)
            {
                if (underlying is InterfaceTypeSymbol || underlying.TypeKind == TypeKind.Interface)
                {
                    return true;
                }
            }
            // Check if name suggests it's an interface alias (any, error, etc.)
            var resolved = type.Resolved();
            if (resolved != type && resolved != null)
            {
                if (resolved is InterfaceTypeSymbol || resolved.TypeKind == TypeKind.Interface)
                {
                    return true;
                }
            }
            return false;
        }

        private static TypeSymbol SubstituteConstraintTypeParam(
            TypeSymbol type,
            IReadOnlyList<TypeParameterSymbol> typeParams,
            IReadOnlyList<TypeSymbol>? typeArgs)
        {
            if (typeArgs == null || typeParams.Count == 0) return type;
            for (int i = 0; i < typeParams.Count && i < typeArgs.Count; i++)
            {
                if (type == typeParams[i]) return typeArgs[i];
            }
            if (type is SliceTypeSymbol slice)
                return new SliceTypeSymbol(SubstituteConstraintTypeParam(slice.ElementType, typeParams, typeArgs));
            if (type is PointerTypeSymbol ptr)
                return new PointerTypeSymbol(SubstituteConstraintTypeParam(ptr.ElementType, typeParams, typeArgs));
            return type;
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
                // Handle pointer-to-struct: *T{...} → &T{...}
                if (resolvedElemType is PointerTypeSymbol ptrType)
                {
                    var baseType = ptrType.ElementType.UnderlyingType ?? ptrType.ElementType;
                    if (baseType is StructTypeSymbol ptrStructType)
                    {
                        var structExpr = ResolveStructCompositeLiteral(ptrStructType, innerLit, innerSpan, ptrType.ElementType);
                        return new AddressOfExpression(structExpr, elementType, innerSpan);
                    }
                }
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
                    long keyLong;
                    if (keyVal is long kl) keyLong = kl;
                    else if (keyVal is int ki) keyLong = ki;
                    else if (keyVal is ulong kul) keyLong = (long)kul;
                    else keyLong = -1;
                    if (keyLong < 0)
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
                    long keyLong;
                    if (keyVal is long kl) keyLong = kl;
                    else if (keyVal is int ki) keyLong = ki;
                    else if (keyVal is ulong kul) keyLong = (long)kul;
                    else keyLong = -1;
                    if (keyLong < 0)
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

        private Expression ResolveTypeArgumentListExpression(TypeArgumentListSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            // Generic type instantiation in expression position: Map[string, int]{...}
            // or generic function reference: Sort[int]
            var resolvedType = _typeResolver.ResolveType(syntax);
            if (resolvedType != null)
            {
                return new IdentifierExpression(
                    new LocalSymbol(resolvedType.Name, resolvedType),
                    resolvedType, span);
            }

            // Try as a generic function reference
            var funcExpr = ResolveExpression(syntax.Expression);
            if (funcExpr is IdentifierExpression idExpr && idExpr.Symbol is FunctionSymbol funcSymbol
                && funcSymbol.IsGeneric)
            {
                var typeArgs = new List<TypeSymbol>();
                for (int i = 0; i < syntax.TypeArguments.Count; i++)
                {
                    var resolved = _typeResolver.ResolveType(syntax.TypeArguments[i]);
                    typeArgs.Add(resolved ?? TypeSymbol.Error);
                }

                var substParams = TypeSubstituter.SubstituteParams(
                    funcSymbol.Parameters, funcSymbol.TypeParameters, typeArgs);
                var substReturnTypes = TypeSubstituter.SubstituteTypes(
                    funcSymbol.ReturnTypes, funcSymbol.TypeParameters, typeArgs);

                var paramTypes = new List<TypeSymbol>();
                for (int i = 0; i < substParams.Count; i++)
                {
                    paramTypes.Add(substParams[i].Type);
                }

                var funcType = new FunctionTypeSymbol(paramTypes, substReturnTypes, funcSymbol.IsVariadic);
                return new IdentifierExpression(funcSymbol, funcType, span);
            }

            _context.Errors.ReportError(span, ErrorCode.UnsupportedSyntax,
                $"Cannot resolve generic type expression");
            return new ErrorExpression("Cannot resolve generic type", span);
        }

        private Expression ResolveIndexExpression(IndexExpressionSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            // Check if this is a generic function or type instantiation: Max[int], Iterator[T]
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

                    var funcType = new FunctionTypeSymbol(paramTypes, substReturnTypes, funcSymbol.IsVariadic);
                    return new IdentifierExpression(funcSymbol, funcType, span);
                }

                // Generic type instantiation: Iterator[int], Set[string]
                if (symbol is TypeSymbol typeSymbol && typeSymbol.IsGeneric)
                {
                    var typeArg = _typeResolver.ResolveType(syntax.Index);
                    if (typeArg != null)
                    {
                        var instantiated = new InstantiatedTypeSymbol(typeSymbol, new[] { typeArg });
                        return new IdentifierExpression(instantiated, instantiated, span);
                    }
                }
            }

            var target = ResolveExpression(syntax.Expression);

            // Check if this is a generic instantiation via selector: pkg.Func[Type] or pkg.Type[Arg]
            if (target is IdentifierExpression selectorId)
            {
                if (selectorId.Symbol is FunctionSymbol selectorFunc)
                {
                    var typeArg = _typeResolver.ResolveType(syntax.Index);
                    if (typeArg != null)
                    {
                        if (selectorFunc.IsGeneric)
                        {
                            var typeArgs = new[] { typeArg };
                            var substParams = TypeSubstituter.SubstituteParams(
                                selectorFunc.Parameters, selectorFunc.TypeParameters, typeArgs);
                            var substReturnTypes = TypeSubstituter.SubstituteTypes(
                                selectorFunc.ReturnTypes, selectorFunc.TypeParameters, typeArgs);
                            var paramTypes = new List<TypeSymbol>();
                            for (int i = 0; i < substParams.Count; i++)
                            {
                                paramTypes.Add(substParams[i].Type);
                            }
                            var funcType = new FunctionTypeSymbol(paramTypes, substReturnTypes, selectorFunc.IsVariadic);
                            return new IdentifierExpression(selectorFunc, funcType, span);
                        }
                        else
                        {
                            return new IdentifierExpression(selectorFunc, _context.GetSymbolType(selectorFunc), span);
                        }
                    }
                }
                else if (selectorId.Symbol is TypeSymbol selectorType && selectorType.IsGeneric)
                {
                    var typeArg = _typeResolver.ResolveType(syntax.Index);
                    if (typeArg != null)
                    {
                        var instantiated = new InstantiatedTypeSymbol(selectorType, new[] { typeArg });
                        return new IdentifierExpression(instantiated, instantiated, span);
                    }
                }
            }

            var index = ResolveExpression(syntax.Index);

            if (target.Type == TypeSymbol.Error)
            {
                return new ErrorExpression("Error target", span);
            }

            var resolvedTargetType = target.Type.Resolved();
            // Unwrap chains of named types (e.g. pallocBits → pageBits → [N]uint64)
            {
                var unwrapped = resolvedTargetType;
                for (int i = 0; i < 10; i++)
                {
                    if (unwrapped.UnderlyingType == null) break;
                    unwrapped = unwrapped.UnderlyingType.Resolved();
                }
                if (unwrapped != resolvedTargetType)
                    resolvedTargetType = unwrapped;
            }

            // Auto-dereference pointer to array/slice: (*t)[i] when t is *[N]T or *[]T
            if (resolvedTargetType is PointerTypeSymbol ptrForIndex)
            {
                var inner = ptrForIndex.ElementType.Resolved();
                // Unwrap named type chains within pointer element
                for (int i = 0; i < 10; i++)
                {
                    if (inner.UnderlyingType == null) break;
                    inner = inner.UnderlyingType.Resolved();
                }
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
                    // Resolve named types to their underlying structural type
                    var resolvedElem = elemType.Resolved();
                    if (resolvedElem == elemType && elemType.UnderlyingType != null)
                        resolvedElem = elemType.UnderlyingType;
                    if (resolvedElem is ArrayTypeSymbol arrElem)
                    {
                        commonElement ??= arrElem.ElementType;
                    }
                    else if (resolvedElem is SliceTypeSymbol sliceElem)
                    {
                        commonElement ??= sliceElem.ElementType;
                    }
                    else if (resolvedElem is MapTypeSymbol mapElem)
                    {
                        commonElement ??= mapElem.ValueType;
                    }
                    else if (resolvedElem.TypeKind == TypeKind.String)
                    {
                        commonElement ??= BuiltinTypes.Byte;
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
            else if (resolvedOpType is TypeParameterSymbol sliceTypeParam)
            {
                // Type parameter with structural constraint (e.g. ~[]E): allow slicing
                var structural = TypeChecker.GetConstraintStructuralType(sliceTypeParam);
                if (structural is SliceTypeSymbol || structural is ArrayTypeSymbol
                    || (structural != null && (structural.TypeKind == TypeKind.String
                        || structural.TypeKind == TypeKind.UntypedString)))
                {
                    // For type params constrained to slices, the result type is the type param itself
                    // (preserves the constraint type)
                    resultType = operand.Type;
                }
                else
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidSlice,
                        $"Cannot slice type '{operand.Type.Name}'");
                    return new ErrorExpression("Invalid slice", span);
                }
            }
            else if (operand.Type.TypeKind == TypeKind.Interface
                || (operand.Type is InterfaceTypeSymbol ifaceSlice && ifaceSlice.Methods.Count == 0))
            {
                // Slicing interface{} — Go allows this at runtime
                resultType = operand.Type;
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

            // The expression must be an interface type (or error, which our runtime
            // represents as string but is an interface in Go)
            var exprTypeResolved = expr.Type.Resolved();
            bool isInterface = expr.Type is InterfaceTypeSymbol
                || exprTypeResolved is InterfaceTypeSymbol
                || expr.Type.TypeKind == TypeKind.Interface
                || exprTypeResolved.TypeKind == TypeKind.Interface
                || (expr.Type is InstantiatedTypeSymbol instType
                    && instType.GenericType is InterfaceTypeSymbol)
                || expr.Type == BuiltinTypes.Error
                || expr.Type.Name == "error"
                || expr.Type.TypeKind == TypeKind.String
                || expr.Type.TypeKind == TypeKind.UntypedString;
            if (!isInterface)
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

// -----------------------------------------------------------------------
// <copyright file="StatementResolver.cs" company="Ziad">
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
    public sealed class StatementResolver
    {
        private readonly AnalysisContext _context;
        private readonly ExpressionResolver _expressionResolver;
        private readonly TypeResolver _typeResolver;

        private Func<VarDeclarationSyntax, AstNode?> _resolveVarDeclaration;
        private Func<ConstDeclarationSyntax, AstNode?> _resolveConstDeclaration;

        public StatementResolver(AnalysisContext context, ExpressionResolver expressionResolver, TypeResolver typeResolver)
        {
            _context = context;
            _expressionResolver = expressionResolver;
            _typeResolver = typeResolver;
            _resolveVarDeclaration = _ => throw new InvalidOperationException(
                "Var declaration resolver has not been set. Call SetDeclarationResolvers first.");
            _resolveConstDeclaration = _ => throw new InvalidOperationException(
                "Const declaration resolver has not been set. Call SetDeclarationResolvers first.");
        }

        public void SetDeclarationResolvers(
            Func<VarDeclarationSyntax, AstNode?> varResolver,
            Func<ConstDeclarationSyntax, AstNode?> constResolver)
        {
            _resolveVarDeclaration = varResolver;
            _resolveConstDeclaration = constResolver;
        }

        public BlockStatement ResolveBlock(BlockSyntax syntax)
        {
            _context.PushScope("block");
            var statements = new List<AstNode>();
            bool unreachableReported = false;

            foreach (var stmtSyntax in syntax.Statements)
            {
                var bound = ResolveStatement(stmtSyntax);
                if (bound != null)
                {
                    if (!unreachableReported && CheckUnreachable(statements, bound))
                    {
                        unreachableReported = true;
                    }

                    statements.Add(bound);
                }
            }

            _context.PopScope();
            return new BlockStatement(statements, _context.SpanOf(syntax));
        }

        public AstNode? ResolveStatement(SyntaxNode syntax)
        {
            switch (syntax.Kind)
            {
                case SyntaxKind.ReturnStatement:
                    return ResolveReturnStatement((ReturnStatementSyntax)syntax);
                case SyntaxKind.ExpressionStatement:
                    return ResolveExpressionStatement((ExpressionStatementSyntax)syntax);
                case SyntaxKind.AssignmentStatement:
                    return ResolveAssignmentStatement((AssignmentStatementSyntax)syntax);
                case SyntaxKind.ShortVarDeclaration:
                    return ResolveShortVarDeclaration((ShortVarDeclarationSyntax)syntax);
                case SyntaxKind.IncDecStatement:
                    return ResolveIncDecStatement((IncDecStatementSyntax)syntax);
                case SyntaxKind.Block:
                    return ResolveBlock((BlockSyntax)syntax);
                case SyntaxKind.VarDeclaration:
                    return _resolveVarDeclaration((VarDeclarationSyntax)syntax);
                case SyntaxKind.IfStatement:
                    return ResolveIfStatement((IfStatementSyntax)syntax);
                case SyntaxKind.ForStatement:
                    return ResolveForStatement((ForStatementSyntax)syntax);
                case SyntaxKind.SwitchStatement:
                    return ResolveSwitchStatement((SwitchStatementSyntax)syntax);
                case SyntaxKind.TypeSwitchStatement:
                    return ResolveTypeSwitchStatement((TypeSwitchStatementSyntax)syntax);
                case SyntaxKind.BranchStatement:
                    return ResolveBranchStatement((BranchStatementSyntax)syntax);
                case SyntaxKind.ConstDeclaration:
                    return _resolveConstDeclaration((ConstDeclarationSyntax)syntax);
                case SyntaxKind.DeferStatement:
                    return ResolveDeferStatement((DeferStatementSyntax)syntax);
                case SyntaxKind.GoStatement:
                    return ResolveGoStatement((GoStatementSyntax)syntax);
                case SyntaxKind.SendStatement:
                    return ResolveSendStatement((SendStatementSyntax)syntax);
                case SyntaxKind.SelectStatement:
                    return ResolveSelectStatement((SelectStatementSyntax)syntax);
                case SyntaxKind.LabeledStatement:
                    return ResolveLabeledStatement((LabeledStatementSyntax)syntax);
                case SyntaxKind.TypeDeclaration:
                    return ResolveLocalTypeDeclaration((TypeDeclarationSyntax)syntax);
                case SyntaxKind.EmptyStatement:
                    return null;
                default:
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UnsupportedSyntax,
                        $"Statement kind '{syntax.Kind}' is not yet supported");
                    return null;
            }
        }

        private ReturnStatement ResolveReturnStatement(ReturnStatementSyntax syntax)
        {
            var values = new List<Expression>();
            for (int i = 0; i < syntax.Values.Count; i++)
            {
                values.Add(_expressionResolver.ResolveExpression(syntax.Values[i]));
            }

            // Bare return with named returns — valid, no further validation needed
            if (values.Count == 0 && _context.CurrentNamedReturns.Count > 0)
            {
                return new ReturnStatement(values, _context.SpanOf(syntax));
            }

            // Multi-return spread: return f() where f returns multiple values
            if (values.Count == 1 && _context.CurrentReturnTypes.Count > 1)
            {
                var returnTypes = _context.GetCallReturnTypes(values[0]);
                if (returnTypes != null && returnTypes.Count == _context.CurrentReturnTypes.Count)
                {
                    return new ReturnStatement(values, _context.SpanOf(syntax));
                }
            }

            // Check if any return types failed to resolve (void = unresolved, error-kinded = error sentinel).
            // When return types didn't resolve, count/type mismatches are cascade errors — suppress them.
            bool hasUnresolvedReturnTypes = false;
            for (int i = 0; i < _context.CurrentReturnTypes.Count; i++)
            {
                var rt = _context.CurrentReturnTypes[i].Resolved();
                if (rt == BuiltinTypes.Void || rt.TypeKind == TypeKind.Error)
                {
                    hasUnresolvedReturnTypes = true;
                    break;
                }
            }

            // Check for multi-return forwarding: return f() where f returns (T1, T2, ...)
            if (values.Count == 1 && _context.CurrentReturnTypes.Count > 1)
            {
                var callReturnTypes = _context.GetCallReturnTypes(values[0]);
                if (callReturnTypes != null && callReturnTypes.Count == _context.CurrentReturnTypes.Count)
                {
                    return new ReturnStatement(values, _context.SpanOf(syntax));
                }
            }

            if (values.Count != _context.CurrentReturnTypes.Count)
            {
                if (values.Count == 0 && _context.CurrentReturnTypes.Count > 0 && !hasUnresolvedReturnTypes)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.MissingReturn,
                        "Missing return value");
                }
                else if (_context.CurrentReturnTypes.Count == 0 && values.Count > 0)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.WrongReturnCount,
                        "Too many return values");
                }
                else if (!hasUnresolvedReturnTypes)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.WrongReturnCount,
                        $"Wrong number of return values: expected {_context.CurrentReturnTypes.Count}, got {values.Count}");
                }
            }
            else
            {
                for (int i = 0; i < values.Count; i++)
                {
                    var rt = _context.CurrentReturnTypes[i].Resolved();
                    // Skip type check if the return type failed to resolve (cascade error)
                    if (rt == BuiltinTypes.Void || rt.TypeKind == TypeKind.Error)
                        continue;

                    if (!TypeChecker.IsAssignable(values[i].Type, _context.CurrentReturnTypes[i]))
                    {
                        _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.TypeMismatch,
                            $"Cannot return '{values[i].Type.Name}' as '{_context.CurrentReturnTypes[i].Name}'");
                    }
                }
            }

            return new ReturnStatement(values, _context.SpanOf(syntax));
        }

        private ExpressionStatement ResolveExpressionStatement(ExpressionStatementSyntax syntax)
        {
            var expression = _expressionResolver.ResolveExpression(syntax.Expression);
            return new ExpressionStatement(expression, _context.SpanOf(syntax));
        }

        private AstNode ResolveAssignmentStatement(AssignmentStatementSyntax syntax)
        {
            bool isPlainAssign = syntax.OperatorToken.Kind == SyntaxKind.EqualsToken;

            // Multi-return call detection: a, b = foo()
            if (syntax.Left.Count > 1 && syntax.Right.Count == 1)
            {
                var rhs = _expressionResolver.ResolveExpression(syntax.Right[0]);
                var returnTypes = _context.GetCallReturnTypes(rhs);
                if (returnTypes != null && returnTypes.Count == syntax.Left.Count)
                {
                    var targets = new Expression?[syntax.Left.Count];
                    for (int i = 0; i < syntax.Left.Count; i++)
                    {
                        _context.SuppressUsageMarking = syntax.Left[i] is IdentifierNameSyntax;
                        var lhsExpr = _expressionResolver.ResolveExpression(syntax.Left[i]);
                        _context.SuppressUsageMarking = false;
                        if (lhsExpr is IdentifierExpression idExpr && idExpr.Symbol.Name == "_")
                        {
                            targets[i] = null;
                            continue;
                        }

                        if (!TypeChecker.IsAssignable(returnTypes[i], lhsExpr.Type))
                        {
                            _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.TypeMismatch,
                                $"Cannot assign '{returnTypes[i].Name}' to '{lhsExpr.Type.Name}'");
                        }

                        targets[i] = lhsExpr;
                    }

                    return new MultiAssignmentStatement(targets, rhs, _context.SpanOf(syntax));
                }
            }

            // Parallel assignment: a, b = b, a (equal count, multiple expressions)
            if (syntax.Left.Count > 1 && syntax.Left.Count == syntax.Right.Count)
            {
                var targets = new Expression?[syntax.Left.Count];
                var values = new Expression[syntax.Right.Count];
                for (int i = 0; i < syntax.Left.Count; i++)
                {
                    _context.SuppressUsageMarking = syntax.Left[i] is IdentifierNameSyntax;
                    var lhsExpr = _expressionResolver.ResolveExpression(syntax.Left[i]);
                    _context.SuppressUsageMarking = false;
                    values[i] = _expressionResolver.ResolveExpression(syntax.Right[i]);

                    if (lhsExpr is IdentifierExpression idExpr && idExpr.Symbol.Name == "_")
                    {
                        targets[i] = null;
                        continue;
                    }

                    if (!TypeChecker.IsAssignable(values[i].Type, lhsExpr.Type))
                    {
                        _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.TypeMismatch,
                            $"Cannot assign '{values[i].Type.Name}' to '{lhsExpr.Type.Name}'");
                    }

                    targets[i] = lhsExpr;
                }

                return new ParallelAssignmentStatement(targets, values, _context.SpanOf(syntax));
            }

            // Single assignment
            _context.SuppressUsageMarking = isPlainAssign && syntax.Left[0] is IdentifierNameSyntax;
            var target = _expressionResolver.ResolveExpression(syntax.Left[0]);
            _context.SuppressUsageMarking = false;
            var value = _expressionResolver.ResolveExpression(syntax.Right[0]);

            if (target is IdentifierExpression targetId && targetId.Symbol.Name == "_")
            {
                return new ExpressionStatement(value, _context.SpanOf(syntax));
            }

            // Desugar compound assignments: x += y → x = x + y
            var compoundOp = GetCompoundBinaryOp(syntax.OperatorToken.Kind);
            if (compoundOp != null)
            {
                var resultType = TypeChecker.CommonType(target.Type, value.Type) ?? target.Type;
                value = new BinaryExpression(target, compoundOp.Value, value, resultType,
                    _context.SpanOf(syntax));
            }

            if (!TypeChecker.IsAssignable(value.Type, target.Type))
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.TypeMismatch,
                    $"Cannot assign '{value.Type.Name}' to '{target.Type.Name}'");
            }

            return new AssignmentStatement(target, value, _context.SpanOf(syntax));
        }

        private static BinaryOperator? GetCompoundBinaryOp(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.PlusEqualsToken => BinaryOperator.Add,
                SyntaxKind.MinusEqualsToken => BinaryOperator.Subtract,
                SyntaxKind.StarEqualsToken => BinaryOperator.Multiply,
                SyntaxKind.SlashEqualsToken => BinaryOperator.Divide,
                SyntaxKind.PercentEqualsToken => BinaryOperator.Remainder,
                SyntaxKind.AmpersandEqualsToken => BinaryOperator.BitwiseAnd,
                SyntaxKind.PipeEqualsToken => BinaryOperator.BitwiseOr,
                SyntaxKind.CaretEqualsToken => BinaryOperator.BitwiseXor,
                SyntaxKind.LessThanLessThanEqualsToken => BinaryOperator.ShiftLeft,
                SyntaxKind.GreaterThanGreaterThanEqualsToken => BinaryOperator.ShiftRight,
                SyntaxKind.AmpersandCaretEqualsToken => BinaryOperator.AndNot,
                _ => null,
            };
        }

        private AstNode ResolveShortVarDeclaration(ShortVarDeclarationSyntax syntax)
        {
            // Multi-return call detection: a, b := foo()
            if (syntax.Left.Count > 1 && syntax.Right.Count == 1)
            {
                var rhs = _expressionResolver.ResolveExpression(syntax.Right[0]);
                var returnTypes = _context.GetCallReturnTypes(rhs);
                if (returnTypes != null && returnTypes.Count == syntax.Left.Count)
                {
                    var symbols = new LocalSymbol?[syntax.Left.Count];
                    bool hasNewVar = false;
                    bool hasNamedVar = false;
                    for (int i = 0; i < syntax.Left.Count; i++)
                    {
                        var nameExpr = syntax.Left[i] as IdentifierNameSyntax;
                        if (nameExpr == null)
                        {
                            _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.SyntaxError,
                                "Left side of := must be an identifier");
                            continue;
                        }

                        if (nameExpr.Identifier.Text == "_")
                        {
                            symbols[i] = null;
                            continue;
                        }

                        hasNamedVar = true;

                        // Short var redeclaration: reuse existing variable if in same scope
                        var existing = _context.Scope.LookupLocal(nameExpr.Identifier.Text);
                        if (existing is LocalSymbol existingLocal)
                        {
                            symbols[i] = existingLocal;
                        }
                        else
                        {
                            var varType = TypeChecker.DefaultType(returnTypes[i]);
                            var symbol = new LocalSymbol(nameExpr.Identifier.Text, varType);
                            _context.Scope.TryDeclare(symbol);
                            _context.TrackLocal(symbol, _context.SpanOf(syntax));
                            symbols[i] = symbol;
                            hasNewVar = true;
                        }
                    }

                    if (hasNamedVar && !hasNewVar)
                    {
                        _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                            "No new variables on left side of :=");
                    }

                    return new MultiVarDeclaration(symbols, rhs, _context.SpanOf(syntax));
                }

                // When RHS is error-typed (unresolved function), declare variables as error type
                // to prevent cascading "Undefined name" errors
                if (rhs.Type == TypeSymbol.Error)
                {
                    var errorSymbols = new LocalSymbol?[syntax.Left.Count];
                    for (int i = 0; i < syntax.Left.Count; i++)
                    {
                        var nameExpr = syntax.Left[i] as IdentifierNameSyntax;
                        if (nameExpr == null || nameExpr.Identifier.Text == "_") continue;

                        var existing = _context.Scope.LookupLocal(nameExpr.Identifier.Text);
                        if (existing is LocalSymbol existingLocal)
                        {
                            errorSymbols[i] = existingLocal;
                        }
                        else
                        {
                            var symbol = new LocalSymbol(nameExpr.Identifier.Text, TypeSymbol.Error);
                            _context.Scope.TryDeclare(symbol);
                            _context.TrackLocal(symbol, _context.SpanOf(syntax));
                            errorSymbols[i] = symbol;
                        }
                    }
                    return new MultiVarDeclaration(errorSymbols, rhs, _context.SpanOf(syntax));
                }
            }

            // Single short var declaration (x := expr)
            if (syntax.Left.Count == 1 && syntax.Right.Count == 1)
            {
                var nameExpr = syntax.Left[0] as IdentifierNameSyntax;
                if (nameExpr == null)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.SyntaxError,
                        "Left side of := must be an identifier");
                    return new ExpressionStatement(
                        new ErrorExpression("Invalid short var declaration", _context.SpanOf(syntax)),
                        _context.SpanOf(syntax));
                }

                var initializer = _expressionResolver.ResolveExpression(syntax.Right[0]);
                var varType = TypeChecker.DefaultType(initializer.Type);

                if (nameExpr.Identifier.Text == "_")
                {
                    return new ExpressionStatement(initializer, _context.SpanOf(syntax));
                }

                var symbol = new LocalSymbol(nameExpr.Identifier.Text, varType);
                _context.TrackLocal(symbol, _context.SpanOf(syntax));

                if (!_context.Scope.TryDeclare(symbol))
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                        $"Variable '{nameExpr.Identifier.Text}' is already declared");
                }

                return new VarDeclaration(symbol, initializer, _context.SpanOf(syntax));
            }

            // Multiple short var — bind each pair
            if (syntax.Left.Count != syntax.Right.Count)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.WrongReturnCount,
                    $"Assignment mismatch: {syntax.Left.Count} variables but {syntax.Right.Count} values");
            }

            var pairDecls = new List<AstNode>();
            bool pairHasNewVar = false;
            bool pairHasNamedVar = false;
            int count = Math.Min(syntax.Left.Count, syntax.Right.Count);
            for (int i = 0; i < count; i++)
            {
                var nameExpr = syntax.Left[i] as IdentifierNameSyntax;
                if (nameExpr == null)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.SyntaxError,
                        "Left side of := must be an identifier");
                    continue;
                }

                var initializer = _expressionResolver.ResolveExpression(syntax.Right[i]);
                var varType = TypeChecker.DefaultType(initializer.Type);

                if (nameExpr.Identifier.Text == "_")
                {
                    continue;
                }

                pairHasNamedVar = true;

                // Short var redeclaration: reuse existing variable if in same scope
                var existing = _context.Scope.LookupLocal(nameExpr.Identifier.Text);
                if (existing is LocalSymbol existingLocal)
                {
                    var target = new IdentifierExpression(existingLocal, existingLocal.Type,
                        _context.SpanOf(nameExpr));
                    pairDecls.Add(new AssignmentStatement(target, initializer, _context.SpanOf(syntax)));
                }
                else
                {
                    var symbol = new LocalSymbol(nameExpr.Identifier.Text, varType);
                    _context.Scope.TryDeclare(symbol);
                    _context.TrackLocal(symbol, _context.SpanOf(syntax));
                    pairDecls.Add(new VarDeclaration(symbol, initializer, _context.SpanOf(syntax)));
                    pairHasNewVar = true;
                }
            }

            if (pairHasNamedVar && !pairHasNewVar)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                    "No new variables on left side of :=");
            }

            if (pairDecls.Count == 1) return pairDecls[0];
            return new BlockStatement(pairDecls, _context.SpanOf(syntax));
        }

        private IncDecStatement ResolveIncDecStatement(IncDecStatementSyntax syntax)
        {
            var operand = _expressionResolver.ResolveExpression(syntax.Operand);
            bool isIncrement = syntax.OperatorToken.Kind == SyntaxKind.PlusPlusToken;

            if (!TypeChecker.IsNumeric(operand.Type) && operand.Type != TypeSymbol.Error)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.InvalidOperation,
                    $"Cannot apply increment/decrement to type '{operand.Type.Name}'");
            }

            return new IncDecStatement(operand, isIncrement, _context.SpanOf(syntax));
        }

        private IfStatement ResolveIfStatement(IfStatementSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            // If has an init statement, it gets its own scope (Go spec: scoped to entire if-else chain)
            bool hasInit = syntax.Init != null;
            if (hasInit)
            {
                _context.PushScope("if-init");
            }

            AstNode? init = syntax.Init != null ? ResolveStatement(syntax.Init) : null;
            var condition = _expressionResolver.ResolveExpression(syntax.Condition);

            if (condition.Type != TypeSymbol.Error &&
                condition.Type.TypeKind != TypeKind.Bool &&
                condition.Type.TypeKind != TypeKind.UntypedBool &&
                condition.Type is not TypeParameterSymbol &&
                condition.Type is not InterfaceTypeSymbol)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax.Condition), ErrorCode.TypeMismatch,
                    $"Non-bool type '{condition.Type.Name}' used as if condition");
            }

            var body = ResolveBlock(syntax.Body);

            AstNode? elseBody = null;
            if (syntax.ElseBody != null)
            {
                if (syntax.ElseBody is IfStatementSyntax elseIf)
                {
                    elseBody = ResolveIfStatement(elseIf);
                }
                else if (syntax.ElseBody is BlockSyntax elseBlock)
                {
                    elseBody = ResolveBlock(elseBlock);
                }
            }

            if (hasInit)
            {
                _context.PopScope();
            }

            return new IfStatement(init, condition, body, elseBody, span);
        }

        private Statement ResolveForStatement(ForStatementSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            if (syntax.RangeClause != null)
            {
                return ResolveForRangeStatement(syntax.RangeClause, syntax.Body, span);
            }

            _context.PushScope("for");

            AstNode? init = syntax.Init != null ? ResolveStatement(syntax.Init) : null;

            Expression? condition = null;
            if (syntax.Condition != null)
            {
                condition = _expressionResolver.ResolveExpression(syntax.Condition);
                if (condition.Type != TypeSymbol.Error &&
                    condition.Type.TypeKind != TypeKind.Bool &&
                    condition.Type.TypeKind != TypeKind.UntypedBool &&
                    condition.Type is not TypeParameterSymbol)
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax.Condition), ErrorCode.TypeMismatch,
                        $"Non-bool type '{condition.Type.Name}' used as for condition");
                }
            }

            AstNode? post = syntax.Post != null ? ResolveStatement(syntax.Post) : null;

            _context.LoopDepth++;
            var body = ResolveBlock(syntax.Body);
            _context.LoopDepth--;

            _context.PopScope();

            return new ForStatement(init, condition, post, body, span);
        }

        private SwitchStatement ResolveSwitchStatement(SwitchStatementSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            bool hasInit = syntax.Init != null;
            if (hasInit)
            {
                _context.PushScope("switch-init");
            }

            AstNode? init = syntax.Init != null ? ResolveStatement(syntax.Init) : null;

            Expression? tag = null;
            TypeSymbol? tagType = null;
            if (syntax.Tag != null)
            {
                tag = _expressionResolver.ResolveExpression(syntax.Tag);
                tagType = tag.Type;
            }

            _context.SwitchDepth++;
            var cases = new List<Ast.SwitchCase>();
            foreach (var caseSyntax in syntax.Cases)
            {
                cases.Add(ResolveSwitchCase(caseSyntax, tagType));
            }
            _context.SwitchDepth--;

            if (hasInit)
            {
                _context.PopScope();
            }

            return new SwitchStatement(init, tag, cases, span);
        }

        private Ast.SwitchCase ResolveSwitchCase(ExprSwitchCaseSyntax syntax, TypeSymbol? tagType)
        {
            var span = _context.SpanOf(syntax);
            bool isDefault = syntax.CaseOrDefault.Kind == SyntaxKind.DefaultKeyword;

            List<Expression>? expressions = null;
            if (!isDefault && syntax.Expressions.HasValue)
            {
                expressions = new List<Expression>();
                for (int i = 0; i < syntax.Expressions.Value.Count; i++)
                {
                    var expr = _expressionResolver.ResolveExpression(syntax.Expressions.Value[i]);
                    expressions.Add(expr);

                    if (expr.Type != TypeSymbol.Error && tagType != null && tagType != TypeSymbol.Error)
                    {
                        if (!TypeChecker.IsAssignable(expr.Type, tagType)
                            && !TypeChecker.IsAssignable(tagType, expr.Type)
                            && TypeChecker.CommonType(expr.Type, tagType) == null
                            && !(TypeChecker.IsNumeric(expr.Type) && TypeChecker.IsNumeric(tagType)))
                        {
                            _context.Errors.ReportError(_context.SpanOf(syntax.Expressions.Value[i]), ErrorCode.TypeMismatch,
                                $"Cannot compare type '{expr.Type.Name}' with switch tag type '{tagType.Name}'");
                        }
                    }
                    else if (tagType == null)
                    {
                        // Tagless switch: each case must be bool
                        if (expr.Type != TypeSymbol.Error &&
                            expr.Type.TypeKind != TypeKind.Bool &&
                            expr.Type.TypeKind != TypeKind.UntypedBool &&
                            expr.Type is not TypeParameterSymbol)
                        {
                            _context.Errors.ReportError(_context.SpanOf(syntax.Expressions.Value[i]), ErrorCode.TypeMismatch,
                                $"Non-bool type '{expr.Type.Name}' used as case condition in tagless switch");
                        }
                    }
                }
            }

            _context.PushScope("case");
            var body = new List<AstNode>();
            bool unreachableReported = false;
            foreach (var stmtSyntax in syntax.Statements)
            {
                var bound = ResolveStatement(stmtSyntax);
                if (bound != null)
                {
                    if (!unreachableReported && CheckUnreachable(body, bound))
                    {
                        unreachableReported = true;
                    }

                    body.Add(bound);
                }
            }
            _context.PopScope();

            return new Ast.SwitchCase(expressions, body, isDefault, span);
        }

        private TypeSwitchStatement ResolveTypeSwitchStatement(TypeSwitchStatementSyntax syntax)
        {
            var span = _context.SpanOf(syntax);

            bool hasInit = syntax.Init != null;
            if (hasInit)
            {
                _context.PushScope("switch-init");
            }

            AstNode? init = syntax.Init != null ? ResolveStatement(syntax.Init) : null;

            // Parse the guard: either `x.(type)` or `v := x.(type)`
            Expression guardExpr;
            string? assignedName = null;

            if (syntax.Guard is ShortVarDeclarationSyntax shortVar)
            {
                // v := x.(type) form
                if (shortVar.Left.Count > 0 && shortVar.Left[0] is IdentifierNameSyntax idSyntax)
                {
                    assignedName = idSyntax.Identifier.Text;
                }

                if (shortVar.Right.Count > 0 && shortVar.Right[0] is TypeAssertExpressionSyntax assertSyntax)
                {
                    guardExpr = _expressionResolver.ResolveExpression(assertSyntax.Expression);
                }
                else
                {
                    guardExpr = new ErrorExpression("Invalid type switch guard", span);
                }
            }
            else if (syntax.Guard is ExpressionStatementSyntax exprStmt
                && exprStmt.Expression is TypeAssertExpressionSyntax assertExpr)
            {
                guardExpr = _expressionResolver.ResolveExpression(assertExpr.Expression);
            }
            else if (syntax.Guard is TypeAssertExpressionSyntax directAssert)
            {
                guardExpr = _expressionResolver.ResolveExpression(directAssert.Expression);
            }
            else
            {
                guardExpr = new ErrorExpression("Invalid type switch guard", span);
            }

            var guardResolved = guardExpr.Type.Resolved();
            if (guardExpr.Type != TypeSymbol.Error
                && guardExpr.Type is not InterfaceTypeSymbol
                && guardResolved is not InterfaceTypeSymbol
                && guardExpr.Type.TypeKind != TypeKind.Interface
                && guardResolved.TypeKind != TypeKind.Interface)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidTypeAssert,
                    $"Cannot type switch on non-interface type '{guardExpr.Type.Name}'");
            }

            _context.SwitchDepth++;
            var cases = new List<TypeSwitchCase>();
            foreach (var caseSyntax in syntax.Cases)
            {
                cases.Add(ResolveTypeSwitchCase(caseSyntax, guardExpr.Type, assignedName));
            }
            _context.SwitchDepth--;

            if (hasInit)
            {
                _context.PopScope();
            }

            return new TypeSwitchStatement(init, guardExpr, assignedName, cases, span);
        }

        private TypeSwitchCase ResolveTypeSwitchCase(TypeSwitchCaseSyntax syntax,
            TypeSymbol guardType, string? assignedName)
        {
            var span = _context.SpanOf(syntax);
            bool isDefault = syntax.CaseOrDefault.Kind == SyntaxKind.DefaultKeyword;

            List<TypeSymbol>? caseTypes = null;
            if (!isDefault && syntax.Types.HasValue)
            {
                caseTypes = new List<TypeSymbol>();
                for (int i = 0; i < syntax.Types.Value.Count; i++)
                {
                    var typeExpr = syntax.Types.Value[i];
                    // case nil: in a type switch — nil matches untyped nil
                    if (typeExpr is IdentifierNameSyntax nilId
                        && nilId.Identifier.Text == "nil")
                    {
                        caseTypes.Add(BuiltinTypes.UntypedNil);
                        continue;
                    }

                    var resolved = _typeResolver.ResolveType(typeExpr);
                    if (resolved != null)
                    {
                        caseTypes.Add(resolved);
                    }
                    else
                    {
                        caseTypes.Add(TypeSymbol.Error);
                    }
                }
            }

            _context.PushScope("type-case");

            // Declare the assigned variable with the appropriate type
            LocalSymbol? assignedSymbol = null;
            if (assignedName != null && assignedName != "_")
            {
                TypeSymbol varType;
                if (caseTypes != null && caseTypes.Count == 1 && caseTypes[0] != TypeSymbol.Error)
                {
                    // Single type case: variable has the case type
                    varType = caseTypes[0];
                }
                else
                {
                    // Default or multi-type case: variable has the guard's interface type
                    varType = guardType;
                }

                assignedSymbol = new LocalSymbol(assignedName, varType);
                _context.Scope.TryDeclare(assignedSymbol);
                // Don't track for unused checking — Go treats type switch variables
                // as used if any case uses them, not per-case.
            }

            var body = new List<AstNode>();
            bool unreachableReported = false;
            foreach (var stmtSyntax in syntax.Statements)
            {
                var bound = ResolveStatement(stmtSyntax);
                if (bound != null)
                {
                    if (!unreachableReported && CheckUnreachable(body, bound))
                    {
                        unreachableReported = true;
                    }

                    body.Add(bound);
                }
            }

            _context.PopScope();

            return new TypeSwitchCase(caseTypes, body, isDefault, assignedSymbol, span);
        }

        private BranchStatement ResolveBranchStatement(BranchStatementSyntax syntax)
        {
            var span = _context.SpanOf(syntax);
            var label = syntax.Label?.Text;

            var kind = syntax.Keyword.Kind switch
            {
                SyntaxKind.BreakKeyword => BranchKind.Break,
                SyntaxKind.ContinueKeyword => BranchKind.Continue,
                SyntaxKind.FallthroughKeyword => BranchKind.Fallthrough,
                SyntaxKind.GotoKeyword => BranchKind.Goto,
                _ => BranchKind.Break,
            };

            if (kind == BranchKind.Goto && label == null)
            {
                _context.Errors.ReportError(span, ErrorCode.SyntaxError,
                    "Goto requires a label");
            }
            else if (kind == BranchKind.Break && _context.LoopDepth == 0 && _context.SwitchDepth == 0)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidBranch,
                    "Break is not in a loop or switch");
            }
            else if (kind == BranchKind.Continue && _context.LoopDepth == 0)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidBranch,
                    "Continue is not in a loop");
            }
            else if (kind == BranchKind.Fallthrough && _context.SwitchDepth == 0)
            {
                _context.Errors.ReportError(span, ErrorCode.InvalidBranch,
                    "Fallthrough is not in a switch");
            }

            return new BranchStatement(kind, label, span);
        }

        private LabeledStatement? ResolveLabeledStatement(LabeledStatementSyntax syntax)
        {
            var span = _context.SpanOf(syntax);
            var label = syntax.Label.Text;
            var inner = ResolveStatement(syntax.Statement);
            if (inner == null)
            {
                inner = new BlockStatement(Array.Empty<AstNode>(), span);
            }
            return new LabeledStatement(label, inner, span);
        }

        private ForRangeStatement ResolveForRangeStatement(RangeClauseSyntax rangeClause,
            BlockSyntax bodySyntax, TextSpan span)
        {
            var iterable = _expressionResolver.ResolveExpression(rangeClause.Expression);

            TypeSymbol? keyType = null;
            TypeSymbol? valueType = null;

            if (iterable.Type != TypeSymbol.Error)
            {
                var resolved = iterable.Type.Resolved();
                // Also check underlying type for named types (e.g. type Float64Slice []float64)
                // Chain through multiple levels (e.g. type HTTPHeadersCarrier http.Header
                // where http.Header is type Header map[string][]string)
                for (int depth = 0; depth < 10; depth++)
                {
                    if (resolved is SliceTypeSymbol || resolved is ArrayTypeSymbol
                        || resolved is MapTypeSymbol || resolved is ChannelTypeSymbol
                        || resolved is PointerTypeSymbol)
                        break;
                    if (resolved.UnderlyingType != null && resolved.UnderlyingType != resolved)
                        resolved = resolved.UnderlyingType;
                    else
                        break;
                }
                if (resolved is SliceTypeSymbol sliceType)
                {
                    keyType = BuiltinTypes.Int;
                    valueType = sliceType.ElementType;
                }
                else if (resolved is ArrayTypeSymbol arrayType)
                {
                    keyType = BuiltinTypes.Int;
                    valueType = arrayType.ElementType;
                }
                else if (resolved is MapTypeSymbol mapType)
                {
                    keyType = mapType.KeyType;
                    valueType = mapType.ValueType;
                }
                else if (resolved.TypeKind == TypeKind.String
                         || resolved.TypeKind == TypeKind.UntypedString)
                {
                    keyType = BuiltinTypes.Int;
                    valueType = BuiltinTypes.Rune;
                }
                else if (resolved is ChannelTypeSymbol chanType)
                {
                    // Channel range yields only one variable (the received value)
                    keyType = chanType.ElementType;
                    valueType = null;
                }
                else if (resolved is PointerTypeSymbol ptrType)
                {
                    // Auto-dereference pointer to array/slice
                    var inner = ptrType.ElementType.Resolved();
                    if (inner is ArrayTypeSymbol ptrArr)
                    {
                        keyType = BuiltinTypes.Int;
                        valueType = ptrArr.ElementType;
                    }
                    else if (inner is SliceTypeSymbol ptrSlice)
                    {
                        keyType = BuiltinTypes.Int;
                        valueType = ptrSlice.ElementType;
                    }
                    else
                    {
                        _context.Errors.ReportError(span, ErrorCode.InvalidRange,
                            $"Cannot range over type '{iterable.Type.Name}'");
                    }
                }
                else if (TypeChecker.IsInteger(iterable.Type))
                {
                    // Go 1.22: for i := range N — iterate 0..N-1
                    keyType = BuiltinTypes.Int;
                    valueType = null;
                }
                else if (resolved is TypeParameterSymbol rangeTypeParam)
                {
                    // Type parameter with structural constraint (e.g. ~[]E, ~map[K]V)
                    var structural = TypeChecker.GetConstraintStructuralType(rangeTypeParam);
                    if (structural != null)
                    {
                        var resolvedStructural = structural.Resolved();
                        if (resolvedStructural == structural && structural.UnderlyingType != null)
                            resolvedStructural = structural.UnderlyingType;
                        if (resolvedStructural is SliceTypeSymbol csSlice)
                        {
                            keyType = BuiltinTypes.Int;
                            valueType = csSlice.ElementType;
                        }
                        else if (resolvedStructural is ArrayTypeSymbol csArray)
                        {
                            keyType = BuiltinTypes.Int;
                            valueType = csArray.ElementType;
                        }
                        else if (resolvedStructural is MapTypeSymbol csMap)
                        {
                            keyType = csMap.KeyType;
                            valueType = csMap.ValueType;
                        }
                        else if (resolvedStructural is ChannelTypeSymbol csChan)
                        {
                            keyType = csChan.ElementType;
                            valueType = null;
                        }
                        else if (resolvedStructural.TypeKind == TypeKind.String)
                        {
                            keyType = BuiltinTypes.Int;
                            valueType = BuiltinTypes.Rune;
                        }
                        else
                        {
                            _context.Errors.ReportError(span, ErrorCode.InvalidRange,
                                $"Cannot range over type '{iterable.Type.Name}'");
                        }
                    }
                    else
                    {
                        _context.Errors.ReportError(span, ErrorCode.InvalidRange,
                            $"Cannot range over type '{iterable.Type.Name}'");
                    }
                }
                else if (resolved is InterfaceTypeSymbol
                    || iterable.Type is InterfaceTypeSymbol
                    || iterable.Type.TypeKind == TypeKind.Interface)
                {
                    // Range over interface{} — valid at runtime if value is iterable
                    keyType = BuiltinTypes.EmptyInterface;
                    valueType = BuiltinTypes.EmptyInterface;
                }
                else
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidRange,
                        $"Cannot range over type '{iterable.Type.Name}'");
                }
            }

            _context.PushScope("for-range");

            LocalSymbol? keySymbol = null;
            LocalSymbol? valueSymbol = null;

            if (rangeClause.Variables.HasValue)
            {
                var vars = rangeClause.Variables.Value;
                bool isDeclare = rangeClause.AssignOrDeclare?.Kind == SyntaxKind.ColonEqualsToken;

                if (vars.Count >= 1)
                {
                    var keyName = _context.GetIdentifierName(vars[0]);
                    if (keyName != null && keyName != "_")
                    {
                        keySymbol = new LocalSymbol(keyName, keyType ?? TypeSymbol.Error);
                        if (isDeclare)
                        {
                            _context.Scope.TryDeclare(keySymbol);
                            _context.TrackLocal(keySymbol, span);
                        }
                    }
                }

                if (vars.Count >= 2)
                {
                    var valueName = _context.GetIdentifierName(vars[1]);
                    if (valueName != null && valueName != "_")
                    {
                        valueSymbol = new LocalSymbol(valueName, valueType ?? TypeSymbol.Error);
                        if (isDeclare)
                        {
                            _context.Scope.TryDeclare(valueSymbol);
                            _context.TrackLocal(valueSymbol, span);
                        }
                    }
                }
            }

            _context.LoopDepth++;
            var body = ResolveBlock(bodySyntax);
            _context.LoopDepth--;

            _context.PopScope();

            return new ForRangeStatement(keySymbol, valueSymbol, iterable, body, span);
        }

        private DeferStatement ResolveDeferStatement(DeferStatementSyntax syntax)
        {
            var expr = _expressionResolver.ResolveExpression(syntax.Expression);
            return new DeferStatement(expr, _context.SpanOf(syntax));
        }

        private GoStatement ResolveGoStatement(GoStatementSyntax syntax)
        {
            var expr = _expressionResolver.ResolveExpression(syntax.Expression);
            return new GoStatement(expr, _context.SpanOf(syntax));
        }

        private SendStatement ResolveSendStatement(SendStatementSyntax syntax)
        {
            var channel = _expressionResolver.ResolveExpression(syntax.Channel);
            var value = _expressionResolver.ResolveExpression(syntax.Value);

            // Unwrap named channel types (e.g. type control chan bool → ChannelTypeSymbol)
            var chanResolved = channel.Type;
            for (int depth = 0; depth < 10 && chanResolved is not ChannelTypeSymbol; depth++)
            {
                if (chanResolved.UnderlyingType != null && chanResolved.UnderlyingType != chanResolved)
                    chanResolved = chanResolved.UnderlyingType;
                else
                    break;
            }
            if (chanResolved is ChannelTypeSymbol chanType)
            {
                if (!TypeChecker.IsAssignable(value.Type, chanType.ElementType))
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.TypeMismatch,
                        $"Cannot send '{value.Type.Name}' on channel of '{chanType.ElementType.Name}'");
                }
            }
            else if (!(chanResolved is InterfaceTypeSymbol ifaceSend && ifaceSend.Methods.Count == 0))
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.InvalidOperation,
                    $"Cannot send on non-channel type '{channel.Type.Name}'");
            }

            return new SendStatement(channel, value, _context.SpanOf(syntax));
        }

        private SelectStatement ResolveSelectStatement(SelectStatementSyntax syntax)
        {
            var span = _context.SpanOf(syntax);
            var cases = new List<SelectCase>();

            _context.SwitchDepth++;
            foreach (var clause in syntax.Clauses)
            {
                cases.Add(ResolveSelectCase(clause));
            }
            _context.SwitchDepth--;

            return new SelectStatement(cases, span);
        }

        private SelectCase ResolveSelectCase(CommClauseSyntax clause)
        {
            var span = _context.SpanOf(clause);

            // Default case
            if (clause.CaseOrDefault.Kind == SyntaxKind.DefaultKeyword)
            {
                _context.PushScope("select-default");
                var body = ResolveStatementList(clause.Statements);
                _context.PopScope();
                return new SelectCase(SelectCaseKind.Default, null, null, null, null, body, span);
            }

            _context.PushScope("select-case");

            var commStmt = clause.CommStatement!;
            SelectCase result;

            if (commStmt is SendStatementSyntax sendSyntax)
            {
                // case ch <- value:
                var channel = _expressionResolver.ResolveExpression(sendSyntax.Channel);
                var value = _expressionResolver.ResolveExpression(sendSyntax.Value);
                var body = ResolveStatementList(clause.Statements);
                result = new SelectCase(SelectCaseKind.Send, channel, value, null, null, body, span);
            }
            else if (commStmt is ShortVarDeclarationSyntax shortVarSyntax)
            {
                // case v := <-ch: or case v, ok := <-ch:
                var recvExpr = ExtractReceiveExpression(shortVarSyntax.Right[0]);
                if (recvExpr == null)
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                        "Select case must be a channel receive operation");
                    var body = ResolveStatementList(clause.Statements);
                    _context.PopScope();
                    return new SelectCase(SelectCaseKind.Default, null, null, null, null, body, span);
                }

                var channel = recvExpr.Channel;
                var elemType = recvExpr.ElementType;

                LocalSymbol? valueLocal = null;
                LocalSymbol? okLocal = null;

                if (shortVarSyntax.Left.Count >= 1)
                {
                    var nameExpr = shortVarSyntax.Left[0] as IdentifierNameSyntax;
                    if (nameExpr != null && nameExpr.Identifier.Text != "_")
                    {
                        valueLocal = new LocalSymbol(nameExpr.Identifier.Text, elemType);
                        _context.Scope.TryDeclare(valueLocal);
                        _context.TrackLocal(valueLocal, _context.SpanOf(clause));
                    }
                }

                if (shortVarSyntax.Left.Count >= 2)
                {
                    var nameExpr = shortVarSyntax.Left[1] as IdentifierNameSyntax;
                    if (nameExpr != null && nameExpr.Identifier.Text != "_")
                    {
                        okLocal = new LocalSymbol(nameExpr.Identifier.Text, BuiltinTypes.Bool);
                        _context.Scope.TryDeclare(okLocal);
                        _context.TrackLocal(okLocal, _context.SpanOf(clause));
                    }
                }

                var body2 = ResolveStatementList(clause.Statements);
                result = new SelectCase(SelectCaseKind.Receive, channel, null, valueLocal, okLocal, body2, span);
            }
            else if (commStmt is AssignmentStatementSyntax assignSyntax)
            {
                // case err = <-ch: or case v, ok = <-ch:
                var recvExpr = ExtractReceiveExpression(assignSyntax.Right[0]);
                if (recvExpr == null)
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                        "Select case must be a channel receive operation");
                    var body = ResolveStatementList(clause.Statements);
                    _context.PopScope();
                    return new SelectCase(SelectCaseKind.Default, null, null, null, null, body, span);
                }

                // Resolve the left-hand side targets as assignments
                foreach (var leftExpr in assignSyntax.Left)
                {
                    _expressionResolver.ResolveExpression(leftExpr);
                }

                var body4 = ResolveStatementList(clause.Statements);
                result = new SelectCase(SelectCaseKind.Receive, recvExpr.Channel, null, null, null, body4, span);
            }
            else
            {
                // Bare receive: case <-ch:
                // The comm statement is an ExpressionStatement wrapping a receive expression
                Expression? channelExpr = null;

                if (commStmt is ExpressionStatementSyntax exprStmt)
                {
                    var resolved = _expressionResolver.ResolveExpression(exprStmt.Expression);
                    if (resolved is ReceiveExpression recv)
                    {
                        channelExpr = recv.Channel;
                    }
                }

                if (channelExpr == null)
                {
                    // Try resolving the comm statement as an expression directly
                    if (commStmt is ExpressionSyntax exprSyntax)
                    {
                        var resolved = _expressionResolver.ResolveExpression(exprSyntax);
                        if (resolved is ReceiveExpression recv)
                        {
                            channelExpr = recv.Channel;
                        }
                    }
                }

                if (channelExpr == null)
                {
                    // Last resort: check the syntax tree for <-expr pattern
                    // This handles cases where the receive expression resolution
                    // failed (e.g. type mismatch in channel function call)
                    ExpressionSyntax? receiveOp = null;
                    if (commStmt is ExpressionStatementSyntax es2
                        && es2.Expression is UnaryExpressionSyntax unary
                        && unary.OperatorToken.Kind == SyntaxKind.LessThanMinusToken)
                    {
                        receiveOp = unary.Operand;
                    }
                    else if (commStmt is UnaryExpressionSyntax unary2
                        && unary2.OperatorToken.Kind == SyntaxKind.LessThanMinusToken)
                    {
                        receiveOp = unary2.Operand;
                    }

                    if (receiveOp != null)
                    {
                        // It's structurally a receive — resolve the operand as the channel
                        var resolved = _expressionResolver.ResolveExpression(receiveOp);
                        channelExpr = resolved;
                    }
                }

                if (channelExpr == null)
                {
                    _context.Errors.ReportError(span, ErrorCode.InvalidOperation,
                        "Select case must be a send or receive operation");
                    var body = ResolveStatementList(clause.Statements);
                    _context.PopScope();
                    return new SelectCase(SelectCaseKind.Default, null, null, null, null, body, span);
                }

                var body3 = ResolveStatementList(clause.Statements);
                result = new SelectCase(SelectCaseKind.Receive, channelExpr, null, null, null, body3, span);
            }

            _context.PopScope();
            return result;
        }

        private ReceiveExpression? ExtractReceiveExpression(ExpressionSyntax syntax)
        {
            var resolved = _expressionResolver.ResolveExpression(syntax);
            return resolved as ReceiveExpression;
        }

        private IReadOnlyList<AstNode> ResolveStatementList(IReadOnlyList<SyntaxNode> statements)
        {
            var result = new List<AstNode>();
            bool unreachableReported = false;
            foreach (var stmtSyntax in statements)
            {
                var bound = ResolveStatement(stmtSyntax);
                if (bound != null)
                {
                    if (!unreachableReported && CheckUnreachable(result, bound))
                    {
                        unreachableReported = true;
                    }

                    result.Add(bound);
                }
            }
            return result;
        }

        private bool CheckUnreachable(List<AstNode> previous, AstNode newStatement)
        {
            if (previous.Count == 0)
            {
                return false;
            }

            if (FlowAnalyzer.IsTerminating(previous[previous.Count - 1]))
            {
                // Labeled statements are reachable via goto, don't warn
                if (newStatement is LabeledStatement)
                {
                    return false;
                }

                _context.Errors.ReportWarning(
                    newStatement.Span,
                    ErrorCode.UnreachableCode,
                    "Unreachable code");
                return true;
            }

            return false;
        }

        private AstNode? ResolveLocalTypeDeclaration(TypeDeclarationSyntax syntax)
        {
            // Handle type declarations inside function bodies
            foreach (var spec in syntax.Specs)
            {
                var name = spec.Name.Text;

                if (spec.AssignToken != null)
                {
                    // Type alias
                    var underlying = _typeResolver.ResolveType(spec.Type);
                    if (underlying == null) underlying = TypeSymbol.Error;
                    var alias = new TypeSymbol(name, underlying.TypeKind, underlying) { IsAlias = true };
                    _context.Scope.TryDeclare(alias);
                    continue;
                }

                if (spec.Type is StructTypeSyntax structSyntax)
                {
                    var structType = new StructTypeSymbol(name, new List<FieldSymbol>());
                    _context.Scope.TryDeclare(structType);

                    var fields = new List<FieldSymbol>();
                    int ordinal = 0;
                    foreach (var fieldSyntax in structSyntax.Fields)
                    {
                        var fieldType = _typeResolver.ResolveType(fieldSyntax.Type);
                        if (fieldType == null) fieldType = TypeSymbol.Error;

                        if (fieldSyntax.Names.HasValue)
                        {
                            for (int i = 0; i < fieldSyntax.Names.Value.Count; i++)
                            {
                                var fieldName = fieldSyntax.Names.Value[i].Text;
                                fields.Add(new FieldSymbol(fieldName, fieldType, ordinal++));
                            }
                        }
                        else
                        {
                            var embeddedName = fieldType.Name;
                            fields.Add(new FieldSymbol(embeddedName, fieldType, ordinal++,
                                isEmbedded: true));
                        }
                    }

                    structType.SetFields(fields);
                    return new TypeDeclaration(structType, _context.SpanOf(spec));
                }

                if (spec.Type is InterfaceTypeSyntax ifaceSyntax)
                {
                    var ifaceType = new InterfaceTypeSymbol(name, new List<MethodSymbol>());
                    _context.Scope.TryDeclare(ifaceType);

                    var methods = new List<MethodSymbol>();
                    foreach (var member in ifaceSyntax.Members)
                    {
                        if (member is MethodSpecSyntax methodSpec)
                        {
                            var parameters = _typeResolver.ResolveParameterList(methodSpec.Parameters);
                            var returnTypes = _typeResolver.ResolveResultTypes(methodSpec.Result);
                            var method = new MethodSymbol(methodSpec.Name.Text, ifaceType, false,
                                parameters, returnTypes);
                            methods.Add(method);
                        }
                    }

                    ifaceType.SetMethods(methods);
                    return new TypeDeclaration(ifaceType, _context.SpanOf(spec));
                }

                // Non-struct type definition (e.g., type MyInt int)
                var resolvedType = _typeResolver.ResolveType(spec.Type);
                if (resolvedType == null) resolvedType = TypeSymbol.Error;
                var namedType = new TypeSymbol(name, resolvedType.TypeKind, resolvedType);
                _context.Scope.TryDeclare(namedType);
                return new TypeDeclaration(namedType, _context.SpanOf(spec));
            }

            return null;
        }
    }
}

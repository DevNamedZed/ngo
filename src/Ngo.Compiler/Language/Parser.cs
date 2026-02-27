// -----------------------------------------------------------------------
// <copyright file="Parser.cs" company="Ziad">
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
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Language
{
    public sealed class Parser
    {
        private readonly IReadOnlyList<SyntaxToken> _tokens;
        private readonly ErrorCollector _errors = new();
        private int _pos;
        private bool _allowCompositeLit = true;

        public IReadOnlyList<CompileError> Errors => _errors.ToReadOnlyList();

        public Parser(string source)
        {
            var lexer = new Lexer(source);
            _tokens = lexer.LexAll();
            _pos = 0;
        }

        public Parser(IReadOnlyList<SyntaxToken> tokens)
        {
            _tokens = tokens;
            _pos = 0;
        }

        // ================================================================
        // Token access
        // ================================================================

        private SyntaxToken Current => _pos < _tokens.Count
            ? _tokens[_pos]
            : _tokens[_tokens.Count - 1]; // EOF

        private SyntaxToken Peek(int offset)
        {
            int index = _pos + offset;
            return index < _tokens.Count
                ? _tokens[index]
                : _tokens[_tokens.Count - 1];
        }

        private SyntaxToken Advance()
        {
            var token = Current;
            if (_pos < _tokens.Count - 1) // Don't advance past EOF
                _pos++;
            return token;
        }

        private SyntaxToken Expect(SyntaxKind kind)
        {
            if (Current.Kind == kind)
                return Advance();

            // Missing token — synthesize one and report error
            var span = new TextSpan(Current.Position, Current.Text.Length);
            _errors.ReportError(span, ErrorCode.TokenExpected,
                $"Expected '{kind}', got '{Current.Kind}'");
            return new SyntaxToken(kind, "", Current.Position);
        }

        private bool At(SyntaxKind kind) => Current.Kind == kind;

        private bool AtSemicolon() => Current.Kind == SyntaxKind.SemicolonToken;

        private SyntaxToken ExpectSemicolon() => Expect(SyntaxKind.SemicolonToken);

        private void SkipSemicolon()
        {
            if (AtSemicolon()) Advance();
        }

        // ================================================================
        // Top-level: SourceFile
        // ================================================================

        public SourceFileSyntax ParseSourceFile()
        {
            var packageClause = ParsePackageClause();
            SkipSemicolon();

            var imports = new List<ImportDeclarationSyntax>();
            while (At(SyntaxKind.ImportKeyword))
            {
                imports.Add(ParseImportDeclaration());
                SkipSemicolon();
            }

            var members = new List<SyntaxNode>();
            while (!At(SyntaxKind.EndOfFileToken))
            {
                members.Add(ParseTopLevelDeclaration());
                SkipSemicolon();
            }

            var eof = Expect(SyntaxKind.EndOfFileToken);
            return new SourceFileSyntax(packageClause, imports, members, eof);
        }

        // ================================================================
        // Package clause
        // ================================================================

        private PackageClauseSyntax ParsePackageClause()
        {
            var keyword = Expect(SyntaxKind.PackageKeyword);
            var name = Expect(SyntaxKind.IdentifierToken);
            return new PackageClauseSyntax(keyword, name);
        }

        // ================================================================
        // Import
        // ================================================================

        private ImportDeclarationSyntax ParseImportDeclaration()
        {
            var keyword = Expect(SyntaxKind.ImportKeyword);

            if (At(SyntaxKind.OpenParenToken))
            {
                var open = Advance();
                var specs = new List<ImportSpecSyntax>();
                while (!At(SyntaxKind.CloseParenToken) && !At(SyntaxKind.EndOfFileToken))
                {
                    specs.Add(ParseImportSpec());
                    SkipSemicolon();
                }
                var close = Expect(SyntaxKind.CloseParenToken);
                return new ImportDeclarationSyntax(keyword, open, specs, close);
            }

            var singleSpec = ParseImportSpec();
            return new ImportDeclarationSyntax(keyword, null, new[] { singleSpec }, null);
        }

        private ImportSpecSyntax ParseImportSpec()
        {
            SyntaxToken? alias = null;

            // Check for alias: identifier or "."
            if (At(SyntaxKind.IdentifierToken) || At(SyntaxKind.DotToken))
            {
                // Look ahead to see if this is an alias or the path
                if (Current.Kind == SyntaxKind.IdentifierToken &&
                    (Peek(1).Kind == SyntaxKind.StringLiteralToken || Peek(1).Kind == SyntaxKind.RawStringLiteralToken))
                {
                    alias = Advance();
                }
                else if (Current.Kind == SyntaxKind.DotToken)
                {
                    alias = Advance();
                }
            }

            var path = Current.Kind == SyntaxKind.StringLiteralToken || Current.Kind == SyntaxKind.RawStringLiteralToken
                ? Advance()
                : Expect(SyntaxKind.StringLiteralToken);

            return new ImportSpecSyntax(alias, path);
        }

        // ================================================================
        // Top-level declarations
        // ================================================================

        private SyntaxNode ParseTopLevelDeclaration()
        {
            switch (Current.Kind)
            {
                case SyntaxKind.FuncKeyword:
                    return ParseFunctionOrMethodDeclaration();
                case SyntaxKind.TypeKeyword:
                    return ParseTypeDeclaration();
                case SyntaxKind.VarKeyword:
                    return ParseVarDeclaration();
                case SyntaxKind.ConstKeyword:
                    return ParseConstDeclaration();
                default:
                    return ParseErrorNode("expected declaration");
            }
        }

        // ================================================================
        // Function / Method declaration
        // ================================================================

        private SyntaxNode ParseFunctionOrMethodDeclaration()
        {
            var funcKeyword = Expect(SyntaxKind.FuncKeyword);

            // Method: func (receiver) name(params) result { body }
            if (At(SyntaxKind.OpenParenToken))
            {
                var receiver = ParseParameterList();
                var name = Expect(SyntaxKind.IdentifierToken);
                var parameters = ParseParameterList();
                var result = ParseResult();
                var body = At(SyntaxKind.OpenBraceToken) ? ParseBlock() : null;
                return new MethodDeclarationSyntax(funcKeyword, receiver, name, parameters, result, body);
            }

            // Function: func name(params) result { body }
            {
                var name = Expect(SyntaxKind.IdentifierToken);
                var parameters = ParseParameterList();
                var result = ParseResult();
                var body = At(SyntaxKind.OpenBraceToken) ? ParseBlock() : null;
                return new FunctionDeclarationSyntax(funcKeyword, name, parameters, result, body);
            }
        }

        private ParameterListSyntax ParseParameterList()
        {
            var open = Expect(SyntaxKind.OpenParenToken);
            var parameters = ParseParameterDecls();
            var close = Expect(SyntaxKind.CloseParenToken);
            return new ParameterListSyntax(open, parameters, close);
        }

        private SeparatedSyntaxList<ParameterSyntax> ParseParameterDecls()
        {
            if (At(SyntaxKind.CloseParenToken))
                return SeparatedSyntaxList<ParameterSyntax>.Empty;

            var builder = new List<SyntaxNode>();
            builder.Add(ParseParameter());

            while (At(SyntaxKind.CommaToken))
            {
                builder.Add(Advance()); // comma
                builder.Add(ParseParameter());
            }

            return new SeparatedSyntaxList<ParameterSyntax>(builder);
        }

        private ParameterSyntax ParseParameter()
        {
            // Go parameter parsing is ambiguous:
            //   (x int)       — named
            //   (int)         — unnamed, just a type
            //   (x, y int)    — multiple names, shared type
            //   (...int)      — variadic
            //   (x ...int)    — named variadic
            //
            // Strategy: try to parse identifiers. If followed by a type, they're names.
            // If followed by comma or close-paren, they might be types (unnamed params).

            SyntaxToken? ellipsis = null;

            // Variadic: ...type
            if (At(SyntaxKind.EllipsisToken))
            {
                ellipsis = Advance();
                var variadicType = ParseType();
                return new ParameterSyntax(null, ellipsis, variadicType);
            }

            // Try to parse an identifier list
            // Look ahead past potential comma-separated identifiers to find a type
            if (At(SyntaxKind.IdentifierToken))
            {
                int offset = 1;
                while (Peek(offset).Kind == SyntaxKind.CommaToken
                    && Peek(offset + 1).Kind == SyntaxKind.IdentifierToken)
                {
                    offset += 2;
                }

                // If a type (or ellipsis) follows the identifier(s), they're parameter names
                if (IsTypeStart(Peek(offset).Kind) || Peek(offset).Kind == SyntaxKind.EllipsisToken)
                {
                    var names = ParseIdentifierTokenList();

                    if (At(SyntaxKind.EllipsisToken))
                        ellipsis = Advance();

                    var type = ParseType();
                    return new ParameterSyntax(names, ellipsis, type);
                }
            }

            // Unnamed parameter: just a type
            var paramType = ParseType();
            return new ParameterSyntax(null, null, paramType);
        }

        private SeparatedSyntaxList<SyntaxToken> ParseIdentifierTokenList()
        {
            var builder = new List<SyntaxNode>();
            builder.Add(Expect(SyntaxKind.IdentifierToken));

            while (At(SyntaxKind.CommaToken) && Peek(1).Kind == SyntaxKind.IdentifierToken)
            {
                builder.Add(Advance()); // comma
                builder.Add(Advance()); // identifier
            }

            return new SeparatedSyntaxList<SyntaxToken>(builder);
        }

        /// <summary>
        /// Parses the result portion of a function signature.
        /// Returns ParameterListSyntax for (type, type), ExpressionSyntax for a single type, or null.
        /// </summary>
        private SyntaxNode? ParseResult()
        {
            if (At(SyntaxKind.OpenParenToken))
                return ParseParameterList();

            if (IsTypeStart(Current.Kind))
                return ParseType();

            return null;
        }

        // ================================================================
        // Type declaration
        // ================================================================

        private TypeDeclarationSyntax ParseTypeDeclaration()
        {
            var keyword = Expect(SyntaxKind.TypeKeyword);

            if (At(SyntaxKind.OpenParenToken))
            {
                var open = Advance();
                var specs = new List<TypeSpecSyntax>();
                while (!At(SyntaxKind.CloseParenToken) && !At(SyntaxKind.EndOfFileToken))
                {
                    specs.Add(ParseTypeSpec());
                    SkipSemicolon();
                }
                var close = Expect(SyntaxKind.CloseParenToken);
                return new TypeDeclarationSyntax(keyword, open, specs, close);
            }

            var singleSpec = ParseTypeSpec();
            return new TypeDeclarationSyntax(keyword, null, new[] { singleSpec }, null);
        }

        private TypeSpecSyntax ParseTypeSpec()
        {
            var name = Expect(SyntaxKind.IdentifierToken);

            SyntaxToken? assign = null;
            if (At(SyntaxKind.EqualsToken))
                assign = Advance();

            var type = ParseType();
            return new TypeSpecSyntax(name, assign, type);
        }

        // ================================================================
        // Var declaration
        // ================================================================

        private VarDeclarationSyntax ParseVarDeclaration()
        {
            var keyword = Expect(SyntaxKind.VarKeyword);

            if (At(SyntaxKind.OpenParenToken))
            {
                var open = Advance();
                var specs = new List<VarSpecSyntax>();
                while (!At(SyntaxKind.CloseParenToken) && !At(SyntaxKind.EndOfFileToken))
                {
                    specs.Add(ParseVarSpec());
                    SkipSemicolon();
                }
                var close = Expect(SyntaxKind.CloseParenToken);
                return new VarDeclarationSyntax(keyword, open, specs, close);
            }

            var singleSpec = ParseVarSpec();
            return new VarDeclarationSyntax(keyword, null, new[] { singleSpec }, null);
        }

        private VarSpecSyntax ParseVarSpec()
        {
            var names = ParseIdentifierTokenList();

            ExpressionSyntax? type = null;
            SyntaxToken? equals = null;
            SeparatedSyntaxList<ExpressionSyntax>? values = null;

            if (!At(SyntaxKind.EqualsToken))
            {
                type = ParseType();
            }

            if (At(SyntaxKind.EqualsToken))
            {
                equals = Advance();
                values = ParseExpressionList();
            }

            return new VarSpecSyntax(names, type, equals, values);
        }

        // ================================================================
        // Const declaration
        // ================================================================

        private ConstDeclarationSyntax ParseConstDeclaration()
        {
            var keyword = Expect(SyntaxKind.ConstKeyword);

            if (At(SyntaxKind.OpenParenToken))
            {
                var open = Advance();
                var specs = new List<ConstSpecSyntax>();
                while (!At(SyntaxKind.CloseParenToken) && !At(SyntaxKind.EndOfFileToken))
                {
                    specs.Add(ParseConstSpec());
                    SkipSemicolon();
                }
                var close = Expect(SyntaxKind.CloseParenToken);
                return new ConstDeclarationSyntax(keyword, open, specs, close);
            }

            var singleSpec = ParseConstSpec();
            return new ConstDeclarationSyntax(keyword, null, new[] { singleSpec }, null);
        }

        private ConstSpecSyntax ParseConstSpec()
        {
            var names = ParseIdentifierTokenList();

            ExpressionSyntax? type = null;
            SyntaxToken? equals = null;
            SeparatedSyntaxList<ExpressionSyntax>? values = null;

            // const names Type = values  OR  const names = values  OR  const names (iota pattern)
            if (IsTypeStart(Current.Kind) && Current.Kind != SyntaxKind.EqualsToken)
            {
                // Could be a type before =
                if (!At(SyntaxKind.SemicolonToken) && !At(SyntaxKind.CloseParenToken) && !At(SyntaxKind.EqualsToken))
                {
                    type = ParseType();
                }
            }

            if (At(SyntaxKind.EqualsToken))
            {
                equals = Advance();
                values = ParseExpressionList();
            }

            return new ConstSpecSyntax(names, type, equals, values);
        }

        // ================================================================
        // Statements
        // ================================================================

        private BlockSyntax ParseBlock()
        {
            var open = Expect(SyntaxKind.OpenBraceToken);
            var statements = new List<SyntaxNode>();

            while (!At(SyntaxKind.CloseBraceToken) && !At(SyntaxKind.EndOfFileToken))
            {
                statements.Add(ParseStatement());
                SkipSemicolon();
            }

            var close = Expect(SyntaxKind.CloseBraceToken);
            return new BlockSyntax(open, statements, close);
        }

        private SyntaxNode ParseStatement()
        {
            switch (Current.Kind)
            {
                case SyntaxKind.VarKeyword:
                    return ParseVarDeclaration();
                case SyntaxKind.ConstKeyword:
                    return ParseConstDeclaration();
                case SyntaxKind.TypeKeyword:
                    return ParseTypeDeclaration();
                case SyntaxKind.ReturnKeyword:
                    return ParseReturnStatement();
                case SyntaxKind.IfKeyword:
                    return ParseIfStatement();
                case SyntaxKind.ForKeyword:
                    return ParseForStatement();
                case SyntaxKind.SwitchKeyword:
                    return ParseSwitchStatement();
                case SyntaxKind.SelectKeyword:
                    return ParseSelectStatement();
                case SyntaxKind.GoKeyword:
                    return ParseGoStatement();
                case SyntaxKind.DeferKeyword:
                    return ParseDeferStatement();
                case SyntaxKind.OpenBraceToken:
                    return ParseBlock();
                case SyntaxKind.BreakKeyword:
                case SyntaxKind.ContinueKeyword:
                case SyntaxKind.GotoKeyword:
                case SyntaxKind.FallthroughKeyword:
                    return ParseBranchStatement();
                case SyntaxKind.SemicolonToken:
                    return new EmptyStatementSyntax(Advance());
                default:
                    return ParseSimpleStatement();
            }
        }

        private SyntaxNode ParseSimpleStatement()
        {
            // Parse expression(s), then look for assignment operators, :=, <-, ++, --
            var expr = ParseExpression();

            // Label: identifier followed by colon
            if (expr is IdentifierNameSyntax && At(SyntaxKind.ColonToken))
            {
                var ident = ((IdentifierNameSyntax)expr).Identifier;
                var colon = Advance();
                var stmt = ParseStatement();
                return new LabeledStatementSyntax(ident, colon, stmt);
            }

            // Multi-expression list: collect if comma follows, then check for := or =
            if (At(SyntaxKind.CommaToken))
            {
                var left = CollectExpressionList(expr);

                if (At(SyntaxKind.ColonEqualsToken))
                {
                    var colonEquals = Advance();
                    var right = ParseExpressionList();
                    return new ShortVarDeclarationSyntax(left, colonEquals, right);
                }

                if (IsAssignmentOperator(Current.Kind))
                {
                    var op = Advance();
                    var right = ParseExpressionList();
                    return new AssignmentStatementSyntax(left, op, right);
                }

                return new ExpressionStatementSyntax(expr);
            }

            // Short var declaration: expr :=
            if (At(SyntaxKind.ColonEqualsToken))
            {
                var left = new SeparatedSyntaxList<ExpressionSyntax>(new List<SyntaxNode> { expr });
                var colonEquals = Advance();
                var right = ParseExpressionList();
                return new ShortVarDeclarationSyntax(left, colonEquals, right);
            }

            // Assignment: expr = expr, expr += expr, etc.
            if (IsAssignmentOperator(Current.Kind))
            {
                var left = new SeparatedSyntaxList<ExpressionSyntax>(new List<SyntaxNode> { expr });
                var op = Advance();
                var right = ParseExpressionList();
                return new AssignmentStatementSyntax(left, op, right);
            }

            // Send statement: channel <- value
            if (At(SyntaxKind.LessThanMinusToken))
            {
                var arrow = Advance();
                var value = ParseExpression();
                return new SendStatementSyntax(expr, arrow, value);
            }

            // Increment/decrement: expr++ or expr--
            if (At(SyntaxKind.PlusPlusToken) || At(SyntaxKind.MinusMinusToken))
            {
                var op = Advance();
                return new IncDecStatementSyntax(expr, op);
            }

            // Expression statement
            return new ExpressionStatementSyntax(expr);
        }

        private SyntaxNode ParseSimpleStatementNoCompositeLit()
        {
            bool saved = _allowCompositeLit;
            _allowCompositeLit = false;
            try
            {
                return ParseSimpleStatement();
            }
            finally
            {
                _allowCompositeLit = saved;
            }
        }

        /// <summary>
        /// If the first expression was already parsed, check if there are more comma-separated
        /// expressions to form a list (for multi-assignment).
        /// </summary>
        private SeparatedSyntaxList<ExpressionSyntax> CollectExpressionList(ExpressionSyntax first)
        {
            var builder = new List<SyntaxNode> { first };

            while (At(SyntaxKind.CommaToken))
            {
                builder.Add(Advance()); // comma
                builder.Add(ParseExpression());
            }

            return new SeparatedSyntaxList<ExpressionSyntax>(builder);
        }

        // ----------------------------------------------------------------
        // Control flow statements
        // ----------------------------------------------------------------

        private ReturnStatementSyntax ParseReturnStatement()
        {
            var keyword = Expect(SyntaxKind.ReturnKeyword);

            SeparatedSyntaxList<ExpressionSyntax> values;
            if (AtSemicolon() || At(SyntaxKind.CloseBraceToken) || At(SyntaxKind.EndOfFileToken))
            {
                values = SeparatedSyntaxList<ExpressionSyntax>.Empty;
            }
            else
            {
                values = ParseExpressionList();
            }

            return new ReturnStatementSyntax(keyword, values);
        }

        private IfStatementSyntax ParseIfStatement()
        {
            var ifKeyword = Expect(SyntaxKind.IfKeyword);

            // Try to parse init; condition
            SyntaxNode? init = null;
            SyntaxToken? initSemicolon = null;

            // Composite literals not allowed here — { would be ambiguous with block
            var expr = ParseExpressionNoCompositeLit();

            if (At(SyntaxKind.CommaToken))
            {
                // Multi-expression list: v, ok := m[key] or a, b = f()
                var left = CollectExpressionList(expr);
                if (At(SyntaxKind.ColonEqualsToken))
                {
                    var colonEquals = Advance();
                    var right = ParseExpressionList();
                    init = new ShortVarDeclarationSyntax(left, colonEquals, right);
                }
                else if (IsAssignmentOperator(Current.Kind))
                {
                    var op = Advance();
                    var right = ParseExpressionList();
                    init = new AssignmentStatementSyntax(left, op, right);
                }
                else
                {
                    init = new ExpressionStatementSyntax(expr);
                }

                initSemicolon = Expect(SyntaxKind.SemicolonToken);
                expr = ParseExpressionNoCompositeLit();
            }
            else if (At(SyntaxKind.ColonEqualsToken) || IsAssignmentOperator(Current.Kind) ||
                At(SyntaxKind.PlusPlusToken) || At(SyntaxKind.MinusMinusToken))
            {
                // This is a statement (short var decl, assignment, or inc/dec) used as init
                init = WrapSimpleStatement(expr);
                initSemicolon = Expect(SyntaxKind.SemicolonToken);
                expr = ParseExpressionNoCompositeLit();
            }
            else if (AtSemicolon())
            {
                // Expression statement as init (e.g., if f(); cond {})
                init = new ExpressionStatementSyntax(expr);
                initSemicolon = Advance();
                expr = ParseExpressionNoCompositeLit();
            }

            var condition = expr;
            var body = ParseBlock();

            SyntaxToken? elseKeyword = null;
            SyntaxNode? elseBody = null;

            // Skip auto-semicolons after } before checking for else
            SkipSemicolon();

            if (At(SyntaxKind.ElseKeyword))
            {
                elseKeyword = Advance();
                if (At(SyntaxKind.IfKeyword))
                    elseBody = ParseIfStatement();
                else
                    elseBody = ParseBlock();
            }

            return new IfStatementSyntax(ifKeyword, init, initSemicolon, condition, body, elseKeyword, elseBody);
        }

        private ForStatementSyntax ParseForStatement()
        {
            var forKeyword = Expect(SyntaxKind.ForKeyword);

            // Infinite loop: for { }
            if (At(SyntaxKind.OpenBraceToken))
            {
                var body = ParseBlock();
                return new ForStatementSyntax(forKeyword, null, null, null, null, null, null, body);
            }

            // C-style for with no init: for ; cond ; post { }
            if (AtSemicolon())
            {
                var semi1 = Advance();
                ExpressionSyntax? condition = null;
                if (!AtSemicolon())
                    condition = ParseExpressionNoCompositeLit();
                var semi2 = Expect(SyntaxKind.SemicolonToken);
                SyntaxNode? post = null;
                if (!At(SyntaxKind.OpenBraceToken))
                    post = ParseSimpleStatementNoCompositeLit();
                var body = ParseBlock();
                return new ForStatementSyntax(forKeyword, null, semi1, condition, semi2, post, null, body);
            }

            // Check for range clause: for k, v := range expr { }
            // or: for range expr { }
            if (At(SyntaxKind.RangeKeyword))
            {
                var rangeClause = ParseRangeClause(null);
                var body = ParseBlock();
                return new ForStatementSyntax(forKeyword, null, null, null, null, null, rangeClause, body);
            }

            // Parse first expression/statement (no composite literals — { is ambiguous)
            var firstExpr = ParseExpressionNoCompositeLit();

            // Check for range clause after expression(s)
            if (At(SyntaxKind.ColonEqualsToken) || At(SyntaxKind.EqualsToken))
            {
                var left = CollectExpressionList(firstExpr);
                var assignOp = Advance();

                if (At(SyntaxKind.RangeKeyword))
                {
                    var rangeClause = ParseRangeClause(left, assignOp);
                    var body = ParseBlock();
                    return new ForStatementSyntax(forKeyword, null, null, null, null, null, rangeClause, body);
                }

                // C-style for: init ; cond ; post
                var rightExprs = ParseExpressionList();
                var initStmt = CreateAssignOrShortVarDecl(left, assignOp, rightExprs);

                var semi1 = Expect(SyntaxKind.SemicolonToken);
                ExpressionSyntax? condition = null;
                if (!AtSemicolon())
                    condition = ParseExpressionNoCompositeLit();
                var semi2 = Expect(SyntaxKind.SemicolonToken);
                SyntaxNode? post = null;
                if (!At(SyntaxKind.OpenBraceToken))
                    post = ParseSimpleStatementNoCompositeLit();
                var body2 = ParseBlock();
                return new ForStatementSyntax(forKeyword, initStmt, semi1, condition, semi2, post, null, body2);
            }

            // Check for comma (multi-value left side before :=/=)
            if (At(SyntaxKind.CommaToken))
            {
                var left = CollectExpressionList(firstExpr);
                if (At(SyntaxKind.ColonEqualsToken) || At(SyntaxKind.EqualsToken))
                {
                    var assignOp = Advance();
                    if (At(SyntaxKind.RangeKeyword))
                    {
                        var rangeClause = ParseRangeClause(left, assignOp);
                        var body = ParseBlock();
                        return new ForStatementSyntax(forKeyword, null, null, null, null, null, rangeClause, body);
                    }

                    // C-style for with multi-value init: for i, j := 0, 10; cond; post { }
                    var rightExprs = ParseExpressionList();
                    var initStmt = CreateAssignOrShortVarDecl(left, assignOp, rightExprs);
                    var semi1 = Expect(SyntaxKind.SemicolonToken);
                    ExpressionSyntax? condition = null;
                    if (!AtSemicolon())
                        condition = ParseExpressionNoCompositeLit();
                    var semi2 = Expect(SyntaxKind.SemicolonToken);
                    SyntaxNode? post = null;
                    if (!At(SyntaxKind.OpenBraceToken))
                        post = ParseSimpleStatementNoCompositeLit();
                    var body2 = ParseBlock();
                    return new ForStatementSyntax(forKeyword, initStmt, semi1, condition, semi2, post, null, body2);
                }
            }

            // C-style for with semicolons: for init; cond; post { }
            if (AtSemicolon())
            {
                var init = WrapSimpleStatement(firstExpr);
                var semi1 = Advance();
                ExpressionSyntax? condition = null;
                if (!AtSemicolon())
                    condition = ParseExpressionNoCompositeLit();
                var semi2 = Expect(SyntaxKind.SemicolonToken);
                SyntaxNode? post = null;
                if (!At(SyntaxKind.OpenBraceToken))
                    post = ParseSimpleStatementNoCompositeLit();
                var body = ParseBlock();
                return new ForStatementSyntax(forKeyword, init, semi1, condition, semi2, post, null, body);
            }

            // Simple for-condition: for expr { }
            {
                var body = ParseBlock();
                return new ForStatementSyntax(forKeyword, null, null, firstExpr, null, null, null, body);
            }
        }

        private RangeClauseSyntax ParseRangeClause(
            SeparatedSyntaxList<ExpressionSyntax>? vars = null,
            SyntaxToken? assignOp = null)
        {
            var rangeKeyword = Expect(SyntaxKind.RangeKeyword);
            var expr = ParseExpressionNoCompositeLit();
            return new RangeClauseSyntax(vars, assignOp, rangeKeyword, expr);
        }

        private SyntaxNode CreateAssignOrShortVarDecl(
            SeparatedSyntaxList<ExpressionSyntax> left,
            SyntaxToken op,
            SeparatedSyntaxList<ExpressionSyntax> right)
        {
            if (op.Kind == SyntaxKind.ColonEqualsToken)
                return new ShortVarDeclarationSyntax(left, op, right);
            return new AssignmentStatementSyntax(left, op, right);
        }

        private SyntaxNode WrapSimpleStatement(ExpressionSyntax expr)
        {
            // Check for assignment operators after the expression
            if (At(SyntaxKind.ColonEqualsToken))
            {
                var left = CollectExpressionList(expr);
                var colonEquals = Advance();
                var right = ParseExpressionList();
                return new ShortVarDeclarationSyntax(left, colonEquals, right);
            }

            if (IsAssignmentOperator(Current.Kind))
            {
                var left = CollectExpressionList(expr);
                var op = Advance();
                var right = ParseExpressionList();
                return new AssignmentStatementSyntax(left, op, right);
            }

            if (At(SyntaxKind.PlusPlusToken) || At(SyntaxKind.MinusMinusToken))
            {
                var op = Advance();
                return new IncDecStatementSyntax(expr, op);
            }

            return new ExpressionStatementSyntax(expr);
        }

        // ----------------------------------------------------------------
        // Switch
        // ----------------------------------------------------------------

        private SyntaxNode ParseSwitchStatement()
        {
            var switchKeyword = Expect(SyntaxKind.SwitchKeyword);

            SyntaxNode? init = null;
            SyntaxToken? initSemicolon = null;
            ExpressionSyntax? tag = null;

            // switch { } — tagless
            if (!At(SyntaxKind.OpenBraceToken))
            {
                bool savedCompositeLit = _allowCompositeLit;
                _allowCompositeLit = false;

                var expr = ParseExpression();

                if (At(SyntaxKind.ColonEqualsToken) || IsAssignmentOperator(Current.Kind) ||
                    At(SyntaxKind.PlusPlusToken) || At(SyntaxKind.MinusMinusToken))
                {
                    init = WrapSimpleStatement(expr);

                    _allowCompositeLit = savedCompositeLit;

                    // Detect type switch: v := x.(type) {
                    if (init is ShortVarDeclarationSyntax svd
                        && svd.Right.Count == 1
                        && svd.Right[0] is TypeAssertExpressionSyntax typeAssert
                        && typeAssert.TypeOrKeyword is SyntaxToken tk
                        && tk.Kind == SyntaxKind.TypeKeyword)
                    {
                        return ParseTypeSwitchBody(switchKeyword, null, null, init);
                    }

                    initSemicolon = Expect(SyntaxKind.SemicolonToken);

                    if (!At(SyntaxKind.OpenBraceToken))
                    {
                        tag = ParseExpressionNoCompositeLit();

                        // Detect type switch with init: switch init; v := x.(type) {
                        if (At(SyntaxKind.ColonEqualsToken))
                        {
                            _allowCompositeLit = false;
                            var guard = WrapSimpleStatement(tag);
                            _allowCompositeLit = savedCompositeLit;
                            tag = null;
                            if (guard is ShortVarDeclarationSyntax svd2
                                && svd2.Right.Count == 1
                                && svd2.Right[0] is TypeAssertExpressionSyntax ta2
                                && ta2.TypeOrKeyword is SyntaxToken tk3
                                && tk3.Kind == SyntaxKind.TypeKeyword)
                            {
                                return ParseTypeSwitchBody(switchKeyword, init, initSemicolon, guard);
                            }
                        }
                    }
                }
                else if (AtSemicolon())
                {
                    _allowCompositeLit = savedCompositeLit;

                    init = new ExpressionStatementSyntax(expr);
                    initSemicolon = Advance();

                    if (!At(SyntaxKind.OpenBraceToken))
                    {
                        tag = ParseExpressionNoCompositeLit();

                        // Detect type switch with init: switch init; v := x.(type) {
                        if (At(SyntaxKind.ColonEqualsToken))
                        {
                            _allowCompositeLit = false;
                            var guard = WrapSimpleStatement(tag);
                            _allowCompositeLit = savedCompositeLit;
                            tag = null;
                            if (guard is ShortVarDeclarationSyntax svd3
                                && svd3.Right.Count == 1
                                && svd3.Right[0] is TypeAssertExpressionSyntax ta3
                                && ta3.TypeOrKeyword is SyntaxToken tk4
                                && tk4.Kind == SyntaxKind.TypeKeyword)
                            {
                                return ParseTypeSwitchBody(switchKeyword, init, initSemicolon, guard);
                            }
                        }
                    }
                }
                else
                {
                    _allowCompositeLit = savedCompositeLit;
                    tag = expr;
                }
            }

            // Detect bare type switch: switch x.(type) { }
            if (tag is TypeAssertExpressionSyntax bareTypeAssert
                && bareTypeAssert.TypeOrKeyword is SyntaxToken tk2
                && tk2.Kind == SyntaxKind.TypeKeyword)
            {
                return ParseTypeSwitchBody(switchKeyword, init, initSemicolon, tag);
            }

            var open = Expect(SyntaxKind.OpenBraceToken);
            var cases = new List<ExprSwitchCaseSyntax>();

            while (!At(SyntaxKind.CloseBraceToken) && !At(SyntaxKind.EndOfFileToken))
            {
                cases.Add(ParseExprSwitchCase());
            }

            var close = Expect(SyntaxKind.CloseBraceToken);
            return new SwitchStatementSyntax(switchKeyword, init, initSemicolon, tag, open, cases, close);
        }

        private ExprSwitchCaseSyntax ParseExprSwitchCase()
        {
            SyntaxToken caseOrDefault;
            SeparatedSyntaxList<ExpressionSyntax>? expressions = null;

            if (At(SyntaxKind.CaseKeyword))
            {
                caseOrDefault = Advance();
                expressions = ParseExpressionList();
            }
            else
            {
                caseOrDefault = Expect(SyntaxKind.DefaultKeyword);
            }

            var colon = Expect(SyntaxKind.ColonToken);

            var statements = new List<SyntaxNode>();
            while (!At(SyntaxKind.CaseKeyword) && !At(SyntaxKind.DefaultKeyword) &&
                   !At(SyntaxKind.CloseBraceToken) && !At(SyntaxKind.EndOfFileToken))
            {
                statements.Add(ParseStatement());
                SkipSemicolon();
            }

            return new ExprSwitchCaseSyntax(caseOrDefault, expressions, colon, statements);
        }

        private TypeSwitchStatementSyntax ParseTypeSwitchBody(SyntaxToken switchKeyword,
            SyntaxNode? init, SyntaxToken? initSemicolon, SyntaxNode guard)
        {
            var open = Expect(SyntaxKind.OpenBraceToken);
            var cases = new List<TypeSwitchCaseSyntax>();

            while (!At(SyntaxKind.CloseBraceToken) && !At(SyntaxKind.EndOfFileToken))
            {
                cases.Add(ParseTypeSwitchCase());
            }

            var close = Expect(SyntaxKind.CloseBraceToken);
            return new TypeSwitchStatementSyntax(switchKeyword, init, initSemicolon, guard, open, cases, close);
        }

        private TypeSwitchCaseSyntax ParseTypeSwitchCase()
        {
            SyntaxToken caseOrDefault;
            SeparatedSyntaxList<ExpressionSyntax>? types = null;

            if (At(SyntaxKind.CaseKeyword))
            {
                caseOrDefault = Advance();
                types = ParseExpressionList();
            }
            else
            {
                caseOrDefault = Expect(SyntaxKind.DefaultKeyword);
            }

            var colon = Expect(SyntaxKind.ColonToken);

            var statements = new List<SyntaxNode>();
            while (!At(SyntaxKind.CaseKeyword) && !At(SyntaxKind.DefaultKeyword) &&
                   !At(SyntaxKind.CloseBraceToken) && !At(SyntaxKind.EndOfFileToken))
            {
                statements.Add(ParseStatement());
                SkipSemicolon();
            }

            return new TypeSwitchCaseSyntax(caseOrDefault, types, colon, statements);
        }

        // ----------------------------------------------------------------
        // Select
        // ----------------------------------------------------------------

        private SelectStatementSyntax ParseSelectStatement()
        {
            var selectKeyword = Expect(SyntaxKind.SelectKeyword);
            var open = Expect(SyntaxKind.OpenBraceToken);
            var clauses = new List<CommClauseSyntax>();

            while (!At(SyntaxKind.CloseBraceToken) && !At(SyntaxKind.EndOfFileToken))
            {
                clauses.Add(ParseCommClause());
            }

            var close = Expect(SyntaxKind.CloseBraceToken);
            return new SelectStatementSyntax(selectKeyword, open, clauses, close);
        }

        private CommClauseSyntax ParseCommClause()
        {
            SyntaxToken caseOrDefault;
            SyntaxNode? commStmt = null;

            if (At(SyntaxKind.CaseKeyword))
            {
                caseOrDefault = Advance();
                commStmt = ParseSimpleStatement();
            }
            else
            {
                caseOrDefault = Expect(SyntaxKind.DefaultKeyword);
            }

            var colon = Expect(SyntaxKind.ColonToken);

            var statements = new List<SyntaxNode>();
            while (!At(SyntaxKind.CaseKeyword) && !At(SyntaxKind.DefaultKeyword) &&
                   !At(SyntaxKind.CloseBraceToken) && !At(SyntaxKind.EndOfFileToken))
            {
                statements.Add(ParseStatement());
                SkipSemicolon();
            }

            return new CommClauseSyntax(caseOrDefault, commStmt, colon, statements);
        }

        // ----------------------------------------------------------------
        // Go, Defer, Branch
        // ----------------------------------------------------------------

        private GoStatementSyntax ParseGoStatement()
        {
            var keyword = Expect(SyntaxKind.GoKeyword);
            var expr = ParseExpression();
            return new GoStatementSyntax(keyword, expr);
        }

        private DeferStatementSyntax ParseDeferStatement()
        {
            var keyword = Expect(SyntaxKind.DeferKeyword);
            var expr = ParseExpression();
            return new DeferStatementSyntax(keyword, expr);
        }

        private BranchStatementSyntax ParseBranchStatement()
        {
            var keyword = Advance(); // break, continue, goto, fallthrough

            SyntaxToken? label = null;
            if (keyword.Kind != SyntaxKind.FallthroughKeyword && At(SyntaxKind.IdentifierToken))
                label = Advance();

            return new BranchStatementSyntax(keyword, label);
        }

        // ================================================================
        // Expressions
        // ================================================================

        public ExpressionSyntax ParseExpression()
        {
            return ParseBinaryExpression(1);
        }

        /// <summary>
        /// Parse an expression with composite literals disallowed.
        /// Used in if/for/switch conditions where { would be ambiguous with block start.
        /// </summary>
        private ExpressionSyntax ParseExpressionNoCompositeLit()
        {
            bool saved = _allowCompositeLit;
            _allowCompositeLit = false;
            try
            {
                return ParseExpression();
            }
            finally
            {
                _allowCompositeLit = saved;
            }
        }

        private ExpressionSyntax ParseBinaryExpression(int minPrecedence)
        {
            var left = ParseUnaryExpression();

            while (true)
            {
                int prec = GetBinaryPrecedence(Current.Kind);
                if (prec < minPrecedence)
                    break;

                var op = Advance();
                var right = ParseBinaryExpression(prec + 1);
                left = new BinaryExpressionSyntax(left, op, right);
            }

            return left;
        }

        private ExpressionSyntax ParseUnaryExpression()
        {
            switch (Current.Kind)
            {
                case SyntaxKind.PlusToken:
                case SyntaxKind.MinusToken:
                case SyntaxKind.ExclamationToken:
                case SyntaxKind.CaretToken:
                case SyntaxKind.AmpersandToken:
                case SyntaxKind.StarToken:
                {
                    var op = Advance();
                    var operand = ParseUnaryExpression();
                    return new UnaryExpressionSyntax(op, operand);
                }

                case SyntaxKind.LessThanMinusToken:
                {
                    // <-channel (receive operation)
                    var op = Advance();
                    var operand = ParseUnaryExpression();
                    return new UnaryExpressionSyntax(op, operand);
                }

                default:
                    return ParsePrimaryExpression();
            }
        }

        private ExpressionSyntax ParsePrimaryExpression()
        {
            var expr = ParseOperand();

            // Postfix operations: .field, [index], (args), .(type)
            while (true)
            {
                switch (Current.Kind)
                {
                    case SyntaxKind.DotToken:
                        expr = ParseSelectorOrTypeAssert(expr);
                        break;

                    case SyntaxKind.OpenBracketToken:
                        expr = ParseIndexOrSlice(expr);
                        break;

                    case SyntaxKind.OpenParenToken:
                        expr = ParseCallExpression(expr);
                        break;

                    case SyntaxKind.OpenBraceToken when _allowCompositeLit:
                        expr = ParseCompositeLiteral(expr);
                        break;

                    default:
                        return expr;
                }
            }
        }

        private ExpressionSyntax ParseOperand()
        {
            switch (Current.Kind)
            {
                case SyntaxKind.IdentifierToken:
                    return new IdentifierNameSyntax(Advance());

                case SyntaxKind.IntLiteralToken:
                case SyntaxKind.FloatLiteralToken:
                case SyntaxKind.ImaginaryLiteralToken:
                case SyntaxKind.RuneLiteralToken:
                case SyntaxKind.StringLiteralToken:
                case SyntaxKind.RawStringLiteralToken:
                    return new LiteralExpressionSyntax(Advance());

                case SyntaxKind.OpenParenToken:
                {
                    var open = Advance();
                    var expr = ParseExpression();
                    var close = Expect(SyntaxKind.CloseParenToken);
                    return new ParenthesizedExpressionSyntax(open, expr, close);
                }

                case SyntaxKind.FuncKeyword:
                    return ParseFunctionLiteral();

                // Type expressions that can start operands
                case SyntaxKind.OpenBracketToken:
                    return ParseArrayOrSliceType();

                case SyntaxKind.MapKeyword:
                    return ParseMapType();

                case SyntaxKind.StructKeyword:
                    return ParseStructType();

                case SyntaxKind.InterfaceKeyword:
                    return ParseInterfaceType();

                case SyntaxKind.ChanKeyword:
                    return ParseChannelType();

                default:
                    // Error recovery: consume the bad token
                    var errorToken = Advance();
                    return new LiteralExpressionSyntax(errorToken);
            }
        }

        private ExpressionSyntax ParseSelectorOrTypeAssert(ExpressionSyntax expr)
        {
            var dot = Advance(); // .

            // Type assertion: expr.(type) or expr.(Type)
            if (At(SyntaxKind.OpenParenToken))
            {
                var open = Advance();
                SyntaxNode typeOrKeyword;
                if (At(SyntaxKind.TypeKeyword))
                    typeOrKeyword = Advance();
                else
                    typeOrKeyword = ParseType();
                var close = Expect(SyntaxKind.CloseParenToken);
                return new TypeAssertExpressionSyntax(expr, dot, open, typeOrKeyword, close);
            }

            // Selector: expr.name
            var name = Expect(SyntaxKind.IdentifierToken);
            return new SelectorExpressionSyntax(expr, dot, name);
        }

        private ExpressionSyntax ParseIndexOrSlice(ExpressionSyntax expr)
        {
            var open = Advance(); // [

            // Check for slice: expr[low:high] or expr[low:high:max]
            ExpressionSyntax? first = null;
            if (!At(SyntaxKind.ColonToken))
                first = ParseExpression();

            if (At(SyntaxKind.ColonToken))
            {
                var colon1 = Advance();
                ExpressionSyntax? high = null;
                if (!At(SyntaxKind.ColonToken) && !At(SyntaxKind.CloseBracketToken))
                    high = ParseExpression();

                SyntaxToken? colon2 = null;
                ExpressionSyntax? max = null;
                if (At(SyntaxKind.ColonToken))
                {
                    colon2 = Advance();
                    max = ParseExpression();
                }

                var close = Expect(SyntaxKind.CloseBracketToken);
                return new SliceExpressionSyntax(expr, open, first, colon1, high, colon2, max, close);
            }

            // Simple index
            var closeBracket = Expect(SyntaxKind.CloseBracketToken);
            return new IndexExpressionSyntax(expr, open, first!, closeBracket);
        }

        private ExpressionSyntax ParseCallExpression(ExpressionSyntax func)
        {
            var open = Advance(); // (
            SeparatedSyntaxList<ExpressionSyntax> args;
            SyntaxToken? ellipsis = null;

            if (At(SyntaxKind.CloseParenToken))
            {
                args = SeparatedSyntaxList<ExpressionSyntax>.Empty;
            }
            else
            {
                args = ParseExpressionList();
                if (At(SyntaxKind.EllipsisToken))
                    ellipsis = Advance();
            }

            var close = Expect(SyntaxKind.CloseParenToken);
            return new CallExpressionSyntax(func, open, args, ellipsis, close);
        }

        private ExpressionSyntax ParseCompositeLiteral(ExpressionSyntax? type)
        {
            var open = Expect(SyntaxKind.OpenBraceToken);

            SeparatedSyntaxList<ExpressionSyntax> elements;
            if (At(SyntaxKind.CloseBraceToken))
            {
                elements = SeparatedSyntaxList<ExpressionSyntax>.Empty;
            }
            else
            {
                elements = ParseElementList();
            }

            var close = Expect(SyntaxKind.CloseBraceToken);
            return new CompositeLiteralSyntax(type, open, elements, close);
        }

        private SeparatedSyntaxList<ExpressionSyntax> ParseElementList()
        {
            var builder = new List<SyntaxNode>();
            builder.Add(ParseElement());

            while (At(SyntaxKind.CommaToken))
            {
                builder.Add(Advance()); // comma
                if (At(SyntaxKind.CloseBraceToken))
                    break; // trailing comma
                builder.Add(ParseElement());
            }

            return new SeparatedSyntaxList<ExpressionSyntax>(builder);
        }

        private ExpressionSyntax ParseElement()
        {
            // Bare composite literal inside another composite literal (e.g. []Point{{1,2}})
            if (At(SyntaxKind.OpenBraceToken))
            {
                return ParseCompositeLiteral(null);
            }

            var expr = ParseExpression();

            // Key-value pair
            if (At(SyntaxKind.ColonToken))
            {
                var colon = Advance();
                ExpressionSyntax value;
                if (At(SyntaxKind.OpenBraceToken))
                    value = ParseCompositeLiteral(null);
                else
                    value = ParseExpression();
                return new KeyValueExpressionSyntax(expr, colon, value);
            }

            return expr;
        }

        private ExpressionSyntax ParseFunctionLiteral()
        {
            var funcKeyword = Advance(); // func
            var parameters = ParseParameterList();
            var result = ParseResult();
            var body = ParseBlock();
            return new FunctionLiteralSyntax(funcKeyword, parameters, result, body);
        }

        // ================================================================
        // Expression list
        // ================================================================

        private SeparatedSyntaxList<ExpressionSyntax> ParseExpressionList()
        {
            var builder = new List<SyntaxNode>();
            builder.Add(ParseExpression());

            while (At(SyntaxKind.CommaToken))
            {
                builder.Add(Advance()); // comma
                builder.Add(ParseExpression());
            }

            return new SeparatedSyntaxList<ExpressionSyntax>(builder);
        }

        // ================================================================
        // Type parsing
        // ================================================================

        public ExpressionSyntax ParseType()
        {
            switch (Current.Kind)
            {
                case SyntaxKind.IdentifierToken:
                {
                    var ident = new IdentifierNameSyntax(Advance());
                    // Qualified name: pkg.Type
                    if (At(SyntaxKind.DotToken))
                    {
                        var dot = Advance();
                        var name = Expect(SyntaxKind.IdentifierToken);
                        return new SelectorExpressionSyntax(ident, dot, name);
                    }
                    return ident;
                }

                case SyntaxKind.StarToken:
                {
                    var star = Advance();
                    var elementType = ParseType();
                    return new PointerTypeSyntax(star, elementType);
                }

                case SyntaxKind.OpenBracketToken:
                    return ParseArrayOrSliceType();

                case SyntaxKind.MapKeyword:
                    return ParseMapType();

                case SyntaxKind.ChanKeyword:
                    return ParseChannelType();

                case SyntaxKind.LessThanMinusToken:
                    // <-chan Type (receive-only channel)
                    if (Peek(1).Kind == SyntaxKind.ChanKeyword)
                    {
                        var arrow = Advance();
                        var chanKeyword = Advance();
                        var elementType = ParseType();
                        return new ChannelTypeSyntax(arrow, chanKeyword, null, elementType);
                    }
                    goto default;

                case SyntaxKind.FuncKeyword:
                    return ParseFuncType();

                case SyntaxKind.StructKeyword:
                    return ParseStructType();

                case SyntaxKind.InterfaceKeyword:
                    return ParseInterfaceType();

                case SyntaxKind.OpenParenToken:
                {
                    var open = Advance();
                    var inner = ParseType();
                    var close = Expect(SyntaxKind.CloseParenToken);
                    return new ParenthesizedExpressionSyntax(open, inner, close);
                }

                default:
                    // Error: expected type
                    return new IdentifierNameSyntax(Expect(SyntaxKind.IdentifierToken));
            }
        }

        private ExpressionSyntax ParseArrayOrSliceType()
        {
            var openBracket = Advance(); // [

            if (At(SyntaxKind.CloseBracketToken))
            {
                // Slice type: []T
                var closeBracket = Advance();
                var elementType = ParseType();
                return new SliceTypeSyntax(openBracket, closeBracket, elementType);
            }

            if (At(SyntaxKind.EllipsisToken))
            {
                // Array with ...: [...]T (length determined by initializer)
                var ellipsis = Advance();
                var closeBracket = Expect(SyntaxKind.CloseBracketToken);
                var elementType = ParseType();
                return new ArrayTypeSyntax(openBracket, new LiteralExpressionSyntax(ellipsis), closeBracket, elementType);
            }

            // Array type: [n]T
            var length = ParseExpression();
            var close = Expect(SyntaxKind.CloseBracketToken);
            var elemType = ParseType();
            return new ArrayTypeSyntax(openBracket, length, close, elemType);
        }

        private MapTypeSyntax ParseMapType()
        {
            var mapKeyword = Advance(); // map
            var openBracket = Expect(SyntaxKind.OpenBracketToken);
            var keyType = ParseType();
            var closeBracket = Expect(SyntaxKind.CloseBracketToken);
            var valueType = ParseType();
            return new MapTypeSyntax(mapKeyword, openBracket, keyType, closeBracket, valueType);
        }

        private ChannelTypeSyntax ParseChannelType()
        {
            var chanKeyword = Advance(); // chan

            SyntaxToken? sendArrow = null;
            if (At(SyntaxKind.LessThanMinusToken))
                sendArrow = Advance(); // chan<-

            var elementType = ParseType();
            return new ChannelTypeSyntax(null, chanKeyword, sendArrow, elementType);
        }

        private FuncTypeSyntax ParseFuncType()
        {
            var funcKeyword = Advance(); // func
            var parameters = ParseParameterList();
            var result = ParseResult();
            return new FuncTypeSyntax(funcKeyword, parameters, result);
        }

        private StructTypeSyntax ParseStructType()
        {
            var structKeyword = Advance(); // struct
            var open = Expect(SyntaxKind.OpenBraceToken);
            var fields = new List<FieldDeclarationSyntax>();

            while (!At(SyntaxKind.CloseBraceToken) && !At(SyntaxKind.EndOfFileToken))
            {
                fields.Add(ParseFieldDeclaration());
                SkipSemicolon();
            }

            var close = Expect(SyntaxKind.CloseBraceToken);
            return new StructTypeSyntax(structKeyword, open, fields, close);
        }

        private FieldDeclarationSyntax ParseFieldDeclaration()
        {
            // Field: names type tag?  OR  *Type (embedded)  OR  Type (embedded)
            SeparatedSyntaxList<SyntaxToken>? names = null;

            if (At(SyntaxKind.IdentifierToken))
            {
                // Could be names or an embedded type
                if (Peek(1).Kind == SyntaxKind.IdentifierToken ||
                    Peek(1).Kind == SyntaxKind.StarToken ||
                    Peek(1).Kind == SyntaxKind.OpenBracketToken ||
                    Peek(1).Kind == SyntaxKind.MapKeyword ||
                    Peek(1).Kind == SyntaxKind.ChanKeyword ||
                    Peek(1).Kind == SyntaxKind.FuncKeyword ||
                    Peek(1).Kind == SyntaxKind.InterfaceKeyword ||
                    Peek(1).Kind == SyntaxKind.StructKeyword ||
                    Peek(1).Kind == SyntaxKind.CommaToken)
                {
                    names = ParseIdentifierTokenList();
                }
            }

            var type = ParseType();

            SyntaxToken? tag = null;
            if (At(SyntaxKind.StringLiteralToken) || At(SyntaxKind.RawStringLiteralToken))
                tag = Advance();

            return new FieldDeclarationSyntax(names, type, tag);
        }

        private InterfaceTypeSyntax ParseInterfaceType()
        {
            var interfaceKeyword = Advance(); // interface
            var open = Expect(SyntaxKind.OpenBraceToken);
            var members = new List<SyntaxNode>();

            while (!At(SyntaxKind.CloseBraceToken) && !At(SyntaxKind.EndOfFileToken))
            {
                if (At(SyntaxKind.IdentifierToken) && Peek(1).Kind == SyntaxKind.OpenParenToken)
                {
                    // Method spec: name(params) result
                    var name = Advance();
                    var parameters = ParseParameterList();
                    var result = ParseResult();
                    members.Add(new MethodSpecSyntax(name, parameters, result));
                }
                else
                {
                    // Embedded type
                    var embeddedType = ParseType();
                    members.Add(embeddedType);
                }

                SkipSemicolon();
            }

            var close = Expect(SyntaxKind.CloseBraceToken);
            return new InterfaceTypeSyntax(interfaceKeyword, open, members, close);
        }

        // ================================================================
        // Helpers
        // ================================================================

        private static int GetBinaryPrecedence(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.PipePipeToken:
                    return 1;
                case SyntaxKind.AmpersandAmpersandToken:
                    return 2;
                case SyntaxKind.EqualsEqualsToken:
                case SyntaxKind.ExclamationEqualsToken:
                case SyntaxKind.LessThanToken:
                case SyntaxKind.LessThanEqualsToken:
                case SyntaxKind.GreaterThanToken:
                case SyntaxKind.GreaterThanEqualsToken:
                    return 3;
                case SyntaxKind.PlusToken:
                case SyntaxKind.MinusToken:
                case SyntaxKind.PipeToken:
                case SyntaxKind.CaretToken:
                    return 4;
                case SyntaxKind.StarToken:
                case SyntaxKind.SlashToken:
                case SyntaxKind.PercentToken:
                case SyntaxKind.LessThanLessThanToken:
                case SyntaxKind.GreaterThanGreaterThanToken:
                case SyntaxKind.AmpersandToken:
                case SyntaxKind.AmpersandCaretToken:
                    return 5;
                default:
                    return 0;
            }
        }

        private static bool IsAssignmentOperator(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.EqualsToken:
                case SyntaxKind.PlusEqualsToken:
                case SyntaxKind.MinusEqualsToken:
                case SyntaxKind.StarEqualsToken:
                case SyntaxKind.SlashEqualsToken:
                case SyntaxKind.PercentEqualsToken:
                case SyntaxKind.AmpersandEqualsToken:
                case SyntaxKind.PipeEqualsToken:
                case SyntaxKind.CaretEqualsToken:
                case SyntaxKind.LessThanLessThanEqualsToken:
                case SyntaxKind.GreaterThanGreaterThanEqualsToken:
                case SyntaxKind.AmpersandCaretEqualsToken:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsTypeStart(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.IdentifierToken:
                case SyntaxKind.StarToken:
                case SyntaxKind.OpenBracketToken:
                case SyntaxKind.MapKeyword:
                case SyntaxKind.ChanKeyword:
                case SyntaxKind.LessThanMinusToken:
                case SyntaxKind.FuncKeyword:
                case SyntaxKind.StructKeyword:
                case SyntaxKind.InterfaceKeyword:
                case SyntaxKind.OpenParenToken:
                case SyntaxKind.EllipsisToken:
                    return true;
                default:
                    return false;
            }
        }

        private ErrorNodeSyntax ParseErrorNode(string message)
        {
            // Consume the current token as part of the error
            var children = new List<SyntaxNode>();
            children.Add(Advance());
            return new ErrorNodeSyntax(children);
        }
    }
}

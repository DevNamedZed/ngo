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
        private bool _restrictCompositeLitToTypes;

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
                var posBefore = _pos;
                members.Add(ParseTopLevelDeclaration());
                SkipSemicolon();
                if (_pos == posBefore)
                {
                    Advance();
                }
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
                    var posBefore = _pos;
                    specs.Add(ParseImportSpec());
                    SkipSemicolon();
                    if (_pos == posBefore)
                    {
                        Advance();
                    }
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

            // Function: func name[T any](params) result { body }
            {
                var name = Expect(SyntaxKind.IdentifierToken);
                TypeParameterListSyntax? typeParams = null;
                if (At(SyntaxKind.OpenBracketToken) && LooksLikeTypeParameterList())
                    typeParams = ParseTypeParameterList();
                var parameters = ParseParameterList();
                var result = ParseResult();
                var body = At(SyntaxKind.OpenBraceToken) ? ParseBlock() : null;
                return new FunctionDeclarationSyntax(funcKeyword, name, typeParams, parameters, result, body);
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
                if (At(SyntaxKind.CloseParenToken))
                    break; // trailing comma
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

                // If a type (or ellipsis) follows the identifier(s), they're parameter names.
                // But: ident[ident...] is a generic type instantiation (Type[T]),
                // not name followed by array type ([N]T). Only treat as named param
                // if [ is followed by int literal, ], or ... (real array/slice syntax).
                var afterIdents = Peek(offset).Kind;
                if (afterIdents == SyntaxKind.OpenBracketToken)
                {
                    var insideBracket = Peek(offset + 1).Kind;
                    if (insideBracket == SyntaxKind.IntLiteralToken
                        || insideBracket == SyntaxKind.CloseBracketToken
                        || insideBracket == SyntaxKind.EllipsisToken)
                    {
                        // Array/slice type: [N]T, []T, [...]T — treat as named param
                    }
                    else if (insideBracket == SyntaxKind.IdentifierToken
                        && Peek(offset + 2).Kind == SyntaxKind.CloseBracketToken
                        && IsTypeStart(Peek(offset + 3).Kind))
                    {
                        // [ident]Type — array with constant-length identifier (e.g., [Size]byte)
                    }
                    else if (insideBracket == SyntaxKind.IdentifierToken
                        && Peek(offset + 2).Kind == SyntaxKind.DotToken
                        && Peek(offset + 3).Kind == SyntaxKind.IdentifierToken
                        && Peek(offset + 4).Kind == SyntaxKind.CloseBracketToken)
                    {
                        // [pkg.Const]Type — array with package-qualified constant length
                    }
                    else
                    {
                        // Looks like generic instantiation — fall through to unnamed param
                        afterIdents = SyntaxKind.None;
                    }
                }
                if (IsTypeStart(afterIdents) || afterIdents == SyntaxKind.EllipsisToken)
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
                    var posBefore = _pos;
                    specs.Add(ParseTypeSpec());
                    SkipSemicolon();
                    if (_pos == posBefore)
                    {
                        Advance();
                    }
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

            TypeParameterListSyntax? typeParams = null;
            if (At(SyntaxKind.OpenBracketToken) && LooksLikeTypeParameterList())
                typeParams = ParseTypeParameterList();

            SyntaxToken? assign = null;
            if (At(SyntaxKind.EqualsToken))
                assign = Advance();

            var type = ParseType();
            return new TypeSpecSyntax(name, typeParams, assign, type);
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
                    var posBefore = _pos;
                    specs.Add(ParseVarSpec());
                    SkipSemicolon();
                    if (_pos == posBefore)
                    {
                        Advance();
                    }
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
                    var posBefore = _pos;
                    specs.Add(ParseConstSpec());
                    SkipSemicolon();
                    if (_pos == posBefore)
                    {
                        Advance();
                    }
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
                var posBefore = _pos;
                statements.Add(ParseStatement());
                SkipSemicolon();
                if (_pos == posBefore)
                {
                    Advance();
                }
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
                // Label at end of block (e.g., `l0:\n}`) — no following statement
                SyntaxNode stmt;
                if (At(SyntaxKind.CloseBraceToken) || At(SyntaxKind.EndOfFileToken))
                {
                    stmt = new EmptyStatementSyntax(new SyntaxToken(SyntaxKind.SemicolonToken, "", Current.Position));
                }
                else
                {
                    stmt = ParseStatement();
                }
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
            if (At(SyntaxKind.ColonEqualsToken) || At(SyntaxKind.EqualsToken) || IsAssignmentOperator(Current.Kind))
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

            // C-style for where init is i++ or i--: for i++; cond; post { }
            if (At(SyntaxKind.PlusPlusToken) || At(SyntaxKind.MinusMinusToken))
            {
                var incOp = Advance();
                var init = new IncDecStatementSyntax(firstExpr, incOp);
                var semi1 = Expect(SyntaxKind.SemicolonToken);
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
            // Allow composite literals after range when the expression starts with
            // a type constructor (slice/array/map type). Otherwise, '{' would be
            // ambiguous with the for-block opening brace.
            ExpressionSyntax expr;
            if (At(SyntaxKind.OpenBracketToken) || At(SyntaxKind.MapKeyword)
                || At(SyntaxKind.StructKeyword))
            {
                bool saved = _allowCompositeLit;
                _allowCompositeLit = true;
                _restrictCompositeLitToTypes = true;
                expr = ParseExpression();
                _restrictCompositeLitToTypes = false;
                _allowCompositeLit = saved;
            }
            else
            {
                expr = ParseExpressionNoCompositeLit();
            }
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

                if (At(SyntaxKind.CommaToken))
                {
                    // Multi-value init: switch a, b := ...; { }
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

                    _allowCompositeLit = savedCompositeLit;
                    initSemicolon = Expect(SyntaxKind.SemicolonToken);

                    if (!At(SyntaxKind.OpenBraceToken))
                    {
                        tag = ParseExpressionNoCompositeLit();
                    }
                }
                else if (At(SyntaxKind.ColonEqualsToken) || IsAssignmentOperator(Current.Kind) ||
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
                var posBefore = _pos;
                cases.Add(ParseExprSwitchCase());
                if (_pos == posBefore)
                {
                    Advance();
                }
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
                var posBefore = _pos;
                statements.Add(ParseStatement());
                SkipSemicolon();
                if (_pos == posBefore)
                {
                    Advance();
                }
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
                var posBefore = _pos;
                cases.Add(ParseTypeSwitchCase());
                if (_pos == posBefore)
                {
                    Advance();
                }
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
                var posBefore = _pos;
                statements.Add(ParseStatement());
                SkipSemicolon();
                if (_pos == posBefore)
                {
                    Advance();
                }
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
                var posBefore = _pos;
                clauses.Add(ParseCommClause());
                if (_pos == posBefore)
                {
                    Advance();
                }
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
                var posBefore = _pos;
                statements.Add(ParseStatement());
                SkipSemicolon();
                if (_pos == posBefore)
                {
                    Advance();
                }
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

                    case SyntaxKind.OpenBraceToken when _allowCompositeLit
                        && (!_restrictCompositeLitToTypes || IsCompositeLitType(expr)):
                        expr = ParseCompositeLiteral(expr);
                        break;

                    // In no-composite-lit contexts (if/for conditions), still allow
                    // composite literals for unambiguous type expressions like [N]T{},
                    // []T{}, map[K]V{} — these can never be confused with block braces.
                    case SyntaxKind.OpenBraceToken when !_allowCompositeLit
                        && (expr is ArrayTypeSyntax || expr is SliceTypeSyntax
                            || expr is MapTypeSyntax || expr is StructTypeSyntax):
                        expr = ParseCompositeLiteral(expr);
                        break;

                    default:
                        return expr;
                }
            }
        }

        private static bool IsCompositeLitType(ExpressionSyntax expr)
        {
            return expr is IdentifierNameSyntax
                || expr is SliceTypeSyntax
                || expr is ArrayTypeSyntax
                || expr is MapTypeSyntax
                || expr is StructTypeSyntax
                || expr is SelectorExpressionSyntax
                || expr is IndexExpressionSyntax
                || expr is TypeArgumentListSyntax;
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
                    // Check for (<-chan T) type conversion: (<-chan T)(expr)
                    if (At(SyntaxKind.LessThanMinusToken) && Peek(1).Kind == SyntaxKind.ChanKeyword)
                    {
                        var chanType = ParseType();
                        var chanClose = Expect(SyntaxKind.CloseParenToken);
                        return new ParenthesizedExpressionSyntax(open, chanType, chanClose);
                    }
                    // Composite literals are always allowed inside parentheses
                    bool savedCompLit = _allowCompositeLit;
                    _allowCompositeLit = true;
                    var expr = ParseExpression();
                    _allowCompositeLit = savedCompLit;
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
            // Check for generic type argument list: expr[Type, ...] followed by (
            // This handles generic function calls like appendString[string](...)
            if (LooksLikeGenericInstantiation())
            {
                return ParseTypeArgumentList(expr);
            }

            var open = Advance(); // [

            // Inside [...], composite literals are always allowed since [
            // already disambiguates from block braces.
            bool savedCompLit = _allowCompositeLit;
            _allowCompositeLit = true;
            try
            {
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

                // Multi type arguments: expr[T1, T2, ...]
                if (At(SyntaxKind.CommaToken) && first != null)
                {
                    var typeArgBuilder = new List<SyntaxNode> { first };
                    while (At(SyntaxKind.CommaToken))
                    {
                        typeArgBuilder.Add(Advance()); // comma
                        typeArgBuilder.Add(ParseExpression());
                    }
                    var close = Expect(SyntaxKind.CloseBracketToken);
                    var typeArgs = new SeparatedSyntaxList<ExpressionSyntax>(typeArgBuilder);
                    return new TypeArgumentListSyntax(expr, open, typeArgs, close);
                }

                // Simple index (or single type arg — disambiguated in semantics)
                var closeBracket = Expect(SyntaxKind.CloseBracketToken);
                return new IndexExpressionSyntax(expr, open, first!, closeBracket);
            }
            finally
            {
                _allowCompositeLit = savedCompLit;
            }
        }

        private ExpressionSyntax ParseCallExpression(ExpressionSyntax func)
        {
            var open = Advance(); // (
            // Inside function call parentheses, composite literals are always allowed
            bool savedCompLit = _allowCompositeLit;
            _allowCompositeLit = true;
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
                // Allow trailing comma after ... (valid in Go)
                if (At(SyntaxKind.CommaToken) && Peek(1).Kind == SyntaxKind.CloseParenToken)
                    Advance();
            }

            _allowCompositeLit = savedCompLit;
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
                var nested = ParseCompositeLiteral(null);
                // Composite literal as map key: {k1, k2}: {v1, v2}
                if (At(SyntaxKind.ColonToken))
                {
                    var colon = Advance();
                    ExpressionSyntax value;
                    if (At(SyntaxKind.OpenBraceToken))
                        value = ParseCompositeLiteral(null);
                    else
                        value = ParseExpression();
                    return new KeyValueExpressionSyntax(nested, colon, value);
                }
                return nested;
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
            // If no opening brace follows, this is a function TYPE in expression
            // context (e.g., (func(int) int)(nil) — type conversion), not a literal.
            if (!At(SyntaxKind.OpenBraceToken))
            {
                return new FuncTypeSyntax(funcKeyword, parameters, result);
            }
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
                // Trailing comma before closing token — stop
                var next = Peek(1).Kind;
                if (next == SyntaxKind.CloseParenToken
                    || next == SyntaxKind.CloseBraceToken
                    || next == SyntaxKind.CloseBracketToken)
                {
                    Advance(); // consume trailing comma
                    break;
                }

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
                        var selector = new SelectorExpressionSyntax(ident, dot, name);
                        // pkg.Type[T, U] — qualified generic instantiation
                        if (At(SyntaxKind.OpenBracketToken) && LooksLikeTypeArgumentList())
                            return ParseTypeArgumentList(selector);
                        return selector;
                    }
                    // Type[T] or Type[T, U] — generic instantiation
                    if (At(SyntaxKind.OpenBracketToken) && LooksLikeTypeArgumentList())
                        return ParseTypeArgumentList(ident);
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
                var posBefore = _pos;
                fields.Add(ParseFieldDeclaration());
                SkipSemicolon();
                // Guard: if we didn't advance, skip the current token to avoid infinite loop
                if (_pos == posBefore)
                    Advance();
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
                // When followed by [, distinguish array type (name [5]int) from
                // generic instantiation (node[N, T] as embedded field)
                bool looksLikeFieldName = false;
                var next = Peek(1).Kind;
                if (next == SyntaxKind.IdentifierToken ||
                    next == SyntaxKind.StarToken ||
                    next == SyntaxKind.MapKeyword ||
                    next == SyntaxKind.ChanKeyword ||
                    next == SyntaxKind.LessThanMinusToken ||
                    next == SyntaxKind.FuncKeyword ||
                    next == SyntaxKind.InterfaceKeyword ||
                    next == SyntaxKind.StructKeyword ||
                    next == SyntaxKind.OpenParenToken ||
                    next == SyntaxKind.CommaToken)
                {
                    looksLikeFieldName = true;
                }
                else if (next == SyntaxKind.OpenBracketToken)
                {
                    // Distinguish: name [expr]type (field + array) vs name[T, U] (embedded generic)
                    // Array: [5]int, []int, [...]int, [maxEntries]rect
                    // Generic: node[N, T] (comma inside brackets)
                    var insideBracket = Peek(2).Kind;
                    if (insideBracket == SyntaxKind.IntLiteralToken ||
                        insideBracket == SyntaxKind.CloseBracketToken ||
                        insideBracket == SyntaxKind.EllipsisToken)
                    {
                        looksLikeFieldName = true;
                    }
                    else if (insideBracket == SyntaxKind.IdentifierToken)
                    {
                        var afterInner = Peek(3).Kind;
                        if (afterInner == SyntaxKind.CommaToken)
                        {
                            // node[N, T] — embedded generic, not a field name
                            looksLikeFieldName = false;
                        }
                        else if (afterInner == SyntaxKind.CloseBracketToken)
                        {
                            // ident[ident] — ambiguous: [const]type vs Type[T]
                            // If a type-start follows ], it's name [const]Type (field name)
                            // If semicolon/}/EOF follows ], it's Type[T] (embedded generic)
                            var afterClose = Peek(4).Kind;
                            looksLikeFieldName = IsTypeStart(afterClose)
                                || afterClose == SyntaxKind.OpenBracketToken;
                        }
                        else
                        {
                            // [maxEntries]rect — treat as array (field name)
                            looksLikeFieldName = true;
                        }
                    }
                    else if (insideBracket == SyntaxKind.StarToken)
                    {
                        // [*Type] — generic type arg with pointer, not array
                        // Array sizes are always integer literals or identifiers, not *
                        looksLikeFieldName = false;
                    }
                    else
                    {
                        looksLikeFieldName = true;
                    }
                }

                if (looksLikeFieldName)
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
                var posBefore = _pos;
                if (At(SyntaxKind.TildeToken))
                {
                    // Union type element starting with ~
                    members.Add(ParseUnionConstraint());
                }
                else if (At(SyntaxKind.IdentifierToken) && Peek(1).Kind == SyntaxKind.OpenParenToken)
                {
                    // Method spec: name(params) result
                    var name = Advance();
                    var parameters = ParseParameterList();
                    var result = ParseResult();
                    members.Add(new MethodSpecSyntax(name, parameters, result));
                }
                else
                {
                    // Embedded type or union type element
                    var embeddedType = ParseType();
                    if (At(SyntaxKind.PipeToken))
                    {
                        // Union type: int | float64 | ...
                        members.Add(ParseUnionConstraintContinuation(embeddedType));
                    }
                    else
                    {
                        members.Add(embeddedType);
                    }
                }

                SkipSemicolon();
                if (_pos == posBefore)
                {
                    Advance();
                }
            }

            var close = Expect(SyntaxKind.CloseBraceToken);
            return new InterfaceTypeSyntax(interfaceKeyword, open, members, close);
        }

        // ================================================================
        // Generics: Type parameter lists and type argument lists
        // ================================================================

        /// <summary>
        /// Disambiguates [N]T (array type) from [T any] (type parameter list).
        /// Called when current token is [. Uses lookahead heuristics.
        /// </summary>
        private bool LooksLikeTypeParameterList()
        {
            // Must be at [
            if (!At(SyntaxKind.OpenBracketToken)) return false;

            var next = Peek(1);

            // [123...] or [...]  → array, not type params
            if (next.Kind == SyntaxKind.IntLiteralToken || next.Kind == SyntaxKind.EllipsisToken)
                return false;

            // [] → slice type, not type params
            if (next.Kind == SyntaxKind.CloseBracketToken)
                return false;

            // [ident ...] — look further
            if (next.Kind == SyntaxKind.IdentifierToken)
            {
                var afterIdent = Peek(2);
                // [T any] or [T comparable] or [T interface{...}] — two idents = type params
                if (afterIdent.Kind == SyntaxKind.IdentifierToken)
                    return true;
                // [T, U ...] — comma after ident = type params
                if (afterIdent.Kind == SyntaxKind.CommaToken)
                    return true;
                // [T interface...] — type params
                if (afterIdent.Kind == SyntaxKind.InterfaceKeyword)
                    return true;
                // [T ~int] — tilde = type params
                if (afterIdent.Kind == SyntaxKind.TildeToken)
                    return true;
                // [Bytes []byte | string] — constraint starts with slice/array type
                if (afterIdent.Kind == SyntaxKind.OpenBracketToken
                    || afterIdent.Kind == SyntaxKind.StarToken
                    || afterIdent.Kind == SyntaxKind.MapKeyword
                    || afterIdent.Kind == SyntaxKind.ChanKeyword
                    || afterIdent.Kind == SyntaxKind.FuncKeyword
                    || afterIdent.Kind == SyntaxKind.StructKeyword)
                    return true;
                // [T] — single ident followed by close bracket. This is ambiguous.
                // In Go 1.18+, type params always require a constraint: [T any], [T comparable].
                // [ident] without a constraint is an array type with constant-length ident.
                // So [ident] → NOT a type param list.
                if (afterIdent.Kind == SyntaxKind.CloseBracketToken)
                    return false;
            }

            // [~...] — tilde can only appear in type param constraints
            if (next.Kind == SyntaxKind.TildeToken)
                return true;

            return false;
        }

        /// <summary>
        /// In expression context, checks if expr[ starts a generic instantiation
        /// by scanning ahead to find ] followed by ( — the pattern expr[Types](args).
        /// Only triggers for type-like content inside brackets (not simple expressions).
        /// </summary>
        private bool LooksLikeGenericInstantiation()
        {
            if (!At(SyntaxKind.OpenBracketToken)) return false;
            var next = Peek(1);
            // [int], [string] or other builtin type names inside [] followed by ]( → generic
            // [[]byte] or [*T] → definitely type args, but only if followed by (
            // [ident] → ambiguous (could be index), only treat as generic if ](

            // Must start with something that looks like a type but NOT like a simple expression
            // Type-only tokens: [], *, struct, func, map, chan, interface, <-
            bool looksLikeType = false;
            if (next.Kind == SyntaxKind.OpenBracketToken  // [[]byte]
                || next.Kind == SyntaxKind.StarToken       // [*Type]
                || next.Kind == SyntaxKind.StructKeyword
                || next.Kind == SyntaxKind.FuncKeyword
                || next.Kind == SyntaxKind.MapKeyword
                || next.Kind == SyntaxKind.ChanKeyword
                || next.Kind == SyntaxKind.InterfaceKeyword
                || next.Kind == SyntaxKind.LessThanMinusToken)
            {
                looksLikeType = true;
            }

            // [ident ...] where ... contains type-only syntax like |, [], etc.
            if (!looksLikeType && next.Kind == SyntaxKind.IdentifierToken)
            {
                // Scan ahead to see if there's type union syntax (|) or other type indicators
                int depth = 1;
                int off = 2;
                bool hasTypeUnion = false;
                while (depth > 0 && off < 100)
                {
                    var tk = Peek(off).Kind;
                    if (tk == SyntaxKind.OpenBracketToken) depth++;
                    else if (tk == SyntaxKind.CloseBracketToken)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            // Check if ] is followed by (
                            if (Peek(off + 1).Kind == SyntaxKind.OpenParenToken && hasTypeUnion)
                                return true;
                            break;
                        }
                    }
                    else if (tk == SyntaxKind.PipeToken) hasTypeUnion = true;
                    else if (tk == SyntaxKind.EndOfFileToken) break;
                    off++;
                }
            }

            if (!looksLikeType) return false;

            // Scan ahead to find matching ] and check if followed by (
            int bracketDepth = 1;
            int offset = 2;
            while (bracketDepth > 0 && offset < 100)
            {
                var tk = Peek(offset).Kind;
                if (tk == SyntaxKind.OpenBracketToken) bracketDepth++;
                else if (tk == SyntaxKind.CloseBracketToken) bracketDepth--;
                else if (tk == SyntaxKind.EndOfFileToken) return false;
                if (bracketDepth == 0)
                {
                    // Check if ] is followed by (
                    return Peek(offset + 1).Kind == SyntaxKind.OpenParenToken;
                }
                offset++;
            }
            return false;
        }

        private bool LooksLikeTypeArgumentList()
        {
            // In a type context (not expression), [T] or [T, U] after an identifier is type args.
            // Array types use integer literals: [N]T, [...]T.
            if (!At(SyntaxKind.OpenBracketToken)) return false;
            var next = Peek(1);
            // [123] or [...] → array, not type args
            if (next.Kind == SyntaxKind.IntLiteralToken || next.Kind == SyntaxKind.EllipsisToken)
                return false;
            // [] → slice
            if (next.Kind == SyntaxKind.CloseBracketToken)
                return false;
            // [ident...] → type arg
            if (next.Kind == SyntaxKind.IdentifierToken)
                return true;
            // [*ident] → *Type as type arg
            if (next.Kind == SyntaxKind.StarToken)
                return true;
            // [struct{...}], [func(...)], [map[...]...], [chan ...], [interface{...}], [[]T]
            if (next.Kind == SyntaxKind.StructKeyword || next.Kind == SyntaxKind.FuncKeyword
                || next.Kind == SyntaxKind.MapKeyword || next.Kind == SyntaxKind.ChanKeyword
                || next.Kind == SyntaxKind.InterfaceKeyword || next.Kind == SyntaxKind.OpenBracketToken
                || next.Kind == SyntaxKind.LessThanMinusToken)
                return true;
            return false;
        }

        private ExpressionSyntax ParseTypeArgumentList(ExpressionSyntax baseExpr)
        {
            var open = Expect(SyntaxKind.OpenBracketToken);
            var builder = new List<SyntaxNode>();
            builder.Add(ParseType());
            while (At(SyntaxKind.CommaToken))
            {
                builder.Add(Advance()); // comma
                builder.Add(ParseType());
            }
            var close = Expect(SyntaxKind.CloseBracketToken);

            if (builder.Count == 1)
            {
                // Single type arg: return as IndexExpressionSyntax
                return new IndexExpressionSyntax(baseExpr, open, (ExpressionSyntax)builder[0], close);
            }

            var args = new SeparatedSyntaxList<ExpressionSyntax>(builder);
            return new TypeArgumentListSyntax(baseExpr, open, args, close);
        }

        private TypeParameterListSyntax ParseTypeParameterList()
        {
            var open = Expect(SyntaxKind.OpenBracketToken);
            var builder = new List<SyntaxNode>();

            builder.Add(ParseTypeParameterDecl());

            while (At(SyntaxKind.CommaToken))
            {
                builder.Add(Advance()); // comma
                if (At(SyntaxKind.CloseBracketToken))
                    break; // trailing comma
                builder.Add(ParseTypeParameterDecl());
            }

            var close = Expect(SyntaxKind.CloseBracketToken);
            var parameters = new SeparatedSyntaxList<TypeParameterDeclSyntax>(builder);
            return new TypeParameterListSyntax(open, parameters, close);
        }

        private TypeParameterDeclSyntax ParseTypeParameterDecl()
        {
            // Parse name(s): T  or  T, U
            var nameBuilder = new List<SyntaxNode>();
            nameBuilder.Add(Expect(SyntaxKind.IdentifierToken));

            // Check if next is comma followed by identifier, indicating grouped names
            // sharing a constraint (e.g., [T1, T2, R any] or [K comparable, V any])
            while (At(SyntaxKind.CommaToken) && Peek(1).Kind == SyntaxKind.IdentifierToken)
            {
                // Look ahead: if after the next ident we see a constraint token (ident, ~, interface, |),
                // OR if we see a comma (meaning more names follow before the constraint),
                // then these are grouped names sharing a constraint.
                var afterNextIdent = Peek(2);
                if (afterNextIdent.Kind == SyntaxKind.IdentifierToken
                    || afterNextIdent.Kind == SyntaxKind.TildeToken
                    || afterNextIdent.Kind == SyntaxKind.InterfaceKeyword
                    || afterNextIdent.Kind == SyntaxKind.PipeToken
                    || afterNextIdent.Kind == SyntaxKind.CommaToken)
                {
                    nameBuilder.Add(Advance()); // comma
                    nameBuilder.Add(Advance()); // identifier
                }
                else
                {
                    break;
                }
            }

            var names = new SeparatedSyntaxList<SyntaxToken>(nameBuilder);

            // Parse constraint: any, comparable, interface{...}, ~int | ~float64, etc.
            ExpressionSyntax constraint;
            if (At(SyntaxKind.TildeToken))
            {
                constraint = ParseUnionConstraint();
            }
            else
            {
                constraint = ParseType();
                // Check for union: type | type
                if (At(SyntaxKind.PipeToken))
                {
                    constraint = ParseUnionConstraintContinuation(constraint);
                }
            }

            return new TypeParameterDeclSyntax(names, constraint);
        }

        private UnionTypeSyntax ParseUnionConstraint()
        {
            var terms = new List<UnionTermSyntax>();

            do
            {
                SyntaxToken? tilde = null;
                if (At(SyntaxKind.TildeToken))
                    tilde = Advance();

                var type = ParseType();

                SyntaxToken? pipe = null;
                if (At(SyntaxKind.PipeToken))
                    pipe = Advance();

                terms.Add(new UnionTermSyntax(tilde, type, pipe));
            }
            while (terms[terms.Count - 1].Pipe != null);

            return new UnionTypeSyntax(terms);
        }

        private UnionTypeSyntax ParseUnionConstraintContinuation(ExpressionSyntax firstType)
        {
            var terms = new List<UnionTermSyntax>();

            // First term (already parsed type, now we see |)
            var pipe = Advance(); // |
            terms.Add(new UnionTermSyntax(null, firstType, pipe));

            // Remaining terms
            do
            {
                SyntaxToken? tilde = null;
                if (At(SyntaxKind.TildeToken))
                    tilde = Advance();

                var type = ParseType();

                SyntaxToken? nextPipe = null;
                if (At(SyntaxKind.PipeToken))
                    nextPipe = Advance();

                terms.Add(new UnionTermSyntax(tilde, type, nextPipe));
            }
            while (terms[terms.Count - 1].Pipe != null);

            return new UnionTypeSyntax(terms);
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

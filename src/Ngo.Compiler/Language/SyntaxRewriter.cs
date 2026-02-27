// Copyright 2016 Ziad
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Language
{
    public class SyntaxRewriter : SyntaxVisitor<SyntaxNode>
    {
        protected override SyntaxNode? DefaultVisit(SyntaxNode node) => node;

        protected override SyntaxNode? VisitToken(SyntaxToken token) => token;

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private IReadOnlyList<T> VisitList<T>(IReadOnlyList<T> list) where T : SyntaxNode
        {
            List<T>? newList = null;
            for (int i = 0; i < list.Count; i++)
            {
                var visited = (T)Visit(list[i])!;
                if (visited != list[i] && newList == null)
                {
                    newList = new List<T>(list.Count);
                    for (int j = 0; j < i; j++)
                        newList.Add(list[j]);
                }
                newList?.Add(visited);
            }
            return newList ?? list;
        }

        private SeparatedSyntaxList<T> VisitSeparatedList<T>(SeparatedSyntaxList<T> list) where T : SyntaxNode
        {
            List<SyntaxNode>? newNodes = null;
            var withSeps = list.GetWithSeparators().ToList();
            for (int i = 0; i < withSeps.Count; i++)
            {
                var original = withSeps[i];
                // Only visit non-separator nodes (separators are tokens at odd indices)
                var visited = (i % 2 == 0) ? Visit(original)! : original;
                if (visited != original && newNodes == null)
                {
                    newNodes = new List<SyntaxNode>(withSeps.Count);
                    for (int j = 0; j < i; j++)
                        newNodes.Add(withSeps[j]);
                }
                newNodes?.Add(visited);
            }
            if (newNodes == null) return list;
            return new SeparatedSyntaxList<T>(newNodes);
        }

        // ---------------------------------------------------------------
        // Declarations
        // ---------------------------------------------------------------

        protected override SyntaxNode? VisitSourceFile(SourceFileSyntax node)
        {
            var packageClause = (PackageClauseSyntax)Visit(node.PackageClause)!;
            var imports = VisitList(node.Imports);
            var members = VisitList(node.Members);
            if (packageClause == node.PackageClause && imports == node.Imports && members == node.Members)
                return node;
            return new SourceFileSyntax(packageClause, imports, members, node.EndOfFile);
        }

        protected override SyntaxNode? VisitPackageClause(PackageClauseSyntax node)
        {
            return node;
        }

        protected override SyntaxNode? VisitImportDeclaration(ImportDeclarationSyntax node)
        {
            var specs = VisitList(node.Specs);
            if (specs == node.Specs)
                return node;
            return new ImportDeclarationSyntax(node.ImportKeyword, node.OpenParen, specs, node.CloseParen);
        }

        protected override SyntaxNode? VisitImportSpec(ImportSpecSyntax node)
        {
            return node;
        }

        protected override SyntaxNode? VisitFunctionDeclaration(FunctionDeclarationSyntax node)
        {
            var parameters = (ParameterListSyntax)Visit(node.Parameters)!;
            var result = node.Result != null ? Visit(node.Result) : null;
            var body = node.Body != null ? (BlockSyntax)Visit(node.Body)! : null;
            if (parameters == node.Parameters && result == node.Result && body == node.Body)
                return node;
            return new FunctionDeclarationSyntax(node.FuncKeyword, node.Name, parameters, result, body);
        }

        protected override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var receiver = (ParameterListSyntax)Visit(node.Receiver)!;
            var parameters = (ParameterListSyntax)Visit(node.Parameters)!;
            var result = node.Result != null ? Visit(node.Result) : null;
            var body = node.Body != null ? (BlockSyntax)Visit(node.Body)! : null;
            if (receiver == node.Receiver && parameters == node.Parameters
                && result == node.Result && body == node.Body)
                return node;
            return new MethodDeclarationSyntax(node.FuncKeyword, receiver, node.Name, parameters, result, body);
        }

        protected override SyntaxNode? VisitParameterList(ParameterListSyntax node)
        {
            var parameters = VisitSeparatedList(node.Parameters);
            if (parameters.GetWithSeparators() == node.Parameters.GetWithSeparators())
                return node;
            return new ParameterListSyntax(node.OpenParen, parameters, node.CloseParen);
        }

        protected override SyntaxNode? VisitParameter(ParameterSyntax node)
        {
            // Names is SeparatedSyntaxList<SyntaxToken> — tokens, skip visiting
            var type = node.Type != null ? (ExpressionSyntax)Visit(node.Type)! : null;
            if (type == node.Type)
                return node;
            return new ParameterSyntax(node.Names, node.Ellipsis, type);
        }

        protected override SyntaxNode? VisitTypeDeclaration(TypeDeclarationSyntax node)
        {
            var specs = VisitList(node.Specs);
            if (specs == node.Specs)
                return node;
            return new TypeDeclarationSyntax(node.TypeKeyword, node.OpenParen, specs, node.CloseParen);
        }

        protected override SyntaxNode? VisitTypeSpec(TypeSpecSyntax node)
        {
            var type = (ExpressionSyntax)Visit(node.Type)!;
            if (type == node.Type)
                return node;
            return new TypeSpecSyntax(node.Name, node.AssignToken, type);
        }

        protected override SyntaxNode? VisitVarDeclaration(VarDeclarationSyntax node)
        {
            var specs = VisitList(node.Specs);
            if (specs == node.Specs)
                return node;
            return new VarDeclarationSyntax(node.VarKeyword, node.OpenParen, specs, node.CloseParen);
        }

        protected override SyntaxNode? VisitVarSpec(VarSpecSyntax node)
        {
            // Names is SeparatedSyntaxList<SyntaxToken> — tokens, skip visiting
            var type = node.Type != null ? (ExpressionSyntax)Visit(node.Type)! : null;
            SeparatedSyntaxList<ExpressionSyntax>? values = null;
            bool valuesChanged = false;
            if (node.Values.HasValue)
            {
                var visited = VisitSeparatedList(node.Values.Value);
                valuesChanged = visited.GetWithSeparators() != node.Values.Value.GetWithSeparators();
                values = visited;
            }
            if (type == node.Type && !valuesChanged)
                return node;
            return new VarSpecSyntax(node.Names, type, node.EqualsToken, values ?? node.Values);
        }

        protected override SyntaxNode? VisitConstDeclaration(ConstDeclarationSyntax node)
        {
            var specs = VisitList(node.Specs);
            if (specs == node.Specs)
                return node;
            return new ConstDeclarationSyntax(node.ConstKeyword, node.OpenParen, specs, node.CloseParen);
        }

        protected override SyntaxNode? VisitConstSpec(ConstSpecSyntax node)
        {
            // Names is SeparatedSyntaxList<SyntaxToken> — tokens, skip visiting
            var type = node.Type != null ? (ExpressionSyntax)Visit(node.Type)! : null;
            SeparatedSyntaxList<ExpressionSyntax>? values = null;
            bool valuesChanged = false;
            if (node.Values.HasValue)
            {
                var visited = VisitSeparatedList(node.Values.Value);
                valuesChanged = visited.GetWithSeparators() != node.Values.Value.GetWithSeparators();
                values = visited;
            }
            if (type == node.Type && !valuesChanged)
                return node;
            return new ConstSpecSyntax(node.Names, type, node.EqualsToken, values ?? node.Values);
        }

        protected override SyntaxNode? VisitErrorNode(ErrorNodeSyntax node)
        {
            var children = VisitList(node.Children);
            if (children == node.Children)
                return node;
            return new ErrorNodeSyntax(children);
        }

        // ---------------------------------------------------------------
        // Statements
        // ---------------------------------------------------------------

        protected override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            var statements = VisitList(node.Statements);
            if (statements == node.Statements)
                return node;
            return new BlockSyntax(node.OpenBrace, statements, node.CloseBrace);
        }

        protected override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var expression = (ExpressionSyntax)Visit(node.Expression)!;
            if (expression == node.Expression)
                return node;
            return new ExpressionStatementSyntax(expression);
        }

        protected override SyntaxNode? VisitEmptyStatement(EmptyStatementSyntax node)
        {
            return node;
        }

        protected override SyntaxNode? VisitAssignmentStatement(AssignmentStatementSyntax node)
        {
            var left = VisitSeparatedList(node.Left);
            var right = VisitSeparatedList(node.Right);
            if (left.GetWithSeparators() == node.Left.GetWithSeparators()
                && right.GetWithSeparators() == node.Right.GetWithSeparators())
                return node;
            return new AssignmentStatementSyntax(left, node.OperatorToken, right);
        }

        protected override SyntaxNode? VisitShortVarDeclaration(ShortVarDeclarationSyntax node)
        {
            var left = VisitSeparatedList(node.Left);
            var right = VisitSeparatedList(node.Right);
            if (left.GetWithSeparators() == node.Left.GetWithSeparators()
                && right.GetWithSeparators() == node.Right.GetWithSeparators())
                return node;
            return new ShortVarDeclarationSyntax(left, node.ColonEquals, right);
        }

        protected override SyntaxNode? VisitIncDecStatement(IncDecStatementSyntax node)
        {
            var operand = (ExpressionSyntax)Visit(node.Operand)!;
            if (operand == node.Operand)
                return node;
            return new IncDecStatementSyntax(operand, node.OperatorToken);
        }

        protected override SyntaxNode? VisitSendStatement(SendStatementSyntax node)
        {
            var channel = (ExpressionSyntax)Visit(node.Channel)!;
            var value = (ExpressionSyntax)Visit(node.Value)!;
            if (channel == node.Channel && value == node.Value)
                return node;
            return new SendStatementSyntax(channel, node.Arrow, value);
        }

        protected override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
        {
            var values = VisitSeparatedList(node.Values);
            if (values.GetWithSeparators() == node.Values.GetWithSeparators())
                return node;
            return new ReturnStatementSyntax(node.ReturnKeyword, values);
        }

        protected override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
        {
            var init = node.Init != null ? Visit(node.Init) : null;
            var condition = (ExpressionSyntax)Visit(node.Condition)!;
            var body = (BlockSyntax)Visit(node.Body)!;
            var elseBody = node.ElseBody != null ? Visit(node.ElseBody) : null;
            if (init == node.Init && condition == node.Condition
                && body == node.Body && elseBody == node.ElseBody)
                return node;
            return new IfStatementSyntax(node.IfKeyword, init, node.InitSemicolon,
                condition, body, node.ElseKeyword, elseBody);
        }

        protected override SyntaxNode? VisitForStatement(ForStatementSyntax node)
        {
            var init = node.Init != null ? Visit(node.Init) : null;
            var condition = node.Condition != null ? (ExpressionSyntax)Visit(node.Condition)! : null;
            var post = node.Post != null ? Visit(node.Post) : null;
            var rangeClause = node.RangeClause != null ? (RangeClauseSyntax)Visit(node.RangeClause)! : null;
            var body = (BlockSyntax)Visit(node.Body)!;
            if (init == node.Init && condition == node.Condition && post == node.Post
                && rangeClause == node.RangeClause && body == node.Body)
                return node;
            return new ForStatementSyntax(node.ForKeyword, init, node.Semicolon1,
                condition, node.Semicolon2, post, rangeClause, body);
        }

        protected override SyntaxNode? VisitRangeClause(RangeClauseSyntax node)
        {
            SeparatedSyntaxList<ExpressionSyntax>? variables = null;
            bool variablesChanged = false;
            if (node.Variables.HasValue)
            {
                var visited = VisitSeparatedList(node.Variables.Value);
                variablesChanged = visited.GetWithSeparators() != node.Variables.Value.GetWithSeparators();
                variables = visited;
            }
            var expression = (ExpressionSyntax)Visit(node.Expression)!;
            if (!variablesChanged && expression == node.Expression)
                return node;
            return new RangeClauseSyntax(variables ?? node.Variables, node.AssignOrDeclare,
                node.RangeKeyword, expression);
        }

        protected override SyntaxNode? VisitSwitchStatement(SwitchStatementSyntax node)
        {
            var init = node.Init != null ? Visit(node.Init) : null;
            var tag = node.Tag != null ? (ExpressionSyntax)Visit(node.Tag)! : null;
            var cases = VisitList(node.Cases);
            if (init == node.Init && tag == node.Tag && cases == node.Cases)
                return node;
            return new SwitchStatementSyntax(node.SwitchKeyword, init, node.InitSemicolon,
                tag, node.OpenBrace, cases, node.CloseBrace);
        }

        protected override SyntaxNode? VisitExprSwitchCase(ExprSwitchCaseSyntax node)
        {
            SeparatedSyntaxList<ExpressionSyntax>? expressions = null;
            bool expressionsChanged = false;
            if (node.Expressions.HasValue)
            {
                var visited = VisitSeparatedList(node.Expressions.Value);
                expressionsChanged = visited.GetWithSeparators() != node.Expressions.Value.GetWithSeparators();
                expressions = visited;
            }
            var statements = VisitList(node.Statements);
            if (!expressionsChanged && statements == node.Statements)
                return node;
            return new ExprSwitchCaseSyntax(node.CaseOrDefault, expressions ?? node.Expressions,
                node.Colon, statements);
        }

        protected override SyntaxNode? VisitTypeSwitchStatement(TypeSwitchStatementSyntax node)
        {
            var init = node.Init != null ? Visit(node.Init) : null;
            var guard = Visit(node.Guard)!;
            var cases = VisitList(node.Cases);
            if (init == node.Init && guard == node.Guard && cases == node.Cases)
                return node;
            return new TypeSwitchStatementSyntax(node.SwitchKeyword, init, node.InitSemicolon,
                guard, node.OpenBrace, cases, node.CloseBrace);
        }

        protected override SyntaxNode? VisitTypeSwitchCase(TypeSwitchCaseSyntax node)
        {
            SeparatedSyntaxList<ExpressionSyntax>? types = null;
            bool typesChanged = false;
            if (node.Types.HasValue)
            {
                var visited = VisitSeparatedList(node.Types.Value);
                typesChanged = visited.GetWithSeparators() != node.Types.Value.GetWithSeparators();
                types = visited;
            }
            var statements = VisitList(node.Statements);
            if (!typesChanged && statements == node.Statements)
                return node;
            return new TypeSwitchCaseSyntax(node.CaseOrDefault, types ?? node.Types,
                node.Colon, statements);
        }

        protected override SyntaxNode? VisitSelectStatement(SelectStatementSyntax node)
        {
            var clauses = VisitList(node.Clauses);
            if (clauses == node.Clauses)
                return node;
            return new SelectStatementSyntax(node.SelectKeyword, node.OpenBrace, clauses, node.CloseBrace);
        }

        protected override SyntaxNode? VisitCommClause(CommClauseSyntax node)
        {
            var commStatement = node.CommStatement != null ? Visit(node.CommStatement) : null;
            var statements = VisitList(node.Statements);
            if (commStatement == node.CommStatement && statements == node.Statements)
                return node;
            return new CommClauseSyntax(node.CaseOrDefault, commStatement, node.Colon, statements);
        }

        protected override SyntaxNode? VisitGoStatement(GoStatementSyntax node)
        {
            var expression = (ExpressionSyntax)Visit(node.Expression)!;
            if (expression == node.Expression)
                return node;
            return new GoStatementSyntax(node.GoKeyword, expression);
        }

        protected override SyntaxNode? VisitDeferStatement(DeferStatementSyntax node)
        {
            var expression = (ExpressionSyntax)Visit(node.Expression)!;
            if (expression == node.Expression)
                return node;
            return new DeferStatementSyntax(node.DeferKeyword, expression);
        }

        protected override SyntaxNode? VisitBranchStatement(BranchStatementSyntax node)
        {
            return node;
        }

        protected override SyntaxNode? VisitLabeledStatement(LabeledStatementSyntax node)
        {
            var statement = Visit(node.Statement)!;
            if (statement == node.Statement)
                return node;
            return new LabeledStatementSyntax(node.Label, node.Colon, statement);
        }

        // ---------------------------------------------------------------
        // Expressions
        // ---------------------------------------------------------------

        protected override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            return node;
        }

        protected override SyntaxNode? VisitLiteralExpression(LiteralExpressionSyntax node)
        {
            return node;
        }

        protected override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            var expression = (ExpressionSyntax)Visit(node.Expression)!;
            if (expression == node.Expression)
                return node;
            return new ParenthesizedExpressionSyntax(node.OpenParen, expression, node.CloseParen);
        }

        protected override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            var left = (ExpressionSyntax)Visit(node.Left)!;
            var right = (ExpressionSyntax)Visit(node.Right)!;
            if (left == node.Left && right == node.Right)
                return node;
            return new BinaryExpressionSyntax(left, node.OperatorToken, right);
        }

        protected override SyntaxNode? VisitUnaryExpression(UnaryExpressionSyntax node)
        {
            var operand = (ExpressionSyntax)Visit(node.Operand)!;
            if (operand == node.Operand)
                return node;
            return new UnaryExpressionSyntax(node.OperatorToken, operand);
        }

        protected override SyntaxNode? VisitCallExpression(CallExpressionSyntax node)
        {
            var function = (ExpressionSyntax)Visit(node.Function)!;
            var arguments = VisitSeparatedList(node.Arguments);
            if (function == node.Function
                && arguments.GetWithSeparators() == node.Arguments.GetWithSeparators())
                return node;
            return new CallExpressionSyntax(function, node.OpenParen, arguments, node.Ellipsis, node.CloseParen);
        }

        protected override SyntaxNode? VisitIndexExpression(IndexExpressionSyntax node)
        {
            var expression = (ExpressionSyntax)Visit(node.Expression)!;
            var index = (ExpressionSyntax)Visit(node.Index)!;
            if (expression == node.Expression && index == node.Index)
                return node;
            return new IndexExpressionSyntax(expression, node.OpenBracket, index, node.CloseBracket);
        }

        protected override SyntaxNode? VisitSliceExpression(SliceExpressionSyntax node)
        {
            var expression = (ExpressionSyntax)Visit(node.Expression)!;
            var low = node.Low != null ? (ExpressionSyntax)Visit(node.Low)! : null;
            var high = node.High != null ? (ExpressionSyntax)Visit(node.High)! : null;
            var max = node.Max != null ? (ExpressionSyntax)Visit(node.Max)! : null;
            if (expression == node.Expression && low == node.Low
                && high == node.High && max == node.Max)
                return node;
            return new SliceExpressionSyntax(expression, node.OpenBracket, low,
                node.Colon1, high, node.Colon2, max, node.CloseBracket);
        }

        protected override SyntaxNode? VisitSelectorExpression(SelectorExpressionSyntax node)
        {
            var expression = (ExpressionSyntax)Visit(node.Expression)!;
            if (expression == node.Expression)
                return node;
            return new SelectorExpressionSyntax(expression, node.Dot, node.Name);
        }

        protected override SyntaxNode? VisitTypeAssertExpression(TypeAssertExpressionSyntax node)
        {
            var expression = (ExpressionSyntax)Visit(node.Expression)!;
            var typeOrKeyword = Visit(node.TypeOrKeyword)!;
            if (expression == node.Expression && typeOrKeyword == node.TypeOrKeyword)
                return node;
            return new TypeAssertExpressionSyntax(expression, node.Dot, node.OpenParen,
                typeOrKeyword, node.CloseParen);
        }

        protected override SyntaxNode? VisitCompositeLiteral(CompositeLiteralSyntax node)
        {
            var type = node.Type != null ? (ExpressionSyntax)Visit(node.Type)! : null;
            var elements = VisitSeparatedList(node.Elements);
            if (type == node.Type
                && elements.GetWithSeparators() == node.Elements.GetWithSeparators())
                return node;
            return new CompositeLiteralSyntax(type, node.OpenBrace, elements, node.CloseBrace);
        }

        protected override SyntaxNode? VisitKeyValueExpression(KeyValueExpressionSyntax node)
        {
            var key = (ExpressionSyntax)Visit(node.Key)!;
            var value = (ExpressionSyntax)Visit(node.Value)!;
            if (key == node.Key && value == node.Value)
                return node;
            return new KeyValueExpressionSyntax(key, node.Colon, value);
        }

        protected override SyntaxNode? VisitFunctionLiteral(FunctionLiteralSyntax node)
        {
            var parameters = (ParameterListSyntax)Visit(node.Parameters)!;
            var result = node.Result != null ? Visit(node.Result) : null;
            var body = (BlockSyntax)Visit(node.Body)!;
            if (parameters == node.Parameters && result == node.Result && body == node.Body)
                return node;
            return new FunctionLiteralSyntax(node.FuncKeyword, parameters, result, body);
        }


        // ---------------------------------------------------------------
        // Types
        // ---------------------------------------------------------------

        protected override SyntaxNode? VisitPointerType(PointerTypeSyntax node)
        {
            var elementType = (ExpressionSyntax)Visit(node.ElementType)!;
            if (elementType == node.ElementType)
                return node;
            return new PointerTypeSyntax(node.Star, elementType);
        }

        protected override SyntaxNode? VisitArrayType(ArrayTypeSyntax node)
        {
            var length = (ExpressionSyntax)Visit(node.Length)!;
            var elementType = (ExpressionSyntax)Visit(node.ElementType)!;
            if (length == node.Length && elementType == node.ElementType)
                return node;
            return new ArrayTypeSyntax(node.OpenBracket, length, node.CloseBracket, elementType);
        }

        protected override SyntaxNode? VisitSliceType(SliceTypeSyntax node)
        {
            var elementType = (ExpressionSyntax)Visit(node.ElementType)!;
            if (elementType == node.ElementType)
                return node;
            return new SliceTypeSyntax(node.OpenBracket, node.CloseBracket, elementType);
        }

        protected override SyntaxNode? VisitMapType(MapTypeSyntax node)
        {
            var keyType = (ExpressionSyntax)Visit(node.KeyType)!;
            var valueType = (ExpressionSyntax)Visit(node.ValueType)!;
            if (keyType == node.KeyType && valueType == node.ValueType)
                return node;
            return new MapTypeSyntax(node.MapKeyword, node.OpenBracket, keyType,
                node.CloseBracket, valueType);
        }

        protected override SyntaxNode? VisitChannelType(ChannelTypeSyntax node)
        {
            var elementType = (ExpressionSyntax)Visit(node.ElementType)!;
            if (elementType == node.ElementType)
                return node;
            return new ChannelTypeSyntax(node.ReceiveArrow, node.ChanKeyword,
                node.SendArrow, elementType);
        }

        protected override SyntaxNode? VisitStructType(StructTypeSyntax node)
        {
            var fields = VisitList(node.Fields);
            if (fields == node.Fields)
                return node;
            return new StructTypeSyntax(node.StructKeyword, node.OpenBrace, fields, node.CloseBrace);
        }

        protected override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            // Names is SeparatedSyntaxList<SyntaxToken> — tokens, skip visiting
            var type = (ExpressionSyntax)Visit(node.Type)!;
            if (type == node.Type)
                return node;
            return new FieldDeclarationSyntax(node.Names, type, node.Tag);
        }

        protected override SyntaxNode? VisitInterfaceType(InterfaceTypeSyntax node)
        {
            var members = VisitList(node.Members);
            if (members == node.Members)
                return node;
            return new InterfaceTypeSyntax(node.InterfaceKeyword, node.OpenBrace, members, node.CloseBrace);
        }

        protected override SyntaxNode? VisitMethodSpec(MethodSpecSyntax node)
        {
            var parameters = (ParameterListSyntax)Visit(node.Parameters)!;
            var result = node.Result != null ? Visit(node.Result) : null;
            if (parameters == node.Parameters && result == node.Result)
                return node;
            return new MethodSpecSyntax(node.Name, parameters, result);
        }

        protected override SyntaxNode? VisitFuncType(FuncTypeSyntax node)
        {
            var parameters = (ParameterListSyntax)Visit(node.Parameters)!;
            var result = node.Result != null ? Visit(node.Result) : null;
            if (parameters == node.Parameters && result == node.Result)
                return node;
            return new FuncTypeSyntax(node.FuncKeyword, parameters, result);
        }
    }
}

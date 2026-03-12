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
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Language
{
    public abstract class SyntaxVisitor
    {
        public void Visit(SyntaxNode? node)
        {
            if (node == null) return;
            if (node is SyntaxToken token) { VisitToken(token); return; }
            switch (node.Kind)
            {
                // Declarations
                case SyntaxKind.SourceFile: VisitSourceFile((SourceFileSyntax)node); break;
                case SyntaxKind.PackageClause: VisitPackageClause((PackageClauseSyntax)node); break;
                case SyntaxKind.ImportDeclaration: VisitImportDeclaration((ImportDeclarationSyntax)node); break;
                case SyntaxKind.ImportSpec: VisitImportSpec((ImportSpecSyntax)node); break;
                case SyntaxKind.FunctionDeclaration: VisitFunctionDeclaration((FunctionDeclarationSyntax)node); break;
                case SyntaxKind.MethodDeclaration: VisitMethodDeclaration((MethodDeclarationSyntax)node); break;
                case SyntaxKind.ParameterList: VisitParameterList((ParameterListSyntax)node); break;
                case SyntaxKind.Parameter: VisitParameter((ParameterSyntax)node); break;
                case SyntaxKind.TypeDeclaration: VisitTypeDeclaration((TypeDeclarationSyntax)node); break;
                case SyntaxKind.TypeSpec: VisitTypeSpec((TypeSpecSyntax)node); break;
                case SyntaxKind.VarDeclaration: VisitVarDeclaration((VarDeclarationSyntax)node); break;
                case SyntaxKind.VarSpec: VisitVarSpec((VarSpecSyntax)node); break;
                case SyntaxKind.ConstDeclaration: VisitConstDeclaration((ConstDeclarationSyntax)node); break;
                case SyntaxKind.ConstSpec: VisitConstSpec((ConstSpecSyntax)node); break;
                case SyntaxKind.ErrorNode: VisitErrorNode((ErrorNodeSyntax)node); break;
                case SyntaxKind.TypeParameterList: VisitTypeParameterList((TypeParameterListSyntax)node); break;
                case SyntaxKind.TypeParameterDecl: VisitTypeParameterDecl((TypeParameterDeclSyntax)node); break;

                // Statements
                case SyntaxKind.Block: VisitBlock((BlockSyntax)node); break;
                case SyntaxKind.ExpressionStatement: VisitExpressionStatement((ExpressionStatementSyntax)node); break;
                case SyntaxKind.EmptyStatement: VisitEmptyStatement((EmptyStatementSyntax)node); break;
                case SyntaxKind.AssignmentStatement: VisitAssignmentStatement((AssignmentStatementSyntax)node); break;
                case SyntaxKind.ShortVarDeclaration: VisitShortVarDeclaration((ShortVarDeclarationSyntax)node); break;
                case SyntaxKind.IncDecStatement: VisitIncDecStatement((IncDecStatementSyntax)node); break;
                case SyntaxKind.SendStatement: VisitSendStatement((SendStatementSyntax)node); break;
                case SyntaxKind.ReturnStatement: VisitReturnStatement((ReturnStatementSyntax)node); break;
                case SyntaxKind.IfStatement: VisitIfStatement((IfStatementSyntax)node); break;
                case SyntaxKind.ForStatement: VisitForStatement((ForStatementSyntax)node); break;
                case SyntaxKind.RangeClause: VisitRangeClause((RangeClauseSyntax)node); break;
                case SyntaxKind.SwitchStatement: VisitSwitchStatement((SwitchStatementSyntax)node); break;
                case SyntaxKind.ExprSwitchCase: VisitExprSwitchCase((ExprSwitchCaseSyntax)node); break;
                case SyntaxKind.TypeSwitchStatement: VisitTypeSwitchStatement((TypeSwitchStatementSyntax)node); break;
                case SyntaxKind.TypeSwitchCase: VisitTypeSwitchCase((TypeSwitchCaseSyntax)node); break;
                case SyntaxKind.SelectStatement: VisitSelectStatement((SelectStatementSyntax)node); break;
                case SyntaxKind.CommClause: VisitCommClause((CommClauseSyntax)node); break;
                case SyntaxKind.GoStatement: VisitGoStatement((GoStatementSyntax)node); break;
                case SyntaxKind.DeferStatement: VisitDeferStatement((DeferStatementSyntax)node); break;
                case SyntaxKind.BranchStatement: VisitBranchStatement((BranchStatementSyntax)node); break;
                case SyntaxKind.LabeledStatement: VisitLabeledStatement((LabeledStatementSyntax)node); break;

                // Expressions
                case SyntaxKind.IdentifierName: VisitIdentifierName((IdentifierNameSyntax)node); break;
                case SyntaxKind.LiteralExpression: VisitLiteralExpression((LiteralExpressionSyntax)node); break;
                case SyntaxKind.ParenthesizedExpression: VisitParenthesizedExpression((ParenthesizedExpressionSyntax)node); break;
                case SyntaxKind.BinaryExpression: VisitBinaryExpression((BinaryExpressionSyntax)node); break;
                case SyntaxKind.UnaryExpression: VisitUnaryExpression((UnaryExpressionSyntax)node); break;
                case SyntaxKind.CallExpression: VisitCallExpression((CallExpressionSyntax)node); break;
                case SyntaxKind.IndexExpression: VisitIndexExpression((IndexExpressionSyntax)node); break;
                case SyntaxKind.SliceExpression: VisitSliceExpression((SliceExpressionSyntax)node); break;
                case SyntaxKind.SelectorExpression: VisitSelectorExpression((SelectorExpressionSyntax)node); break;
                case SyntaxKind.TypeAssertExpression: VisitTypeAssertExpression((TypeAssertExpressionSyntax)node); break;
                case SyntaxKind.CompositeLiteral: VisitCompositeLiteral((CompositeLiteralSyntax)node); break;
                case SyntaxKind.KeyValuePair: VisitKeyValueExpression((KeyValueExpressionSyntax)node); break;
                case SyntaxKind.FunctionLiteral: VisitFunctionLiteral((FunctionLiteralSyntax)node); break;
                case SyntaxKind.TypeArgumentList: VisitTypeArgumentList((TypeArgumentListSyntax)node); break;

                // Types
                case SyntaxKind.PointerType: VisitPointerType((PointerTypeSyntax)node); break;
                case SyntaxKind.ArrayType: VisitArrayType((ArrayTypeSyntax)node); break;
                case SyntaxKind.SliceType: VisitSliceType((SliceTypeSyntax)node); break;
                case SyntaxKind.MapType: VisitMapType((MapTypeSyntax)node); break;
                case SyntaxKind.ChannelType: VisitChannelType((ChannelTypeSyntax)node); break;
                case SyntaxKind.StructType: VisitStructType((StructTypeSyntax)node); break;
                case SyntaxKind.FieldDeclaration: VisitFieldDeclaration((FieldDeclarationSyntax)node); break;
                case SyntaxKind.InterfaceType: VisitInterfaceType((InterfaceTypeSyntax)node); break;
                case SyntaxKind.MethodSpec: VisitMethodSpec((MethodSpecSyntax)node); break;
                case SyntaxKind.FunctionType: VisitFuncType((FuncTypeSyntax)node); break;
                case SyntaxKind.UnionType: VisitUnionType((UnionTypeSyntax)node); break;
                case SyntaxKind.UnionTerm: VisitUnionTerm((UnionTermSyntax)node); break;

                default: DefaultVisit(node); break;
            }
        }

        protected virtual void DefaultVisit(SyntaxNode node)
        {
            foreach (var child in node.ChildNodes())
                Visit(child);
        }

        protected virtual void VisitToken(SyntaxToken token) { }

        // Declarations
        protected virtual void VisitSourceFile(SourceFileSyntax node) => DefaultVisit(node);
        protected virtual void VisitPackageClause(PackageClauseSyntax node) => DefaultVisit(node);
        protected virtual void VisitImportDeclaration(ImportDeclarationSyntax node) => DefaultVisit(node);
        protected virtual void VisitImportSpec(ImportSpecSyntax node) => DefaultVisit(node);
        protected virtual void VisitFunctionDeclaration(FunctionDeclarationSyntax node) => DefaultVisit(node);
        protected virtual void VisitMethodDeclaration(MethodDeclarationSyntax node) => DefaultVisit(node);
        protected virtual void VisitParameterList(ParameterListSyntax node) => DefaultVisit(node);
        protected virtual void VisitParameter(ParameterSyntax node) => DefaultVisit(node);
        protected virtual void VisitTypeDeclaration(TypeDeclarationSyntax node) => DefaultVisit(node);
        protected virtual void VisitTypeSpec(TypeSpecSyntax node) => DefaultVisit(node);
        protected virtual void VisitVarDeclaration(VarDeclarationSyntax node) => DefaultVisit(node);
        protected virtual void VisitVarSpec(VarSpecSyntax node) => DefaultVisit(node);
        protected virtual void VisitConstDeclaration(ConstDeclarationSyntax node) => DefaultVisit(node);
        protected virtual void VisitConstSpec(ConstSpecSyntax node) => DefaultVisit(node);
        protected virtual void VisitErrorNode(ErrorNodeSyntax node) => DefaultVisit(node);
        protected virtual void VisitTypeParameterList(TypeParameterListSyntax node) => DefaultVisit(node);
        protected virtual void VisitTypeParameterDecl(TypeParameterDeclSyntax node) => DefaultVisit(node);

        // Statements
        protected virtual void VisitBlock(BlockSyntax node) => DefaultVisit(node);
        protected virtual void VisitExpressionStatement(ExpressionStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitEmptyStatement(EmptyStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitAssignmentStatement(AssignmentStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitShortVarDeclaration(ShortVarDeclarationSyntax node) => DefaultVisit(node);
        protected virtual void VisitIncDecStatement(IncDecStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitSendStatement(SendStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitReturnStatement(ReturnStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitIfStatement(IfStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitForStatement(ForStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitRangeClause(RangeClauseSyntax node) => DefaultVisit(node);
        protected virtual void VisitSwitchStatement(SwitchStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitExprSwitchCase(ExprSwitchCaseSyntax node) => DefaultVisit(node);
        protected virtual void VisitTypeSwitchStatement(TypeSwitchStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitTypeSwitchCase(TypeSwitchCaseSyntax node) => DefaultVisit(node);
        protected virtual void VisitSelectStatement(SelectStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitCommClause(CommClauseSyntax node) => DefaultVisit(node);
        protected virtual void VisitGoStatement(GoStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitDeferStatement(DeferStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitBranchStatement(BranchStatementSyntax node) => DefaultVisit(node);
        protected virtual void VisitLabeledStatement(LabeledStatementSyntax node) => DefaultVisit(node);

        // Expressions
        protected virtual void VisitIdentifierName(IdentifierNameSyntax node) => DefaultVisit(node);
        protected virtual void VisitLiteralExpression(LiteralExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitParenthesizedExpression(ParenthesizedExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitBinaryExpression(BinaryExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitUnaryExpression(UnaryExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitCallExpression(CallExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitIndexExpression(IndexExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitSliceExpression(SliceExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitSelectorExpression(SelectorExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitTypeAssertExpression(TypeAssertExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitCompositeLiteral(CompositeLiteralSyntax node) => DefaultVisit(node);
        protected virtual void VisitKeyValueExpression(KeyValueExpressionSyntax node) => DefaultVisit(node);
        protected virtual void VisitFunctionLiteral(FunctionLiteralSyntax node) => DefaultVisit(node);
        protected virtual void VisitTypeArgumentList(TypeArgumentListSyntax node) => DefaultVisit(node);

        // Types
        protected virtual void VisitPointerType(PointerTypeSyntax node) => DefaultVisit(node);
        protected virtual void VisitArrayType(ArrayTypeSyntax node) => DefaultVisit(node);
        protected virtual void VisitSliceType(SliceTypeSyntax node) => DefaultVisit(node);
        protected virtual void VisitMapType(MapTypeSyntax node) => DefaultVisit(node);
        protected virtual void VisitChannelType(ChannelTypeSyntax node) => DefaultVisit(node);
        protected virtual void VisitStructType(StructTypeSyntax node) => DefaultVisit(node);
        protected virtual void VisitFieldDeclaration(FieldDeclarationSyntax node) => DefaultVisit(node);
        protected virtual void VisitInterfaceType(InterfaceTypeSyntax node) => DefaultVisit(node);
        protected virtual void VisitMethodSpec(MethodSpecSyntax node) => DefaultVisit(node);
        protected virtual void VisitFuncType(FuncTypeSyntax node) => DefaultVisit(node);
        protected virtual void VisitUnionType(UnionTypeSyntax node) => DefaultVisit(node);
        protected virtual void VisitUnionTerm(UnionTermSyntax node) => DefaultVisit(node);
    }
}

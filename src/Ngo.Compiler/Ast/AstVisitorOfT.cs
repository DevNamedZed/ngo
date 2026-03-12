// -----------------------------------------------------------------------
// <copyright file="AstVisitorOfT.cs" company="Ziad">
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

namespace Ngo.Compiler.Ast
{
    public abstract class AstVisitor<TResult>
    {
        public TResult Visit(AstNode node)
        {
            return node.NodeType switch
            {
                NodeType.LiteralExpression => VisitLiteralExpression((LiteralExpression)node),
                NodeType.BinaryExpression => VisitBinaryExpression((BinaryExpression)node),
                NodeType.UnaryExpression => VisitUnaryExpression((UnaryExpression)node),
                NodeType.IdentifierExpression => VisitIdentifierExpression((IdentifierExpression)node),
                NodeType.CallExpression => VisitCallExpression((CallExpression)node),
                NodeType.ConversionExpression => VisitConversionExpression((ConversionExpression)node),
                NodeType.ErrorExpression => VisitErrorExpression((ErrorExpression)node),
                NodeType.BlockStatement => VisitBlockStatement((BlockStatement)node),
                NodeType.ReturnStatement => VisitReturnStatement((ReturnStatement)node),
                NodeType.ExpressionStatement => VisitExpressionStatement((ExpressionStatement)node),
                NodeType.AssignmentStatement => VisitAssignmentStatement((AssignmentStatement)node),
                NodeType.IncDecStatement => VisitIncDecStatement((IncDecStatement)node),
                NodeType.IfStatement => VisitIfStatement((IfStatement)node),
                NodeType.ForStatement => VisitForStatement((ForStatement)node),
                NodeType.SwitchStatement => VisitSwitchStatement((SwitchStatement)node),
                NodeType.SwitchCase => VisitSwitchCase((SwitchCase)node),
                NodeType.BranchStatement => VisitBranchStatement((BranchStatement)node),
                NodeType.SourceFile => VisitSourceFile((SourceFile)node),
                NodeType.PackageDeclaration => VisitPackageDeclaration((PackageDeclaration)node),
                NodeType.ImportDeclaration => VisitImportDeclaration((ImportDeclaration)node),
                NodeType.FunctionDeclaration => VisitFunctionDeclaration((FunctionDeclaration)node),
                NodeType.VarDeclaration => VisitVarDeclaration((VarDeclaration)node),
                NodeType.TypeDeclaration => VisitTypeDeclaration((TypeDeclaration)node),
                NodeType.SelectorExpression => VisitSelectorExpression((SelectorExpression)node),
                NodeType.CompositeLiteralExpression => VisitCompositeLiteralExpression((CompositeLiteralExpression)node),
                NodeType.AddressOfExpression => VisitAddressOfExpression((AddressOfExpression)node),
                NodeType.DerefExpression => VisitDerefExpression((DerefExpression)node),
                NodeType.IndexExpression => VisitIndexExpression((IndexExpression)node),
                NodeType.SliceExpression => VisitSliceExpression((SliceExpression)node),
                NodeType.ForRangeStatement => VisitForRangeStatement((ForRangeStatement)node),
                NodeType.MethodDeclaration => VisitMethodDeclaration((MethodDeclaration)node),
                NodeType.MethodCallExpression => VisitMethodCallExpression((MethodCallExpression)node),
                NodeType.TypeAssertExpression => VisitTypeAssertExpression((TypeAssertExpression)node),
                NodeType.TypeSwitchStatement => VisitTypeSwitchStatement((TypeSwitchStatement)node),
                NodeType.TypeSwitchCase => VisitTypeSwitchCase((TypeSwitchCase)node),
                NodeType.ConstDeclaration => VisitConstDeclaration((ConstDeclaration)node),
                NodeType.FunctionLiteralExpression => VisitFunctionLiteralExpression((FunctionLiteralExpression)node),
                NodeType.SelectStatement => VisitSelectStatement((SelectStatement)node),
                NodeType.SelectCase => VisitSelectCase((SelectCase)node),
                _ => throw new ArgumentException($"Unexpected node type: {node.NodeType}"),
            };
        }

        protected virtual TResult VisitLiteralExpression(LiteralExpression node) => default!;
        protected virtual TResult VisitBinaryExpression(BinaryExpression node) => default!;
        protected virtual TResult VisitUnaryExpression(UnaryExpression node) => default!;
        protected virtual TResult VisitIdentifierExpression(IdentifierExpression node) => default!;
        protected virtual TResult VisitCallExpression(CallExpression node) => default!;
        protected virtual TResult VisitConversionExpression(ConversionExpression node) => default!;
        protected virtual TResult VisitErrorExpression(ErrorExpression node) => default!;
        protected virtual TResult VisitBlockStatement(BlockStatement node) => default!;
        protected virtual TResult VisitReturnStatement(ReturnStatement node) => default!;
        protected virtual TResult VisitExpressionStatement(ExpressionStatement node) => default!;
        protected virtual TResult VisitAssignmentStatement(AssignmentStatement node) => default!;
        protected virtual TResult VisitIncDecStatement(IncDecStatement node) => default!;
        protected virtual TResult VisitIfStatement(IfStatement node) => default!;
        protected virtual TResult VisitForStatement(ForStatement node) => default!;
        protected virtual TResult VisitSwitchStatement(SwitchStatement node) => default!;
        protected virtual TResult VisitSwitchCase(SwitchCase node) => default!;
        protected virtual TResult VisitBranchStatement(BranchStatement node) => default!;
        protected virtual TResult VisitSourceFile(SourceFile node) => default!;
        protected virtual TResult VisitPackageDeclaration(PackageDeclaration node) => default!;
        protected virtual TResult VisitImportDeclaration(ImportDeclaration node) => default!;
        protected virtual TResult VisitFunctionDeclaration(FunctionDeclaration node) => default!;
        protected virtual TResult VisitVarDeclaration(VarDeclaration node) => default!;
        protected virtual TResult VisitTypeDeclaration(TypeDeclaration node) => default!;
        protected virtual TResult VisitSelectorExpression(SelectorExpression node) => default!;
        protected virtual TResult VisitCompositeLiteralExpression(CompositeLiteralExpression node) => default!;
        protected virtual TResult VisitAddressOfExpression(AddressOfExpression node) => default!;
        protected virtual TResult VisitDerefExpression(DerefExpression node) => default!;
        protected virtual TResult VisitIndexExpression(IndexExpression node) => default!;
        protected virtual TResult VisitSliceExpression(SliceExpression node) => default!;
        protected virtual TResult VisitForRangeStatement(ForRangeStatement node) => default!;
        protected virtual TResult VisitMethodDeclaration(MethodDeclaration node) => default!;
        protected virtual TResult VisitMethodCallExpression(MethodCallExpression node) => default!;
        protected virtual TResult VisitTypeAssertExpression(TypeAssertExpression node) => default!;
        protected virtual TResult VisitTypeSwitchStatement(TypeSwitchStatement node) => default!;
        protected virtual TResult VisitTypeSwitchCase(TypeSwitchCase node) => default!;
        protected virtual TResult VisitConstDeclaration(ConstDeclaration node) => default!;
        protected virtual TResult VisitFunctionLiteralExpression(FunctionLiteralExpression node) => default!;
        protected virtual TResult VisitSelectStatement(SelectStatement node) => default!;
        protected virtual TResult VisitSelectCase(SelectCase node) => default!;
    }
}

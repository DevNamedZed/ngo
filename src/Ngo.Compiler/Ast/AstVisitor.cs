// -----------------------------------------------------------------------
// <copyright file="AstVisitor.cs" company="Ziad">
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
    public abstract class AstVisitor
    {
        public void Visit(AstNode node)
        {
            switch (node.NodeType)
            {
                case NodeType.LiteralExpression:
                    VisitLiteralExpression((LiteralExpression)node);
                    break;
                case NodeType.BinaryExpression:
                    VisitBinaryExpression((BinaryExpression)node);
                    break;
                case NodeType.UnaryExpression:
                    VisitUnaryExpression((UnaryExpression)node);
                    break;
                case NodeType.IdentifierExpression:
                    VisitIdentifierExpression((IdentifierExpression)node);
                    break;
                case NodeType.CallExpression:
                    VisitCallExpression((CallExpression)node);
                    break;
                case NodeType.ConversionExpression:
                    VisitConversionExpression((ConversionExpression)node);
                    break;
                case NodeType.ErrorExpression:
                    VisitErrorExpression((ErrorExpression)node);
                    break;
                case NodeType.BlockStatement:
                    VisitBlockStatement((BlockStatement)node);
                    break;
                case NodeType.ReturnStatement:
                    VisitReturnStatement((ReturnStatement)node);
                    break;
                case NodeType.ExpressionStatement:
                    VisitExpressionStatement((ExpressionStatement)node);
                    break;
                case NodeType.AssignmentStatement:
                    VisitAssignmentStatement((AssignmentStatement)node);
                    break;
                case NodeType.IncDecStatement:
                    VisitIncDecStatement((IncDecStatement)node);
                    break;
                case NodeType.IfStatement:
                    VisitIfStatement((IfStatement)node);
                    break;
                case NodeType.ForStatement:
                    VisitForStatement((ForStatement)node);
                    break;
                case NodeType.SwitchStatement:
                    VisitSwitchStatement((SwitchStatement)node);
                    break;
                case NodeType.SwitchCase:
                    VisitSwitchCase((SwitchCase)node);
                    break;
                case NodeType.BranchStatement:
                    VisitBranchStatement((BranchStatement)node);
                    break;
                case NodeType.SourceFile:
                    VisitSourceFile((SourceFile)node);
                    break;
                case NodeType.PackageDeclaration:
                    VisitPackageDeclaration((PackageDeclaration)node);
                    break;
                case NodeType.ImportDeclaration:
                    VisitImportDeclaration((ImportDeclaration)node);
                    break;
                case NodeType.FunctionDeclaration:
                    VisitFunctionDeclaration((FunctionDeclaration)node);
                    break;
                case NodeType.VarDeclaration:
                    VisitVarDeclaration((VarDeclaration)node);
                    break;
                case NodeType.TypeDeclaration:
                    VisitTypeDeclaration((TypeDeclaration)node);
                    break;
                case NodeType.SelectorExpression:
                    VisitSelectorExpression((SelectorExpression)node);
                    break;
                case NodeType.CompositeLiteralExpression:
                    VisitCompositeLiteralExpression((CompositeLiteralExpression)node);
                    break;
                case NodeType.AddressOfExpression:
                    VisitAddressOfExpression((AddressOfExpression)node);
                    break;
                case NodeType.DerefExpression:
                    VisitDerefExpression((DerefExpression)node);
                    break;
                case NodeType.IndexExpression:
                    VisitIndexExpression((IndexExpression)node);
                    break;
                case NodeType.SliceExpression:
                    VisitSliceExpression((SliceExpression)node);
                    break;
                case NodeType.ForRangeStatement:
                    VisitForRangeStatement((ForRangeStatement)node);
                    break;
                case NodeType.MethodDeclaration:
                    VisitMethodDeclaration((MethodDeclaration)node);
                    break;
                case NodeType.MethodCallExpression:
                    VisitMethodCallExpression((MethodCallExpression)node);
                    break;
                case NodeType.TypeAssertExpression:
                    VisitTypeAssertExpression((TypeAssertExpression)node);
                    break;
                case NodeType.TypeSwitchStatement:
                    VisitTypeSwitchStatement((TypeSwitchStatement)node);
                    break;
                case NodeType.TypeSwitchCase:
                    VisitTypeSwitchCase((TypeSwitchCase)node);
                    break;
                case NodeType.ConstDeclaration:
                    VisitConstDeclaration((ConstDeclaration)node);
                    break;
                case NodeType.FunctionLiteralExpression:
                    VisitFunctionLiteralExpression((FunctionLiteralExpression)node);
                    break;
                case NodeType.SelectStatement:
                    VisitSelectStatement((SelectStatement)node);
                    break;
                case NodeType.SelectCase:
                    VisitSelectCase((SelectCase)node);
                    break;
                default:
                    throw new ArgumentException($"Unexpected node type: {node.NodeType}");
            }
        }

        protected virtual void VisitLiteralExpression(LiteralExpression node) { }
        protected virtual void VisitBinaryExpression(BinaryExpression node) { }
        protected virtual void VisitUnaryExpression(UnaryExpression node) { }
        protected virtual void VisitIdentifierExpression(IdentifierExpression node) { }
        protected virtual void VisitCallExpression(CallExpression node) { }
        protected virtual void VisitConversionExpression(ConversionExpression node) { }
        protected virtual void VisitErrorExpression(ErrorExpression node) { }
        protected virtual void VisitBlockStatement(BlockStatement node) { }
        protected virtual void VisitReturnStatement(ReturnStatement node) { }
        protected virtual void VisitExpressionStatement(ExpressionStatement node) { }
        protected virtual void VisitAssignmentStatement(AssignmentStatement node) { }
        protected virtual void VisitIncDecStatement(IncDecStatement node) { }
        protected virtual void VisitIfStatement(IfStatement node) { }
        protected virtual void VisitForStatement(ForStatement node) { }
        protected virtual void VisitSwitchStatement(SwitchStatement node) { }
        protected virtual void VisitSwitchCase(SwitchCase node) { }
        protected virtual void VisitBranchStatement(BranchStatement node) { }
        protected virtual void VisitSourceFile(SourceFile node) { }
        protected virtual void VisitPackageDeclaration(PackageDeclaration node) { }
        protected virtual void VisitImportDeclaration(ImportDeclaration node) { }
        protected virtual void VisitFunctionDeclaration(FunctionDeclaration node) { }
        protected virtual void VisitVarDeclaration(VarDeclaration node) { }
        protected virtual void VisitTypeDeclaration(TypeDeclaration node) { }
        protected virtual void VisitSelectorExpression(SelectorExpression node) { }
        protected virtual void VisitCompositeLiteralExpression(CompositeLiteralExpression node) { }
        protected virtual void VisitAddressOfExpression(AddressOfExpression node) { }
        protected virtual void VisitDerefExpression(DerefExpression node) { }
        protected virtual void VisitIndexExpression(IndexExpression node) { }
        protected virtual void VisitSliceExpression(SliceExpression node) { }
        protected virtual void VisitForRangeStatement(ForRangeStatement node) { }
        protected virtual void VisitMethodDeclaration(MethodDeclaration node) { }
        protected virtual void VisitMethodCallExpression(MethodCallExpression node) { }
        protected virtual void VisitTypeAssertExpression(TypeAssertExpression node) { }
        protected virtual void VisitTypeSwitchStatement(TypeSwitchStatement node) { }
        protected virtual void VisitTypeSwitchCase(TypeSwitchCase node) { }
        protected virtual void VisitConstDeclaration(ConstDeclaration node) { }
        protected virtual void VisitFunctionLiteralExpression(FunctionLiteralExpression node) { }
        protected virtual void VisitSelectStatement(SelectStatement node) { }
        protected virtual void VisitSelectCase(SelectCase node) { }
    }

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

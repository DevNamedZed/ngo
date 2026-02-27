// -----------------------------------------------------------------------
// <copyright file="NodeType.cs" company="Ziad">
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

namespace Ngo.Compiler.Ast
{
    public enum NodeType
    {
        // Expressions
        LiteralExpression,
        BinaryExpression,
        UnaryExpression,
        IdentifierExpression,
        CallExpression,
        ConversionExpression,
        ErrorExpression,
        SelectorExpression,
        CompositeLiteralExpression,
        AddressOfExpression,
        DerefExpression,
        IndexExpression,
        SliceExpression,
        MethodCallExpression,
        TypeAssertExpression,
        FunctionLiteralExpression,
        MethodValueExpression,

        // Statements
        BlockStatement,
        ReturnStatement,
        ExpressionStatement,
        AssignmentStatement,
        MultiAssignmentStatement,
        IncDecStatement,
        IfStatement,
        ForStatement,
        SwitchStatement,
        SwitchCase,
        BranchStatement,
        ForRangeStatement,
        TypeSwitchStatement,
        TypeSwitchCase,
        DeferStatement,
        GoStatement,
        SendStatement,
        ReceiveExpression,
        SelectStatement,
        SelectCase,
        LabeledStatement,
        ParallelAssignmentStatement,

        // Declarations
        SourceFile,
        PackageDeclaration,
        ImportDeclaration,
        FunctionDeclaration,
        VarDeclaration,
        MultiVarDeclaration,
        TypeDeclaration,
        MethodDeclaration,
        ConstDeclaration,
    }
}

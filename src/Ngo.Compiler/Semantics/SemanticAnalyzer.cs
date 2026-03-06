// -----------------------------------------------------------------------
// <copyright file="SemanticAnalyzer.cs" company="Ziad">
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

using System.Collections.Generic;
using Ngo.Compiler.Language;

namespace Ngo.Compiler.Semantics
{
    public sealed class SemanticAnalyzer
    {
        public static AnalysisResult Analyze(SyntaxTree tree, bool checkUnused = false)
        {
            return Analyze(new[] { tree }, checkUnused);
        }

        public static AnalysisResult Analyze(IReadOnlyList<SyntaxTree> trees, bool checkUnused = false)
        {
            var universe = AnalysisContext.CreateUniverseScope();
            var context = new AnalysisContext(universe);
            context.CheckUnused = checkUnused;

            var typeResolver = new TypeResolver(context);
            var builtinResolver = new BuiltinResolver(context, typeResolver);
            var callResolver = new CallResolver(context, typeResolver, builtinResolver);
            var expressionResolver = new ExpressionResolver(context, typeResolver, callResolver);

            builtinResolver.SetExpressionResolver(expressionResolver.ResolveExpression);
            callResolver.SetExpressionResolver(expressionResolver.ResolveExpression);

            var statementResolver = new StatementResolver(context, expressionResolver, typeResolver);
            expressionResolver.SetBlockResolver(statementResolver.ResolveBlock);

            var declarationResolver = new DeclarationResolver(
                context, typeResolver, expressionResolver, statementResolver);
            statementResolver.SetDeclarationResolvers(
                declarationResolver.ResolveVarDeclarationStatement,
                declarationResolver.ResolveConstDeclarationStatement);

            var roots = new List<Language.Syntax.SourceFileSyntax>();
            foreach (var tree in trees)
                roots.Add(tree.Root);

            var root = declarationResolver.ResolveSourceFiles(roots);

            var allErrors = new List<CompileError>();
            foreach (var tree in trees)
                allErrors.AddRange(tree.Errors);
            allErrors.AddRange(context.Errors.ToReadOnlyList());
            return new AnalysisResult(root, allErrors);
        }
    }
}

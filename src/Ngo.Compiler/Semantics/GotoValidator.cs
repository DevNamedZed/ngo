// -----------------------------------------------------------------------
// <copyright file="GotoValidator.cs" company="Ziad">
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
using Ngo.Compiler.Ast;
using Ngo.Compiler.Language;

namespace Ngo.Compiler.Semantics
{
    /// <summary>
    /// Validates goto statements in a function body per the Go specification:
    /// - Labels must be defined in the same function
    /// - Labels must not be duplicated
    /// - A goto must not jump over variable declarations
    /// - A goto must not jump into an inner block
    /// </summary>
    internal static class GotoValidator
    {
        private sealed class LabelInfo
        {
            public TextSpan Span;
            public List<int> ScopePath = null!;
            public int IndexInBlock;
        }

        private sealed class GotoInfo
        {
            public string Label = null!;
            public TextSpan Span;
            public List<int> ScopePath = null!;
            public int IndexInBlock;
        }

        private sealed class VarDeclInfo
        {
            public List<int> ScopePath = null!;
            public int IndexInBlock;
        }

        public static void Validate(BlockStatement body, ErrorCollector errors)
        {
            var labels = new Dictionary<string, LabelInfo>();
            var gotos = new List<GotoInfo>();
            var varDecls = new List<VarDeclInfo>();

            // Walk the entire function body and collect labels, gotos, and var decls
            CollectFromBlock(body, new List<int>(), labels, gotos, varDecls, errors);

            // Check undefined labels
            foreach (var g in gotos)
            {
                if (!labels.ContainsKey(g.Label))
                {
                    errors.ReportError(g.Span, ErrorCode.UndefinedLabel,
                        $"Label '{g.Label}' not defined");
                }
            }

            // Check jump-into-block and jump-over-declaration
            foreach (var g in gotos)
            {
                if (!labels.TryGetValue(g.Label, out var target))
                    continue;

                // Jump into block: the goto's scope path must be a prefix of the label's
                // scope path, or they must share the same scope path. If the label is in
                // a deeper or sibling scope that the goto is not in, that's jumping into a block.
                if (!IsValidJumpTarget(g.ScopePath, target.ScopePath))
                {
                    errors.ReportError(g.Span, ErrorCode.GotoJumpsIntoBlock,
                        $"Goto '{g.Label}' jumps into a block");
                    continue;
                }

                // Jump over declaration: for forward gotos in the same scope,
                // check if any variable declaration exists between goto and label
                if (ScopePathsEqual(g.ScopePath, target.ScopePath))
                {
                    if (g.IndexInBlock < target.IndexInBlock)
                    {
                        // Forward jump in same scope — check for var decls between
                        foreach (var v in varDecls)
                        {
                            if (ScopePathsEqual(v.ScopePath, g.ScopePath)
                                && v.IndexInBlock > g.IndexInBlock
                                && v.IndexInBlock < target.IndexInBlock)
                            {
                                errors.ReportError(g.Span, ErrorCode.GotoJumpsOverDeclaration,
                                    $"Goto '{g.Label}' jumps over variable declaration");
                                break;
                            }
                        }
                    }
                }
                else if (IsPrefixOf(g.ScopePath, target.ScopePath))
                {
                    // Goto is in an outer scope, label is in an inner scope at the
                    // same nesting path — check var decls in goto's scope between
                    // goto position and the block containing the label
                    // (This is the "jumping into block" case which we already handle above
                    // via IsValidJumpTarget — if we reach here, it's because the label
                    // is in a nested block within the same containing block.
                    // This is actually a jump-into-block, handled above.)
                }
            }
        }

        private static void CollectFromBlock(BlockStatement block, List<int> scopePath,
            Dictionary<string, LabelInfo> labels, List<GotoInfo> gotos,
            List<VarDeclInfo> varDecls, ErrorCollector errors)
        {
            for (int i = 0; i < block.Statements.Count; i++)
            {
                CollectFromNode(block.Statements[i], scopePath, i, labels, gotos, varDecls, errors);
            }
        }

        private static void CollectFromNode(AstNode node, List<int> scopePath, int indexInBlock,
            Dictionary<string, LabelInfo> labels, List<GotoInfo> gotos,
            List<VarDeclInfo> varDecls, ErrorCollector errors)
        {
            switch (node)
            {
                case LabeledStatement labeled:
                    if (labels.ContainsKey(labeled.Label))
                    {
                        errors.ReportError(labeled.Span, ErrorCode.DuplicateLabel,
                            $"Label '{labeled.Label}' already defined");
                    }
                    else
                    {
                        labels[labeled.Label] = new LabelInfo
                        {
                            Span = labeled.Span,
                            ScopePath = new List<int>(scopePath),
                            IndexInBlock = indexInBlock,
                        };
                    }
                    // Recurse into the inner statement (it may be a block, loop, etc.)
                    CollectFromNode(labeled.InnerStatement, scopePath, indexInBlock,
                        labels, gotos, varDecls, errors);
                    break;

                case BranchStatement branch when branch.BranchKind == BranchKind.Goto && branch.Label != null:
                    gotos.Add(new GotoInfo
                    {
                        Label = branch.Label,
                        Span = branch.Span,
                        ScopePath = new List<int>(scopePath),
                        IndexInBlock = indexInBlock,
                    });
                    break;

                case VarDeclaration:
                case MultiVarDeclaration:
                    varDecls.Add(new VarDeclInfo
                    {
                        ScopePath = new List<int>(scopePath),
                        IndexInBlock = indexInBlock,
                    });
                    break;

                case BlockStatement innerBlock:
                    var blockPath = new List<int>(scopePath) { indexInBlock };
                    CollectFromBlock(innerBlock, blockPath, labels, gotos, varDecls, errors);
                    break;

                case IfStatement ifStmt:
                    CollectFromIfStatement(ifStmt, scopePath, indexInBlock,
                        labels, gotos, varDecls, errors);
                    break;

                case ForStatement forStmt:
                    var forPath = new List<int>(scopePath) { indexInBlock };
                    CollectFromBlock(forStmt.Body, forPath, labels, gotos, varDecls, errors);
                    break;

                case ForRangeStatement forRange:
                    var rangePath = new List<int>(scopePath) { indexInBlock };
                    CollectFromBlock(forRange.Body, rangePath, labels, gotos, varDecls, errors);
                    break;

                case SwitchStatement switchStmt:
                    for (int c = 0; c < switchStmt.Cases.Count; c++)
                    {
                        var casePath = new List<int>(scopePath) { indexInBlock, c };
                        CollectFromNodeList(switchStmt.Cases[c].Body, casePath,
                            labels, gotos, varDecls, errors);
                    }
                    break;

                case TypeSwitchStatement typeSwitchStmt:
                    for (int c = 0; c < typeSwitchStmt.Cases.Count; c++)
                    {
                        var casePath = new List<int>(scopePath) { indexInBlock, c };
                        CollectFromNodeList(typeSwitchStmt.Cases[c].Body, casePath,
                            labels, gotos, varDecls, errors);
                    }
                    break;

                case SelectStatement selectStmt:
                    for (int c = 0; c < selectStmt.Cases.Count; c++)
                    {
                        var casePath = new List<int>(scopePath) { indexInBlock, c };
                        CollectFromNodeList(selectStmt.Cases[c].Body, casePath,
                            labels, gotos, varDecls, errors);
                    }
                    break;

                case DeferStatement deferStmt:
                    // Defer wraps a call expression — no inner blocks to recurse into
                    break;

                case GoStatement goStmt:
                    // Go wraps a call expression — no inner blocks to recurse into
                    break;
            }
        }

        private static void CollectFromNodeList(IReadOnlyList<AstNode> nodes, List<int> scopePath,
            Dictionary<string, LabelInfo> labels, List<GotoInfo> gotos,
            List<VarDeclInfo> varDecls, ErrorCollector errors)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                CollectFromNode(nodes[i], scopePath, i, labels, gotos, varDecls, errors);
            }
        }

        private static void CollectFromIfStatement(IfStatement ifStmt,
            List<int> scopePath, int indexInBlock,
            Dictionary<string, LabelInfo> labels, List<GotoInfo> gotos,
            List<VarDeclInfo> varDecls, ErrorCollector errors)
        {
            var thenPath = new List<int>(scopePath) { indexInBlock, 0 };
            CollectFromBlock(ifStmt.Body, thenPath, labels, gotos, varDecls, errors);

            if (ifStmt.ElseBody is BlockStatement elseBlock)
            {
                var elsePath = new List<int>(scopePath) { indexInBlock, 1 };
                CollectFromBlock(elseBlock, elsePath, labels, gotos, varDecls, errors);
            }
            else if (ifStmt.ElseBody is IfStatement elseIf)
            {
                CollectFromIfStatement(elseIf, scopePath, indexInBlock,
                    labels, gotos, varDecls, errors);
            }
        }

        /// <summary>
        /// A jump is valid if the goto's scope path is a prefix of (or equal to) the
        /// label's scope path. This means the goto is in the same scope or an enclosing
        /// scope — never jumping INTO a block.
        ///
        /// Actually, per Go spec, goto can only target labels in the same scope or an
        /// enclosing scope. The label's scope path must be a prefix of or equal to
        /// the goto's scope path — i.e., the label must be in the same or outer scope.
        /// OR they must be in the same scope.
        ///
        /// Wait — Go spec says: "A 'goto' statement in a block cannot jump to a label
        /// inside an inner block of that block." So goto from outer to inner is forbidden.
        /// Goto from inner to outer is allowed. Same scope is allowed.
        ///
        /// So: the label's scope path must be a prefix of (or equal to) the goto's scope
        /// path. The label must be at the same level or higher (outer) than the goto.
        /// </summary>
        private static bool IsValidJumpTarget(List<int> gotoPath, List<int> labelPath)
        {
            // Label must be in same scope or an enclosing (outer) scope.
            // That means labelPath must be a prefix of gotoPath, or equal to it.
            return IsPrefixOf(labelPath, gotoPath);
        }

        private static bool IsPrefixOf(List<int> prefix, List<int> full)
        {
            if (prefix.Count > full.Count)
                return false;

            for (int i = 0; i < prefix.Count; i++)
            {
                if (prefix[i] != full[i])
                    return false;
            }
            return true;
        }

        private static bool ScopePathsEqual(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}

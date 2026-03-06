// -----------------------------------------------------------------------
// <copyright file="FlowAnalyzer.cs" company="Ziad">
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

using System.Linq;
using Ngo.Compiler.Ast;

namespace Ngo.Compiler.Semantics
{
    public static class FlowAnalyzer
    {
        public static bool AllPathsReturn(BlockStatement block)
        {
            if (block.Statements.Count == 0)
            {
                return false;
            }

            return StatementReturns(block.Statements[block.Statements.Count - 1]);
        }

        public static bool IsTerminating(AstNode node)
        {
            switch (node)
            {
                case ReturnStatement:
                    return true;

                case BranchStatement:
                    return true;

                case IfStatement ifStmt:
                    if (ifStmt.ElseBody == null)
                    {
                        return false;
                    }
                    return BlockTerminates(ifStmt.Body) && IsTerminating(ifStmt.ElseBody);

                case BlockStatement block:
                    return block.Statements.Count > 0
                        && IsTerminating(block.Statements[block.Statements.Count - 1]);

                case ForStatement forStmt:
                    return forStmt.Condition == null;

                case SwitchStatement switchStmt:
                    if (!switchStmt.Cases.Any(c => c.IsDefault))
                    {
                        return false;
                    }
                    return switchStmt.Cases.All(CaseTerminates);

                case TypeSwitchStatement typeSwitchStmt:
                    if (!typeSwitchStmt.Cases.Any(c => c.IsDefault))
                    {
                        return false;
                    }
                    return typeSwitchStmt.Cases.All(TypeCaseTerminates);

                case SelectStatement selectStmt:
                    if (!selectStmt.Cases.Any(c => c.IsDefault))
                    {
                        return false;
                    }
                    return selectStmt.Cases.All(SelectCaseTerminates);

                default:
                    return false;
            }
        }

        private static bool StatementReturns(AstNode node)
        {
            return IsTerminating(node);
        }

        private static bool BlockTerminates(BlockStatement block)
        {
            return block.Statements.Count > 0
                && IsTerminating(block.Statements[block.Statements.Count - 1]);
        }

        private static bool CaseTerminates(SwitchCase switchCase)
        {
            if (switchCase.Body.Count == 0)
            {
                return false;
            }

            return IsTerminating(switchCase.Body[switchCase.Body.Count - 1]);
        }

        private static bool TypeCaseTerminates(TypeSwitchCase typeCase)
        {
            if (typeCase.Body.Count == 0)
            {
                return false;
            }

            return IsTerminating(typeCase.Body[typeCase.Body.Count - 1]);
        }

        private static bool SelectCaseTerminates(SelectCase selectCase)
        {
            if (selectCase.Body.Count == 0)
            {
                return false;
            }

            return IsTerminating(selectCase.Body[selectCase.Body.Count - 1]);
        }
    }
}

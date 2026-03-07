// -----------------------------------------------------------------------
// <copyright file="AnalysisContext.cs" company="Ziad">
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
using Ngo.Compiler.Ast;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    public sealed class AnalysisContext
    {
        public ErrorCollector Errors { get; } = new();
        public Scope Scope { get; set; }
        public IReadOnlyList<TypeSymbol> CurrentReturnTypes { get; set; } = Array.Empty<TypeSymbol>();
        public IReadOnlyList<LocalSymbol> CurrentNamedReturns { get; set; } = Array.Empty<LocalSymbol>();
        public int IotaCounter { get; set; } = -1;
        public int LoopDepth { get; set; }
        public int SwitchDepth { get; set; }
        public HashSet<string> UsedPackages { get; } = new();
        public List<(LocalSymbol Symbol, TextSpan Span)> FunctionLocals { get; } = new();
        public bool CheckUnused { get; set; }
        public bool SuppressUsageMarking { get; set; }
        public Dictionary<string, int> PendingConstInts { get; } = new();

        public AnalysisContext(Scope universeScope)
        {
            Scope = universeScope;
        }

        public void PushScope(string name)
        {
            Scope = new Scope(name, Scope);
        }

        public void PopScope()
        {
            Scope = Scope.Parent!;
        }

        public void TrackLocal(LocalSymbol symbol, TextSpan span)
        {
            if (symbol.Name != "_" && CheckUnused)
            {
                FunctionLocals.Add((symbol, span));
            }
        }

        public TextSpan SpanOf(SyntaxNode node)
        {
            return node.Span;
        }

        public string? GetIdentifierName(ExpressionSyntax syntax)
        {
            if (syntax is IdentifierNameSyntax idSyntax)
            {
                return idSyntax.Identifier.Text;
            }

            return null;
        }

        public TypeSymbol GetSymbolType(Symbol symbol)
        {
            switch (symbol)
            {
                case ParameterSymbol param:
                    return param.Type;
                case LocalSymbol local:
                    return local.Type;
                case FunctionSymbol func:
                {
                    var paramTypes = new List<TypeSymbol>();
                    foreach (var p in func.Parameters)
                        paramTypes.Add(p.Type);
                    return new FunctionTypeSymbol(paramTypes, func.ReturnTypes, func.IsVariadic);
                }
                case ConstantSymbol constant:
                    return constant.Type;
                case PackageVarSymbol pkgVar:
                    return pkgVar.Type;
                case TypeSymbol type:
                    return type;
                default:
                    return TypeSymbol.Error;
            }
        }

        public IReadOnlyList<TypeSymbol>? GetCallReturnTypes(Expression expr)
        {
            if (expr is CallExpression call && call.Function.ReturnTypes.Count > 1)
            {
                return call.Function.ReturnTypes;
            }

            if (expr is MethodCallExpression methodCall && methodCall.Method.ReturnTypes.Count > 1)
            {
                return methodCall.Method.ReturnTypes;
            }

            // Comma-ok patterns: val, ok := m[key] / x.(T) / <-ch
            if (expr is Ast.IndexExpression idx && idx.Target.Type.Resolved() is Symbols.MapTypeSymbol mapType)
            {
                idx.IsCommaOk = true;
                return new[] { mapType.ValueType, Symbols.BuiltinTypes.Bool };
            }

            if (expr is Ast.TypeAssertExpression typeAssert)
            {
                typeAssert.IsCommaOk = true;
                return new[] { typeAssert.AssertedType, Symbols.BuiltinTypes.Bool };
            }

            if (expr is Ast.ReceiveExpression recv)
            {
                recv.IsCommaOk = true;
                return new[] { recv.ElementType, Symbols.BuiltinTypes.Bool };
            }

            return null;
        }

        public object? TryEvaluateConstant(Expression? expr)
        {
            if (expr is LiteralExpression lit)
            {
                return lit.Value;
            }

            if (expr is IdentifierExpression id && id.Symbol is ConstantSymbol cs)
            {
                return cs.Value;
            }

            if (expr is BinaryExpression bin)
            {
                var leftVal = TryEvaluateConstant(bin.Left);
                var rightVal = TryEvaluateConstant(bin.Right);

                if (leftVal is long l && rightVal is long r)
                {
                    return bin.Operator switch
                    {
                        BinaryOperator.Add => (object)(l + r),
                        BinaryOperator.Subtract => (object)(l - r),
                        BinaryOperator.Multiply => (object)(l * r),
                        BinaryOperator.Divide when r != 0 => (object)(l / r),
                        BinaryOperator.Remainder when r != 0 => (object)(l % r),
                        BinaryOperator.BitwiseAnd => (object)(l & r),
                        BinaryOperator.BitwiseOr => (object)(l | r),
                        BinaryOperator.BitwiseXor => (object)(l ^ r),
                        BinaryOperator.AndNot => (object)(l & ~r),
                        BinaryOperator.ShiftLeft => (object)(l << (int)r),
                        BinaryOperator.ShiftRight => (object)(l >> (int)r),
                        BinaryOperator.Equal => (object)(l == r),
                        BinaryOperator.NotEqual => (object)(l != r),
                        BinaryOperator.Less => (object)(l < r),
                        BinaryOperator.Greater => (object)(l > r),
                        BinaryOperator.LessOrEqual => (object)(l <= r),
                        BinaryOperator.GreaterOrEqual => (object)(l >= r),
                        _ => null,
                    };
                }

                if ((leftVal is long || leftVal is double) && (rightVal is long || rightVal is double))
                {
                    double dl = System.Convert.ToDouble(leftVal);
                    double dr = System.Convert.ToDouble(rightVal);
                    return bin.Operator switch
                    {
                        BinaryOperator.Add => (object)(dl + dr),
                        BinaryOperator.Subtract => (object)(dl - dr),
                        BinaryOperator.Multiply => (object)(dl * dr),
                        BinaryOperator.Divide when dr != 0 => (object)(dl / dr),
                        BinaryOperator.Equal => (object)(dl == dr),
                        BinaryOperator.NotEqual => (object)(dl != dr),
                        BinaryOperator.Less => (object)(dl < dr),
                        BinaryOperator.Greater => (object)(dl > dr),
                        BinaryOperator.LessOrEqual => (object)(dl <= dr),
                        BinaryOperator.GreaterOrEqual => (object)(dl >= dr),
                        _ => null,
                    };
                }

                if (leftVal is string sl && rightVal is string sr && bin.Operator == BinaryOperator.Add)
                {
                    return sl + sr;
                }

                if (leftVal is bool bl && rightVal is bool br)
                {
                    return bin.Operator switch
                    {
                        BinaryOperator.LogicalAnd => (object)(bl && br),
                        BinaryOperator.LogicalOr => (object)(bl || br),
                        BinaryOperator.Equal => (object)(bl == br),
                        BinaryOperator.NotEqual => (object)(bl != br),
                        _ => null,
                    };
                }
            }

            if (expr is UnaryExpression unary)
            {
                var val = TryEvaluateConstant(unary.Operand);
                if (val is long v)
                {
                    return unary.Operator switch
                    {
                        UnaryOperator.Negate => (object)(-v),
                        UnaryOperator.Plus => (object)v,
                        UnaryOperator.BitwiseNot => (object)(~v),
                        _ => null,
                    };
                }
                if (val is double dv)
                {
                    return unary.Operator switch
                    {
                        UnaryOperator.Negate => (object)(-dv),
                        UnaryOperator.Plus => (object)dv,
                        _ => null,
                    };
                }
                if (val is bool b && unary.Operator == UnaryOperator.LogicalNot)
                {
                    return (object)(!b);
                }
            }

            return null;
        }

        public static Scope CreateUniverseScope()
        {
            var scope = new Scope("universe", null);

            scope.TryDeclare(BuiltinTypes.Bool);
            scope.TryDeclare(BuiltinTypes.Int);
            scope.TryDeclare(BuiltinTypes.Int8);
            scope.TryDeclare(BuiltinTypes.Int16);
            scope.TryDeclare(BuiltinTypes.Int32);
            scope.TryDeclare(BuiltinTypes.Int64);
            scope.TryDeclare(BuiltinTypes.Uint);
            scope.TryDeclare(BuiltinTypes.Uint8);
            scope.TryDeclare(BuiltinTypes.Uint16);
            scope.TryDeclare(BuiltinTypes.Uint32);
            scope.TryDeclare(BuiltinTypes.Uint64);
            scope.TryDeclare(BuiltinTypes.Float32);
            scope.TryDeclare(BuiltinTypes.Float64);
            scope.TryDeclare(BuiltinTypes.String);
            scope.TryDeclare(BuiltinTypes.Byte);
            scope.TryDeclare(BuiltinTypes.Rune);
            scope.TryDeclare(BuiltinTypes.Error);

            return scope;
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="TypeResolver.cs" company="Ziad">
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
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    public sealed class TypeResolver
    {
        private readonly AnalysisContext _context;

        public TypeResolver(AnalysisContext context)
        {
            _context = context;
        }

        public TypeSymbol? ResolveType(ExpressionSyntax syntax)
        {
            // Qualified type name: pkg.Type (e.g. sync.WaitGroup)
            if (syntax is SelectorExpressionSyntax selectorSyntax)
            {
                if (selectorSyntax.Expression is IdentifierNameSyntax pkgIdSyntax)
                {
                    var symbol = _context.Scope.Lookup(pkgIdSyntax.Identifier.Text);
                    if (symbol is PackageSymbol pkg)
                    {
                        _context.UsedPackages.Add(pkg.Name);
                        var export = pkg.LookupExport(selectorSyntax.Name.Text);
                        if (export is TypeSymbol typeSymbol)
                        {
                            return typeSymbol;
                        }

                        _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UndeclaredName,
                            $"Package '{pkg.Name}' has no exported type '{selectorSyntax.Name.Text}'");
                        return null;
                    }

                    // Identifier not found in scope — likely an unresolved package
                    if (symbol == null)
                    {
                        _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UndeclaredName,
                            $"Undefined name '{pkgIdSyntax.Identifier.Text}'");
                        return null;
                    }
                }

                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UnsupportedSyntax,
                    "Invalid qualified type name");
                return null;
            }

            if (syntax is IdentifierNameSyntax idSyntax)
            {
                var resolved = BuiltinTypes.Resolve(idSyntax.Identifier.Text);
                if (resolved != null)
                {
                    return resolved;
                }

                var symbol = _context.Scope.Lookup(idSyntax.Identifier.Text);
                if (symbol is TypeParameterSymbol typeParamSymbol)
                {
                    return typeParamSymbol;
                }

                if (symbol is TypeSymbol typeSymbol)
                {
                    return typeSymbol;
                }

                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UndeclaredName,
                    $"Undefined type '{idSyntax.Identifier.Text}'");
                return null;
            }

            // Type instantiation with multiple type args: Type[int, string]
            if (syntax is TypeArgumentListSyntax typeArgListSyntax)
            {
                var baseType = ResolveType(typeArgListSyntax.Expression);
                if (baseType == null)
                    return null;

                var typeArgs = new List<TypeSymbol>();
                for (int i = 0; i < typeArgListSyntax.TypeArguments.Count; i++)
                {
                    var arg = ResolveType(typeArgListSyntax.TypeArguments[i]);
                    typeArgs.Add(arg ?? TypeSymbol.Error);
                }

                return new InstantiatedTypeSymbol(baseType, typeArgs);
            }

            // Single type arg instantiation: Type[int] — comes as IndexExpressionSyntax
            // when the base resolves to a generic type
            if (syntax is IndexExpressionSyntax indexSyntax)
            {
                // Check if this is a type instantiation
                TypeSymbol? baseSym = null;
                if (indexSyntax.Expression is IdentifierNameSyntax baseIdSyntax)
                {
                    baseSym = _context.Scope.Lookup(baseIdSyntax.Identifier.Text) as TypeSymbol;
                }
                else if (indexSyntax.Expression is SelectorExpressionSyntax selSyntax
                    && selSyntax.Expression is IdentifierNameSyntax pkgId)
                {
                    var pkgSym = _context.Scope.Lookup(pkgId.Identifier.Text);
                    if (pkgSym is PackageSymbol pkg)
                        baseSym = pkg.LookupExport(selSyntax.Name.Text) as TypeSymbol;
                }
                if (baseSym != null && baseSym.IsGeneric)
                {
                    var argType = ResolveType(indexSyntax.Index);
                    return new InstantiatedTypeSymbol(baseSym, new[] { argType ?? TypeSymbol.Error });
                }
            }

            if (syntax is PointerTypeSyntax pointerSyntax)
            {
                var elementType = ResolveType(pointerSyntax.ElementType);
                if (elementType == null)
                {
                    return null;
                }

                return new PointerTypeSymbol(elementType);
            }

            // *T in expression context is parsed as UnaryExpression(Star, T)
            if (syntax is UnaryExpressionSyntax unarySyntax
                && unarySyntax.OperatorToken.Kind == SyntaxKind.StarToken)
            {
                var elementType = ResolveType(unarySyntax.Operand);
                if (elementType == null)
                {
                    return null;
                }

                return new PointerTypeSymbol(elementType);
            }

            if (syntax is StructTypeSyntax structSyntax)
            {
                return ResolveAnonymousStruct(structSyntax);
            }

            if (syntax is InterfaceTypeSyntax ifaceSyntax)
            {
                return ResolveAnonymousInterface(ifaceSyntax);
            }

            if (syntax is SliceTypeSyntax sliceSyntax)
            {
                var elementType = ResolveType(sliceSyntax.ElementType);
                if (elementType == null)
                {
                    return null;
                }

                return new SliceTypeSymbol(elementType);
            }

            if (syntax is ArrayTypeSyntax arraySyntax)
            {
                var elementType = ResolveType(arraySyntax.ElementType);
                if (elementType == null)
                {
                    return null;
                }

                // [...]T — length inferred from composite literal
                if (arraySyntax.Length is LiteralExpressionSyntax lit
                    && lit.Token.Kind == SyntaxKind.EllipsisToken)
                {
                    return new ArrayTypeSymbol(elementType, -1);
                }

                // [N]T — length is an integer literal
                if (arraySyntax.Length is LiteralExpressionSyntax lengthLit
                    && lengthLit.Token.Kind == SyntaxKind.IntLiteralToken
                    && int.TryParse(lengthLit.Token.Text, out var length))
                {
                    return new ArrayTypeSymbol(elementType, length);
                }

                // [CONST]T — length is a constant identifier
                if (arraySyntax.Length is IdentifierNameSyntax constId)
                {
                    var sym = _context.Scope.Lookup(constId.Identifier.Text);
                    if (sym is ConstantSymbol constSym && constSym.Value is long lval)
                    {
                        return new ArrayTypeSymbol(elementType, (int)lval);
                    }
                    if (sym is ConstantSymbol constSym2 && constSym2.Value is int ival)
                    {
                        return new ArrayTypeSymbol(elementType, ival);
                    }
                }

                // Try evaluating constant expression (e.g. unicode.MaxASCII + 1)
                {
                    var constVal = TryEvalConstantLength(arraySyntax.Length);
                    if (constVal.HasValue)
                    {
                        return new ArrayTypeSymbol(elementType, constVal.Value);
                    }
                }

                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UnsupportedSyntax,
                    "Array length must be a constant integer");
                return null;
            }

            if (syntax is MapTypeSyntax mapSyntax)
            {
                var keyType = ResolveType(mapSyntax.KeyType);
                var valueType = ResolveType(mapSyntax.ValueType);
                if (keyType == null || valueType == null)
                {
                    return null;
                }

                return new MapTypeSymbol(keyType, valueType);
            }

            if (syntax is ChannelTypeSyntax chanSyntax)
            {
                var elemType = ResolveType(chanSyntax.ElementType);
                if (elemType == null) return null;
                return new ChannelTypeSymbol(elemType);
            }

            if (syntax is FuncTypeSyntax funcSyntax)
            {
                var paramTypes = new List<TypeSymbol>();
                bool isVariadic = false;
                for (int i = 0; i < funcSyntax.Parameters.Parameters.Count; i++)
                {
                    var param = funcSyntax.Parameters.Parameters[i];
                    if (param.Type != null)
                    {
                        var resolved = ResolveType(param.Type) ?? TypeSymbol.Error;
                        // Each named parameter gets its own entry (e.g., "prev, curr, next rune" → 3 params)
                        int count = param.Names.HasValue ? param.Names.Value.Count : 1;
                        for (int j = 0; j < count; j++)
                            paramTypes.Add(resolved);
                    }
                    if (param.Ellipsis != null)
                    {
                        isVariadic = true;
                        // Wrap variadic param type in slice (func(...T) → last param is []T)
                        if (paramTypes.Count > 0)
                        {
                            var lastIdx = paramTypes.Count - 1;
                            paramTypes[lastIdx] = new SliceTypeSymbol(paramTypes[lastIdx]);
                        }
                    }
                }

                var returnTypes = ResolveResultTypes(funcSyntax.Result);
                return new FunctionTypeSymbol(paramTypes, returnTypes, isVariadic);
            }

            // Parenthesized type: (T) — unwrap parens
            if (syntax is ParenthesizedExpressionSyntax parenSyntax)
            {
                return ResolveType(parenSyntax.Expression);
            }

            _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UnsupportedSyntax,
                "Complex type expressions are not yet supported");
            return null;
        }

        public StructTypeSymbol ResolveAnonymousStruct(StructTypeSyntax syntax)
        {
            var fields = new List<FieldSymbol>();
            int ordinal = 0;

            foreach (var fieldSyntax in syntax.Fields)
            {
                var fieldType = ResolveType(fieldSyntax.Type);
                if (fieldType == null)
                {
                    fieldType = TypeSymbol.Error;
                }

                if (fieldSyntax.Names.HasValue)
                {
                    for (int i = 0; i < fieldSyntax.Names.Value.Count; i++)
                    {
                        var name = fieldSyntax.Names.Value[i].Text;
                        fields.Add(new FieldSymbol(name, fieldType, ordinal++));
                    }
                }
                else
                {
                    // Embedded field: use the type name as the field name
                    var embeddedName = fieldType.Name;
                    fields.Add(new FieldSymbol(embeddedName, fieldType, ordinal++, isEmbedded: true));
                }
            }

            return new StructTypeSymbol("struct", fields);
        }

        public InterfaceTypeSymbol ResolveAnonymousInterface(InterfaceTypeSyntax syntax)
        {
            var methods = new List<MethodSymbol>();
            var placeholder = new InterfaceTypeSymbol("interface", new List<MethodSymbol>());

            foreach (var member in syntax.Members)
            {
                if (member is MethodSpecSyntax methodSpec)
                {
                    var parameters = ResolveParameterList(methodSpec.Parameters);
                    var returnTypes = ResolveResultTypes(methodSpec.Result);
                    var method = new MethodSymbol(methodSpec.Name.Text, placeholder, false,
                        parameters, returnTypes);
                    methods.Add(method);
                }
            }

            placeholder.SetMethods(methods);
            return placeholder;
        }

        public IReadOnlyList<ParameterSymbol> ResolveParameterList(ParameterListSyntax syntax)
        {
            var parameters = new List<ParameterSymbol>();
            int ordinal = 0;

            for (int i = 0; i < syntax.Parameters.Count; i++)
            {
                var paramSyntax = syntax.Parameters[i];

                // Resolve the parameter type
                var paramType = BuiltinTypes.Void;
                if (paramSyntax.Type != null)
                {
                    paramType = ResolveType(paramSyntax.Type) ?? BuiltinTypes.Void;
                }

                // Variadic parameter: wrap base type in a slice
                if (paramSyntax.Ellipsis != null)
                {
                    paramType = new SliceTypeSymbol(paramType);
                }

                // Each name in the parameter gets its own ParameterSymbol
                if (paramSyntax.Names.HasValue)
                {
                    for (int j = 0; j < paramSyntax.Names.Value.Count; j++)
                    {
                        var name = paramSyntax.Names.Value[j].Text;
                        parameters.Add(new ParameterSymbol(name, paramType, ordinal++));
                    }
                }
                else
                {
                    // Unnamed parameter
                    parameters.Add(new ParameterSymbol("_", paramType, ordinal++));
                }
            }

            return parameters;
        }

        public TypeSymbol ResolveResultType(SyntaxNode? result)
        {
            var types = ResolveResultTypes(result);
            return types.Count > 0 ? types[0] : BuiltinTypes.Void;
        }

        public IReadOnlyList<TypeSymbol> ResolveResultTypes(SyntaxNode? result)
        {
            if (result == null)
            {
                return Array.Empty<TypeSymbol>();
            }

            if (result is ExpressionSyntax typeExpr)
            {
                var resolved = ResolveType(typeExpr);
                if (resolved != null && resolved != BuiltinTypes.Void)
                {
                    return new[] { resolved };
                }

                return Array.Empty<TypeSymbol>();
            }

            if (result is ParameterListSyntax paramList && paramList.Parameters.Count > 0)
            {
                var types = new List<TypeSymbol>();
                for (int i = 0; i < paramList.Parameters.Count; i++)
                {
                    var param = paramList.Parameters[i];
                    if (param.Type != null)
                    {
                        var resolved = ResolveType(param.Type) ?? BuiltinTypes.Void;
                        // Named returns may group names: (ok, found bool) → 2 returns of type bool
                        int count = param.Names.HasValue ? param.Names.Value.Count : 1;
                        for (int j = 0; j < count; j++)
                            types.Add(resolved);
                    }
                }

                return types;
            }

            return Array.Empty<TypeSymbol>();
        }

        private int? TryEvalConstantLength(ExpressionSyntax expr)
        {
            if (expr is LiteralExpressionSyntax lit
                && lit.Token.Kind == SyntaxKind.IntLiteralToken
                && int.TryParse(lit.Token.Text, out var litVal))
            {
                return litVal;
            }

            if (expr is IdentifierNameSyntax id)
            {
                var sym = _context.Scope.Lookup(id.Identifier.Text);
                if (sym is ConstantSymbol c)
                {
                    return c.Value is long lv ? (int)lv : c.Value is int iv ? iv : null;
                }

                // Fallback: search pending const syntax for simple integer constants not yet in scope
                var syntaxVal = TryLookupConstFromSyntax(id.Identifier.Text);
                if (syntaxVal.HasValue)
                {
                    return syntaxVal;
                }
            }

            if (expr is SelectorExpressionSyntax sel
                && sel.Expression is IdentifierNameSyntax pkgId)
            {
                var pkgSym = _context.Scope.Lookup(pkgId.Identifier.Text);
                if (pkgSym is PackageSymbol pkg)
                {
                    var member = pkg.LookupExport(sel.Name.Text);
                    if (member is ConstantSymbol c)
                    {
                        return c.Value is long lv ? (int)lv : c.Value is int iv ? iv : null;
                    }
                }
            }

            if (expr is UnaryExpressionSyntax unary
                && unary.OperatorToken.Kind == SyntaxKind.MinusToken)
            {
                var inner = TryEvalConstantLength(unary.Operand);
                if (inner.HasValue) return -inner.Value;
            }

            if (expr is BinaryExpressionSyntax bin)
            {
                var left = TryEvalConstantLength(bin.Left);
                var right = TryEvalConstantLength(bin.Right);
                if (left.HasValue && right.HasValue)
                {
                    return bin.OperatorToken.Kind switch
                    {
                        SyntaxKind.PlusToken => left.Value + right.Value,
                        SyntaxKind.MinusToken => left.Value - right.Value,
                        SyntaxKind.StarToken => left.Value * right.Value,
                        SyntaxKind.SlashToken when right.Value != 0 => left.Value / right.Value,
                        _ => (int?)null,
                    };
                }
            }

            return null;
        }

        private int? TryLookupConstFromSyntax(string name)
        {
            if (_context.PendingConstInts.TryGetValue(name, out var val))
            {
                return val;
            }
            return null;
        }
    }
}

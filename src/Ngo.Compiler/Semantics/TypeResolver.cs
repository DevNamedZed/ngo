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
                        var export = pkg.LookupExport(selectorSyntax.Name.Text);
                        if (export is TypeSymbol typeSymbol)
                        {
                            return typeSymbol;
                        }

                        _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UndeclaredName,
                            $"Package '{pkg.Name}' has no exported type '{selectorSyntax.Name.Text}'");
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
                if (symbol is TypeSymbol typeSymbol)
                {
                    return typeSymbol;
                }

                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UndeclaredName,
                    $"Undefined type '{idSyntax.Identifier.Text}'");
                return null;
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
                for (int i = 0; i < funcSyntax.Parameters.Parameters.Count; i++)
                {
                    var param = funcSyntax.Parameters.Parameters[i];
                    if (param.Type != null)
                    {
                        var resolved = ResolveType(param.Type);
                        paramTypes.Add(resolved ?? TypeSymbol.Error);
                    }
                }

                var returnTypes = ResolveResultTypes(funcSyntax.Result);
                return new FunctionTypeSymbol(paramTypes, returnTypes);
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
                        var resolved = ResolveType(param.Type);
                        types.Add(resolved ?? BuiltinTypes.Void);
                    }
                }

                return types;
            }

            return Array.Empty<TypeSymbol>();
        }
    }
}

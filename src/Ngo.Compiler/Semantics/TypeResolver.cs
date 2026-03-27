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

                    // Symbol found but not a package — might be shadowed by a local variable.
                    // Search parent scopes for a PackageSymbol with this name.
                    if (symbol != null)
                    {
                        var scope = _context.Scope;
                        while (scope != null)
                        {
                            var local = scope.LookupLocal(pkgIdSyntax.Identifier.Text);
                            if (local is PackageSymbol shadowedPkg)
                            {
                                _context.UsedPackages.Add(shadowedPkg.Name);
                                var export = shadowedPkg.LookupExport(selectorSyntax.Name.Text);
                                if (export is TypeSymbol ts2) return ts2;
                                break;
                            }
                            scope = scope.Parent;
                        }
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
                if (baseSym != null)
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
                        int clampedLength = lval > int.MaxValue ? int.MaxValue : lval < 0 ? 0 : (int)lval;
                        return new ArrayTypeSymbol(elementType, clampedLength);
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

                // Fallback: check PendingConstInts directly for simple identifiers
                if (arraySyntax.Length is IdentifierNameSyntax pendingId
                    && _context.PendingConstInts.TryGetValue(pendingId.Identifier.Text, out var pendingVal))
                {
                    int clampedVal = pendingVal > int.MaxValue ? int.MaxValue : pendingVal < int.MinValue ? int.MinValue : (int)pendingVal;
                    return new ArrayTypeSymbol(elementType, clampedVal);
                }

                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.UnsupportedSyntax, "Array length must be a constant integer");
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

            // Union type constraints: int | float64 | string (Go 1.18 generics)
            // These appear as type parameter constraints. Resolve to an empty interface
            // since the constraint checking is handled separately via ConstraintInfo.
            if (syntax is UnionTypeSyntax)
            {
                return BuiltinTypes.Resolve("interface{}");
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
                    // Embedded field: use the base type name as the field name
                    // For *T, the embedded name is T (not *T)
                    var embeddedType = fieldType is PointerTypeSymbol embPtr
                        ? embPtr.ElementType : fieldType;
                    var embeddedName = embeddedType.Name;
                    fields.Add(new FieldSymbol(embeddedName, fieldType, ordinal++, isEmbedded: true));
                }
            }

            if (fields.Count == 0)
            {
                return new StructTypeSymbol("struct{}", fields);
            }

            // Build a unique fingerprint from field names and types so that distinct
            // anonymous structs don't collide when serialized to archives.
            var fingerprint = new System.Text.StringBuilder("struct{");
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                {
                    fingerprint.Append(';');
                }
                fingerprint.Append(fields[i].Name);
                fingerprint.Append(' ');
                fingerprint.Append(fields[i].Type.Name);
            }
            fingerprint.Append('}');
            return new StructTypeSymbol(fingerprint.ToString(), fields);
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
                else if (member is ExpressionSyntax embeddedSyntax)
                {
                    var embeddedType = ResolveType(embeddedSyntax);
                    while (embeddedType != null && embeddedType.IsAlias && embeddedType.UnderlyingType != null)
                    {
                        embeddedType = embeddedType.UnderlyingType;
                    }
                    if (embeddedType is InterfaceTypeSymbol embeddedIface)
                    {
                        foreach (var m in embeddedIface.Methods)
                        {
                            var promoted = new MethodSymbol(m.Name, placeholder, false,
                                System.Array.Empty<TypeParameterSymbol>(), m.Parameters, m.ReturnTypes, m.IsVariadic);
                            methods.Add(promoted);
                        }
                    }
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
                && lit.Token.Kind == SyntaxKind.IntLiteralToken)
            {
                var text = lit.Token.Text;
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hexVal))
                    return hexVal;
                if (int.TryParse(text, out var litVal))
                    return litVal;
            }

            if (expr is IdentifierNameSyntax id)
            {
                var sym = _context.Scope.Lookup(id.Identifier.Text);
                if (sym is ConstantSymbol c)
                {
                    if (c.Value is long lv)
                    {
                        if (lv > int.MaxValue) return int.MaxValue;
                        if (lv < 0) return 0;
                        return (int)lv;
                    }
                    if (c.Value is int iv) return iv;
                    // Value not resolved yet — fall through to PendingConstInts
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
                        if (c.Value is long lv)
                        {
                            if (lv > int.MaxValue) return int.MaxValue;
                            if (lv < 0) return 0;
                            return (int)lv;
                        }
                        if (c.Value is int iv) return iv;
                        if (c.Value is bool bv) return bv ? 1 : 0;
                    }
                }

                // Known constants from unregistered packages (used in array lengths)
                if (pkgId.Identifier.Text == "cpu" && sel.Name.Text == "CacheLinePadSize")
                    return 64; // internal/cpu: amd64
            }

            // Parenthesized expressions: (expr)
            if (expr is ParenthesizedExpressionSyntax parenExpr)
            {
                return TryEvalConstantLength(parenExpr.Expression);
            }

            if (expr is UnaryExpressionSyntax unary)
            {
                var inner = TryEvalConstantLength(unary.Operand);
                if (inner.HasValue)
                {
                    return unary.OperatorToken.Kind switch
                    {
                        SyntaxKind.MinusToken => -inner.Value,
                        SyntaxKind.PlusToken => inner.Value,
                        SyntaxKind.CaretToken => ~inner.Value, // bitwise NOT in Go is ^
                        _ => (int?)null,
                    };
                }
            }

            if (expr is BinaryExpressionSyntax bin)
            {
                var left = TryEvalConstantLength(bin.Left);
                var right = TryEvalConstantLength(bin.Right);
                if (left.HasValue && right.HasValue)
                {
                    var binResult = bin.OperatorToken.Kind switch
                    {
                        SyntaxKind.PlusToken => left.Value + right.Value,
                        SyntaxKind.MinusToken => left.Value - right.Value,
                        SyntaxKind.StarToken => left.Value * right.Value,
                        SyntaxKind.SlashToken when right.Value != 0 => left.Value / right.Value,
                        SyntaxKind.PercentToken when right.Value != 0 => left.Value % right.Value,
                        SyntaxKind.AmpersandToken => left.Value & right.Value,
                        SyntaxKind.PipeToken => left.Value | right.Value,
                        SyntaxKind.CaretToken => left.Value ^ right.Value,
                        SyntaxKind.LessThanLessThanToken => left.Value << right.Value,
                        SyntaxKind.GreaterThanGreaterThanToken => left.Value >> right.Value,
                        SyntaxKind.AmpersandCaretToken => left.Value & ~right.Value, // &^ in Go
                        _ => (int?)null,
                    };
                    return binResult;
                }
            }

            // len(x) where x is an array with known length, or len("string literal")
            if (expr is CallExpressionSyntax lenCallExpr
                && lenCallExpr.Function is IdentifierNameSyntax lenId
                && lenId.Identifier.Text == "len"
                && lenCallExpr.Arguments.Count == 1)
            {
                var lenArg = lenCallExpr.Arguments[0];

                // len("string literal") — return byte length of UTF-8 encoding
                if (lenArg is LiteralExpressionSyntax lenLit
                    && lenLit.Token.Kind == SyntaxKind.StringLiteralToken)
                {
                    var raw = lenLit.Token.Text;
                    // Strip quotes — Go len() on string returns byte count (UTF-8)
                    if (raw.Length >= 2 && raw[0] == '"')
                    {
                        var inner = raw.Substring(1, raw.Length - 2);
                        return System.Text.Encoding.UTF8.GetByteCount(inner);
                    }
                    // Raw string literal `...`
                    if (raw.Length >= 2 && raw[0] == '`')
                    {
                        var inner = raw.Substring(1, raw.Length - 2);
                        return System.Text.Encoding.UTF8.GetByteCount(inner);
                    }
                }

                // len(StructType{}.field) — look up the struct type, find the field, return array length
                if (lenArg is SelectorExpressionSyntax lenSel
                    && lenSel.Expression is CompositeLiteralSyntax lenCompLit
                    && lenCompLit.Type != null)
                {
                    var structType = ResolveType(lenCompLit.Type)?.Resolved();
                    if (structType is StructTypeSymbol sts)
                    {
                        var field = sts.LookupField(lenSel.Name.Text);
                        if (field != null)
                        {
                            var fieldType = field.Type.Resolved();
                            // Unwrap named types to find underlying array
                            for (int i = 0; i < 10 && fieldType != null; i++)
                            {
                                if (fieldType is ArrayTypeSymbol arrField)
                                    return arrField.Length;
                                if (fieldType.UnderlyingType == null) break;
                                fieldType = fieldType.UnderlyingType.Resolved();
                            }
                        }
                    }
                }

                // len(variable.field) — resolve the variable, find the field, return array length
                if (lenArg is SelectorExpressionSyntax lenVarSel
                    && lenVarSel.Expression is IdentifierNameSyntax lenVarId)
                {
                    var varSym = _context.Scope.Lookup(lenVarId.Identifier.Text);
                    TypeSymbol? varType = null;
                    if (varSym is LocalSymbol ls) varType = ls.Type;
                    else if (varSym is ParameterSymbol ps) varType = ps.Type;
                    else if (varSym is PackageVarSymbol pvs) varType = pvs.Type;
                    if (varType != null)
                    {
                        var resolved = varType.Resolved();
                        // Unwrap pointer to struct (e.g. *p → p)
                        if (resolved is PointerTypeSymbol ptrVar)
                            resolved = ptrVar.ElementType.Resolved();
                        if (resolved is StructTypeSymbol varSts)
                        {
                            var field = varSts.LookupField(lenVarSel.Name.Text);
                            if (field != null)
                            {
                                var fieldType = field.Type.Resolved();
                                for (int i = 0; i < 10 && fieldType != null; i++)
                                {
                                    if (fieldType is ArrayTypeSymbol arrField)
                                        return arrField.Length;
                                    if (fieldType.UnderlyingType == null) break;
                                    fieldType = fieldType.UnderlyingType.Resolved();
                                }
                            }
                        }
                    }
                }

                if (lenArg is IdentifierNameSyntax lenArgId)
                {
                    // Try looking at pending var syntax for auto-sized array literals
                    if (_context.PendingVarArrayLens.TryGetValue(lenArgId.Identifier.Text, out var arrLen))
                        return arrLen;

                    // len(stringConst) — look up the constant and get its string length
                    var lenSym = _context.Scope.Lookup(lenArgId.Identifier.Text);
                    if (lenSym is ConstantSymbol lenConst && lenConst.Value is string lenStr)
                        return lenStr.Length;

                    // Fallback: check pre-scanned string constant lengths
                    if (_context.PendingConstStringLens.TryGetValue(lenArgId.Identifier.Text, out var strLen))
                        return strLen;
                }
            }

            // unsafe.Sizeof(...) — compute size of type for amd64
            if (expr is CallExpressionSyntax callExpr
                && callExpr.Function is SelectorExpressionSyntax callSel
                && callSel.Expression is IdentifierNameSyntax callPkg
                && callPkg.Identifier.Text == "unsafe"
                && callSel.Name.Text == "Sizeof"
                && callExpr.Arguments.Count == 1)
            {
                var arg = callExpr.Arguments[0];
                // Try to resolve the type of the argument and compute its size
                TypeSymbol? argType = null;

                // unsafe.Sizeof(Type{}) — composite literal
                if (arg is CompositeLiteralSyntax compLit && compLit.Type != null)
                    argType = ResolveType(compLit.Type);
                // unsafe.Sizeof(TypeName(0)) — type conversion
                else if (arg is CallExpressionSyntax convCall
                    && convCall.Function is IdentifierNameSyntax convId)
                    argType = _context.Scope.Lookup(convId.Identifier.Text) as TypeSymbol;
                // unsafe.Sizeof(*ptr) or unsafe.Sizeof(var) — resolve expression type
                else if (arg is UnaryExpressionSyntax unarySizeof
                    && unarySizeof.OperatorToken.Kind == SyntaxKind.StarToken
                    && unarySizeof.Operand is IdentifierNameSyntax unaryId)
                {
                    var sym = _context.Scope.Lookup(unaryId.Identifier.Text);
                    if (sym is TypeSymbol ts && ts is PointerTypeSymbol pts)
                        argType = pts.ElementType;
                }

                if (argType != null)
                {
                    var size = ComputeSizeOf(argType);
                    if (size.HasValue) return size.Value;
                }
                // Default: 8 (word size on amd64)
                return 8;
            }

            // unsafe.Offsetof(...) — compute field offset for amd64
            if (expr is CallExpressionSyntax offsetCall
                && offsetCall.Function is SelectorExpressionSyntax offsetSel
                && offsetSel.Expression is IdentifierNameSyntax offsetPkg
                && offsetPkg.Identifier.Text == "unsafe"
                && offsetSel.Name.Text == "Offsetof"
                && offsetCall.Arguments.Count == 1
                && offsetCall.Arguments[0] is SelectorExpressionSyntax fieldSel)
            {
                var fieldName = fieldSel.Name.Text;
                TypeSymbol? structType = null;

                if (fieldSel.Expression is CompositeLiteralSyntax offsetCompLit && offsetCompLit.Type != null)
                {
                    structType = ResolveType(offsetCompLit.Type);
                }
                else if (fieldSel.Expression is ParenthesizedExpressionSyntax offsetParenExpr)
                {
                    var innerType = ResolveType(offsetParenExpr.Expression);
                    if (innerType is PointerTypeSymbol ptrOff)
                    {
                        structType = ptrOff.ElementType;
                    }
                }

                if (structType != null)
                {
                    var resolved = structType.Resolved();
                    if (resolved is StructTypeSymbol st)
                    {
                        int offset = 0;
                        for (int fi = 0; fi < st.Fields.Count; fi++)
                        {
                            var fieldSize = ComputeSizeOf(st.Fields[fi].Type) ?? 8;
                            var align = fieldSize > 8 ? 8 : fieldSize;
                            if (align > 0 && offset % align != 0)
                            {
                                offset += align - (offset % align);
                            }
                            if (st.Fields[fi].Name == fieldName)
                            {
                                return offset;
                            }
                            offset += fieldSize;
                        }
                    }
                }
                return 8;
            }

            return null;
        }

        /// <summary>
        /// Compute the size of a type for amd64 (8-byte word, 8-byte alignment).
        /// Returns null if the size cannot be determined.
        /// </summary>
        private int? ComputeSizeOf(TypeSymbol type)
        {
            type = type.Resolved() ?? type;

            switch (type.TypeKind)
            {
                case TypeKind.Bool:
                case TypeKind.Int8:
                case TypeKind.Uint8:
                    return 1;
                case TypeKind.Int16:
                case TypeKind.Uint16:
                    return 2;
                case TypeKind.Int32:
                case TypeKind.Uint32:
                case TypeKind.Float32:
                    return 4;
                case TypeKind.Int64:
                case TypeKind.Uint64:
                case TypeKind.Float64:
                case TypeKind.Complex64:
                case TypeKind.Int:
                case TypeKind.Uint:
                case TypeKind.Uintptr:
                case TypeKind.Pointer:
                case TypeKind.String:     // string header: ptr + len = 16, but Sizeof(string) = 16
                case TypeKind.Channel:
                case TypeKind.Function:
                    return 8;
                case TypeKind.Complex128:
                    return 16;
            }

            // String is 16 bytes (ptr + len)
            if (type.TypeKind == TypeKind.String)
                return 16;

            // Slice: ptr + len + cap = 24
            if (type is SliceTypeSymbol)
                return 24;

            // Pointer: 8
            if (type is PointerTypeSymbol)
                return 8;

            // Map: 8 (pointer)
            if (type is MapTypeSymbol)
                return 8;

            // Interface: 16 (type ptr + data ptr)
            if (type is InterfaceTypeSymbol)
                return 16;

            // Function type: 8 (pointer)
            if (type is FunctionTypeSymbol)
                return 8;

            // Array: element size * length
            if (type is ArrayTypeSymbol arr)
            {
                var elemSize = ComputeSizeOf(arr.ElementType);
                if (elemSize.HasValue)
                    return elemSize.Value * arr.Length;
            }

            // Named type: unwrap to underlying
            if (type.UnderlyingType != null)
                return ComputeSizeOf(type.UnderlyingType);

            // Struct: sum of field sizes with alignment
            if (type is StructTypeSymbol st)
            {
                if (st.Fields.Count == 0) return null; // unknown — fields not yet populated
                int totalSize = 0;
                int maxAlign = 1;
                for (int i = 0; i < st.Fields.Count; i++)
                {
                    var fieldSize = ComputeSizeOf(st.Fields[i].Type);
                    if (!fieldSize.HasValue) return null;

                    var fieldAlign = ComputeAlignOf(st.Fields[i].Type);
                    if (!fieldAlign.HasValue) return null;

                    // Align field offset
                    int padding = (fieldAlign.Value - (totalSize % fieldAlign.Value)) % fieldAlign.Value;
                    totalSize += padding + fieldSize.Value;
                    if (fieldAlign.Value > maxAlign) maxAlign = fieldAlign.Value;
                }
                // Final padding to align struct to its largest field
                int finalPadding = (maxAlign - (totalSize % maxAlign)) % maxAlign;
                return totalSize + finalPadding;
            }

            return null;
        }

        private int? ComputeAlignOf(TypeSymbol type)
        {
            type = type.Resolved() ?? type;
            switch (type.TypeKind)
            {
                case TypeKind.Bool:
                case TypeKind.Int8:
                case TypeKind.Uint8:
                    return 1;
                case TypeKind.Int16:
                case TypeKind.Uint16:
                    return 2;
                case TypeKind.Int32:
                case TypeKind.Uint32:
                case TypeKind.Float32:
                    return 4;
                default:
                    return 8;
            }
        }

        private int? TryLookupConstFromSyntax(string name)
        {
            if (_context.PendingConstInts.TryGetValue(name, out var val))
            {
                // Clamp large values to int range for array lengths
                if (val > int.MaxValue) return int.MaxValue;
                if (val < int.MinValue) return int.MinValue;
                return (int)val;
            }
            return null;
        }
    }
}

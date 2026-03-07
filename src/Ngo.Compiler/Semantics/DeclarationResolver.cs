// -----------------------------------------------------------------------
// <copyright file="DeclarationResolver.cs" company="Ziad">
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
    public sealed class DeclarationResolver
    {
        private readonly AnalysisContext _context;
        private readonly TypeResolver _typeResolver;
        private readonly ExpressionResolver _expressionResolver;
        private readonly StatementResolver _statementResolver;
        private readonly List<FunctionSymbol> _initSymbols = new List<FunctionSymbol>();

        public DeclarationResolver(AnalysisContext context, TypeResolver typeResolver,
            ExpressionResolver expressionResolver, StatementResolver statementResolver)
        {
            _context = context;
            _typeResolver = typeResolver;
            _expressionResolver = expressionResolver;
            _statementResolver = statementResolver;
        }

        public SourceFile ResolveSourceFile(SourceFileSyntax syntax)
        {
            return ResolveSourceFiles(new[] { syntax });
        }

        public SourceFile ResolveSourceFiles(IReadOnlyList<SourceFileSyntax> files)
        {
            var packageDecl = ResolvePackageClause(files[0].PackageClause);

            // Validate all files declare the same package
            for (int i = 1; i < files.Count; i++)
            {
                if (files[i].PackageClause.Name.Text != files[0].PackageClause.Name.Text)
                {
                    _context.Errors.ReportError(_context.SpanOf(files[i].PackageClause),
                        ErrorCode.TypeMismatch,
                        $"Found package '{files[i].PackageClause.Name.Text}' but expected '{files[0].PackageClause.Name.Text}'");
                }
            }

            // Push package scope
            _context.PushScope("package");

            // Process imports from all files
            var imports = new List<ImportDeclaration>();
            foreach (var file in files)
            {
                imports.AddRange(ResolveImports(file.Imports));
            }

            // Pass 1: register all type, function, and method signatures from all files
            var functionSyntaxes = new List<FunctionDeclarationSyntax>();
            var methodSyntaxes = new List<MethodDeclarationSyntax>();
            var varSyntaxes = new List<VarDeclarationSyntax>();
            var typeSyntaxes = new List<TypeDeclarationSyntax>();
            var constSyntaxes = new List<ConstDeclarationSyntax>();

            foreach (var file in files)
            {
                foreach (var member in file.Members)
                {
                    if (member is FunctionDeclarationSyntax funcSyntax)
                    {
                        functionSyntaxes.Add(funcSyntax);
                    }
                    else if (member is MethodDeclarationSyntax methodSyntax)
                    {
                        methodSyntaxes.Add(methodSyntax);
                    }
                    else if (member is VarDeclarationSyntax varSyntax)
                    {
                        varSyntaxes.Add(varSyntax);
                    }
                    else if (member is TypeDeclarationSyntax typeSyntax)
                    {
                        typeSyntaxes.Add(typeSyntax);
                    }
                    else if (member is ConstDeclarationSyntax constSyntax)
                    {
                        constSyntaxes.Add(constSyntax);
                    }
                }
            }

            // Pre-scan constants for simple integer values (needed for array lengths in types)
            PreScanConstInts(constSyntaxes);

            // Pass 1a: pre-declare all type names as placeholders
            foreach (var typeSyntax in typeSyntaxes)
            {
                foreach (var spec in typeSyntax.Specs)
                {
                    PreDeclareType(spec);
                }
            }

            // Pass 1b: resolve type underlying types and fill in struct/interface details
            foreach (var typeSyntax in typeSyntaxes)
            {
                foreach (var spec in typeSyntax.Specs)
                {
                    RegisterTypeDeclaration(spec);
                }
            }

            // Register functions after types so function signatures can reference user types
            foreach (var funcSyntax in functionSyntaxes)
            {
                RegisterFunction(funcSyntax);
            }

            // Register methods after types so receiver types are resolved
            foreach (var methodSyntax in methodSyntaxes)
            {
                RegisterMethod(methodSyntax);
            }

            // Pass 2: bind type bodies, constants, var declarations, function bodies, method bodies
            var types = new List<TypeDeclaration>();
            foreach (var typeSyntax in typeSyntaxes)
            {
                foreach (var spec in typeSyntax.Specs)
                {
                    types.Add(ResolveTypeDeclaration(spec));
                }
            }

            // Post-process: upgrade named types based on structs to StructTypeSymbol
            // (must happen after all struct fields are populated)
            UpgradeStructBasedTypes(typeSyntaxes);

            var constants = new List<ConstDeclaration>();
            foreach (var constSyntax in constSyntaxes)
            {
                constants.AddRange(ResolveConstDeclaration(constSyntax));
            }

            // Pre-declare package-level variable names so cross-file references resolve
            foreach (var varSyntax in varSyntaxes)
            {
                foreach (var spec in varSyntax.Specs)
                {
                    PreDeclareVarSpec(spec);
                }
            }

            var variables = new List<VarDeclaration>();
            foreach (var varSyntax in varSyntaxes)
            {
                foreach (var spec in varSyntax.Specs)
                {
                    variables.AddRange(ResolveVarSpec(spec));
                }
            }

            var functions = new List<FunctionDeclaration>();
            foreach (var funcSyntax in functionSyntaxes)
            {
                functions.Add(ResolveFunctionDeclaration(funcSyntax));
            }

            var methods = new List<MethodDeclaration>();
            foreach (var methodSyntax in methodSyntaxes)
            {
                methods.Add(ResolveMethodDeclaration(methodSyntax));
            }

            ReportUnusedImports(imports);
            _context.PopScope(); // package

            return new SourceFile(packageDecl, imports, functions, methods, variables, types, constants,
                _context.SpanOf(files[0]));
        }

        private PackageDeclaration ResolvePackageClause(PackageClauseSyntax syntax)
        {
            var symbol = new PackageSymbol(syntax.Name.Text);
            return new PackageDeclaration(symbol, _context.SpanOf(syntax));
        }

        private List<ImportDeclaration> ResolveImports(IReadOnlyList<ImportDeclarationSyntax> importDecls)
        {
            var imports = new List<ImportDeclaration>();

            foreach (var importDecl in importDecls)
            {
                foreach (var spec in importDecl.Specs)
                {
                    var path = spec.Path.Value as string ?? spec.Path.Text.Trim('"');
                    var span = _context.SpanOf(spec);

                    // Determine the local name for this import
                    string? alias = null;
                    string localName;
                    if (spec.Alias != null)
                    {
                        alias = spec.Alias.Text;
                        if (alias == ".")
                        {
                            // Dot import: inject all exports into file scope
                            var dotPkg = PackageRegistry.Resolve(path);
                            if (dotPkg == null)
                            {
                                _context.Errors.ReportError(span, ErrorCode.UndeclaredName,
                                    $"Cannot find package '{path}'");
                                continue;
                            }

                            foreach (var export in dotPkg.Exports)
                            {
                                _context.Scope.TryDeclare(export.Value);
                            }

                            // Mark as used immediately — dot imports are always "used"
                            _context.UsedPackages.Add(dotPkg.Name);
                            imports.Add(new ImportDeclaration(dotPkg, path, alias, span));
                            continue;
                        }

                        if (alias == "_")
                        {
                            // Blank import: side-effects only, nothing to register
                            continue;
                        }

                        localName = alias;
                    }
                    else
                    {
                        localName = PackageRegistry.GetDefaultName(path);
                    }

                    // Resolve the package
                    var pkg = PackageRegistry.Resolve(path);
                    if (pkg == null)
                    {
                        _context.Errors.ReportError(span, ErrorCode.UndeclaredName,
                            $"Cannot find package '{path}'");
                        continue;
                    }

                    // If the alias changes the local name, create a new symbol with the alias
                    if (alias != null && alias != pkg.Name)
                    {
                        var aliased = new PackageSymbol(alias, path);
                        foreach (var export in pkg.Exports)
                        {
                            aliased.AddExport(export.Value);
                        }

                        pkg = aliased;
                    }

                    // Register the package in the current scope
                    if (!_context.Scope.TryDeclare(pkg))
                    {
                        // In multi-file packages, the same package may be imported
                        // in multiple files — this is valid in Go
                        var existing = _context.Scope.Lookup(pkg.Name);
                        if (existing is PackageSymbol existingPkg)
                        {
                            // Different packages with same local name (e.g. crypto/rand vs math/rand):
                            // merge exports so both files' usages resolve
                            if (existingPkg.ImportPath != pkg.ImportPath)
                            {
                                foreach (var export in pkg.Exports)
                                {
                                    if (!existingPkg.Exports.ContainsKey(export.Key))
                                    {
                                        existingPkg.AddExport(export.Value);
                                    }
                                }
                            }
                        }
                        else
                        {
                            _context.Errors.ReportError(span, ErrorCode.AlreadyDeclared,
                                $"'{pkg.Name}' is already declared in this scope");
                        }
                    }

                    imports.Add(new ImportDeclaration(pkg, path, alias, span));
                }
            }

            return imports;
        }

        private void RegisterFunction(FunctionDeclarationSyntax syntax)
        {
            // Resolve type parameters if present
            IReadOnlyList<TypeParameterSymbol> typeParams;
            if (syntax.TypeParameters != null)
            {
                typeParams = ResolveTypeParameterList(syntax.TypeParameters);
                // Push type params into scope so parameter/return types can reference them
                _context.PushScope("typeParams");
                foreach (var tp in typeParams)
                    _context.Scope.TryDeclare(tp);
            }
            else
            {
                typeParams = Array.Empty<TypeParameterSymbol>();
            }

            var parameters = _typeResolver.ResolveParameterList(syntax.Parameters);
            var returnTypes = _typeResolver.ResolveResultTypes(syntax.Result);

            if (syntax.TypeParameters != null)
                _context.PopScope();

            // Detect variadic: last parameter in syntax has an ellipsis
            bool isVariadic = false;
            if (syntax.Parameters.Parameters.Count > 0)
            {
                var lastParam = syntax.Parameters.Parameters[syntax.Parameters.Parameters.Count - 1];
                isVariadic = lastParam.Ellipsis != null;
            }

            var symbol = new FunctionSymbol(syntax.Name.Text, typeParams, parameters, returnTypes, isVariadic);

            // Go allows multiple init() functions — don't register in scope,
            // track separately instead
            if (syntax.Name.Text == "init")
            {
                _initSymbols.Add(symbol);
            }
            else if (!_context.Scope.TryDeclare(symbol))
            {
                // In multi-file packages, build tags may cause multiple files
                // to define the same function. Since we analyze all files,
                // silently allow function redeclaration at package scope.
            }
        }

        private FunctionDeclaration ResolveFunctionDeclaration(FunctionDeclarationSyntax syntax)
        {
            FunctionSymbol symbol;
            if (syntax.Name.Text == "init")
            {
                // Pop the first init symbol from the queue (order matches registration)
                symbol = _initSymbols[0];
                _initSymbols.RemoveAt(0);
            }
            else
            {
                symbol = (FunctionSymbol)_context.Scope.Lookup(syntax.Name.Text)!;
            }

            // Push function scope and register parameters
            _context.PushScope("function");
            var previousReturnTypes = _context.CurrentReturnTypes;
            var previousNamedReturns = _context.CurrentNamedReturns;
            _context.CurrentReturnTypes = symbol.ReturnTypes;

            // Register type parameters in function scope
            foreach (var tp in symbol.TypeParameters)
            {
                _context.Scope.TryDeclare(tp);
            }

            foreach (var param in symbol.Parameters)
            {
                _context.Scope.TryDeclare(param);
            }

            // Declare named return variables in the function scope
            var namedReturns = ResolveNamedReturns(syntax.Result, symbol.ReturnTypes);
            _context.CurrentNamedReturns = namedReturns;
            foreach (var nr in namedReturns)
            {
                _context.Scope.TryDeclare(nr);
            }

            // External function declarations (no body) — skip body resolution
            if (syntax.Body == null)
            {
                _context.PopScope();
                _context.CurrentReturnTypes = Array.Empty<TypeSymbol>();
                _context.CurrentNamedReturns = Array.Empty<LocalSymbol>();
                return new FunctionDeclaration(symbol, new BlockStatement(
                    new List<AstNode>(), _context.SpanOf(syntax)), _context.SpanOf(syntax));
            }

            var body = _statementResolver.ResolveBlock(syntax.Body);

            if (symbol.ReturnTypes.Count > 0 && !FlowAnalyzer.AllPathsReturn(body))
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.MissingReturn,
                    $"Function '{symbol.Name}' missing return at end of function");
            }

            GotoValidator.Validate(body, _context.Errors);
            ReportUnusedLocals();

            _context.CurrentReturnTypes = previousReturnTypes;
            _context.CurrentNamedReturns = previousNamedReturns;
            _context.PopScope(); // function

            return new FunctionDeclaration(symbol, body, namedReturns, _context.SpanOf(syntax));
        }

        private void RegisterMethod(MethodDeclarationSyntax syntax)
        {
            if (syntax.Receiver.Parameters.Count == 0)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.InvalidMethodReceiver,
                    "Method receiver must have a parameter");
                return;
            }

            var receiverParam = syntax.Receiver.Parameters[0];
            var receiverTypeExpr = receiverParam.Type;

            bool isPointerReceiver = receiverTypeExpr is PointerTypeSyntax;
            var baseTypeExpr = isPointerReceiver
                ? ((PointerTypeSyntax)receiverTypeExpr!).ElementType
                : receiverTypeExpr;

            var (baseType, receiverTypeParams) = ResolveReceiverType(baseTypeExpr!);
            if (baseType == null)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.InvalidMethodReceiver,
                    "Undefined receiver type");
                return;
            }

            // Push generic type params into scope for parameter/return type resolution
            bool pushedScope = receiverTypeParams != null;
            if (pushedScope)
            {
                _context.PushScope("methodTypeParams");
                foreach (var tp in receiverTypeParams!)
                    _context.Scope.TryDeclare(tp);
            }

            var parameters = _typeResolver.ResolveParameterList(syntax.Parameters);
            var returnTypes = _typeResolver.ResolveResultTypes(syntax.Result);

            if (pushedScope)
                _context.PopScope();

            bool isVariadic = false;
            if (syntax.Parameters.Parameters.Count > 0)
            {
                var lastParam = syntax.Parameters.Parameters[syntax.Parameters.Parameters.Count - 1];
                isVariadic = lastParam.Ellipsis != null;
            }

            var method = new MethodSymbol(syntax.Name.Text, baseType, isPointerReceiver,
                Array.Empty<TypeParameterSymbol>(), parameters, returnTypes, isVariadic);

            var existing = baseType.LookupMethod(syntax.Name.Text);
            if (existing != null)
            {
                // Build tags may cause duplicate method declarations across files.
                // Silently skip the duplicate.
                return;
            }

            baseType.AddMethod(method);
        }

        private MethodDeclaration ResolveMethodDeclaration(MethodDeclarationSyntax syntax)
        {
            if (syntax.Receiver.Parameters.Count == 0)
            {
                var errorMethod = new MethodSymbol(syntax.Name.Text, TypeSymbol.Error, false,
                    Array.Empty<ParameterSymbol>(), Array.Empty<TypeSymbol>());
                var errorReceiver = new ParameterSymbol("_", TypeSymbol.Error, 0);
                return new MethodDeclaration(errorMethod, errorReceiver,
                    new BlockStatement(Array.Empty<Statement>(), _context.SpanOf(syntax)),
                    _context.SpanOf(syntax));
            }

            var receiverParam = syntax.Receiver.Parameters[0];
            var receiverTypeExpr = receiverParam.Type;

            bool isPointerReceiver = receiverTypeExpr is PointerTypeSyntax;
            var baseTypeExpr = isPointerReceiver
                ? ((PointerTypeSyntax)receiverTypeExpr!).ElementType
                : receiverTypeExpr;

            var (baseType, receiverTypeParams) = ResolveReceiverType(baseTypeExpr!);
            if (baseType == null)
            {
                var errorMethod = new MethodSymbol(syntax.Name.Text, TypeSymbol.Error, false,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Void);
                var errorReceiver = new ParameterSymbol("_", TypeSymbol.Error, 0);
                return new MethodDeclaration(errorMethod, errorReceiver,
                    new BlockStatement(new List<AstNode>(), _context.SpanOf(syntax)), _context.SpanOf(syntax));
            }

            var method = baseType.LookupMethod(syntax.Name.Text);
            if (method == null)
            {
                var errorMethod = new MethodSymbol(syntax.Name.Text, baseType, isPointerReceiver,
                    Array.Empty<ParameterSymbol>(), BuiltinTypes.Void);
                var errorReceiver = new ParameterSymbol("_", TypeSymbol.Error, 0);
                return new MethodDeclaration(errorMethod, errorReceiver,
                    new BlockStatement(new List<AstNode>(), _context.SpanOf(syntax)), _context.SpanOf(syntax));
            }

            _context.PushScope("method");
            var previousReturnTypes = _context.CurrentReturnTypes;
            var previousNamedReturns = _context.CurrentNamedReturns;
            _context.CurrentReturnTypes = method.ReturnTypes;

            // Push generic type parameters into method scope for body resolution
            if (receiverTypeParams != null)
            {
                foreach (var tp in receiverTypeParams)
                    _context.Scope.TryDeclare(tp);
            }

            var receiverName = GetReceiverName(receiverParam);
            var receiverType = isPointerReceiver ? new PointerTypeSymbol(baseType) : (TypeSymbol)baseType;
            var receiverSymbol = new ParameterSymbol(receiverName, receiverType, 0);
            _context.Scope.TryDeclare(receiverSymbol);

            foreach (var param in method.Parameters)
            {
                _context.Scope.TryDeclare(param);
            }

            // Declare named return variables in the method scope
            var namedReturns = ResolveNamedReturns(syntax.Result, method.ReturnTypes);
            _context.CurrentNamedReturns = namedReturns;
            foreach (var nr in namedReturns)
            {
                _context.Scope.TryDeclare(nr);
            }

            // External method declarations (no body) — skip body resolution
            if (syntax.Body == null)
            {
                _context.PopScope();
                _context.CurrentReturnTypes = Array.Empty<TypeSymbol>();
                _context.CurrentNamedReturns = Array.Empty<LocalSymbol>();
                return new MethodDeclaration(method, receiverSymbol, new BlockStatement(
                    new List<AstNode>(), _context.SpanOf(syntax)), _context.SpanOf(syntax));
            }

            var body = _statementResolver.ResolveBlock(syntax.Body);

            if (method.ReturnTypes.Count > 0 && !FlowAnalyzer.AllPathsReturn(body))
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.MissingReturn,
                    $"Method '{method.Name}' missing return at end of function");
            }

            GotoValidator.Validate(body, _context.Errors);
            ReportUnusedLocals();

            _context.CurrentReturnTypes = previousReturnTypes;
            _context.CurrentNamedReturns = previousNamedReturns;
            _context.PopScope();

            return new MethodDeclaration(method, receiverSymbol, body, namedReturns, _context.SpanOf(syntax));
        }

        private IReadOnlyList<LocalSymbol> ResolveNamedReturns(
            SyntaxNode? result, IReadOnlyList<TypeSymbol> returnTypes)
        {
            if (result is ParameterListSyntax paramList && paramList.Parameters.Count > 0)
            {
                var namedReturns = new List<LocalSymbol>();
                int typeIndex = 0;

                for (int i = 0; i < paramList.Parameters.Count; i++)
                {
                    var param = paramList.Parameters[i];
                    if (param.Names.HasValue)
                    {
                        for (int j = 0; j < param.Names.Value.Count; j++)
                        {
                            var name = param.Names.Value[j].Text;
                            if (name != "_" && typeIndex < returnTypes.Count)
                            {
                                namedReturns.Add(new LocalSymbol(name, returnTypes[typeIndex]));
                            }

                            typeIndex++;
                        }
                    }
                    else
                    {
                        typeIndex++;
                    }
                }

                return namedReturns;
            }

            return Array.Empty<LocalSymbol>();
        }

        private string GetReceiverName(ParameterSyntax syntax)
        {
            if (syntax.Names.HasValue && syntax.Names.Value.Count > 0)
                return syntax.Names.Value[0].Text;
            return "_";
        }

        private void PreScanConstInts(List<ConstDeclarationSyntax> constSyntaxes)
        {
            foreach (var constDecl in constSyntaxes)
            {
                foreach (var spec in constDecl.Specs)
                {
                    if (!spec.Values.HasValue) continue;
                    for (int i = 0; i < spec.Names.Count && i < spec.Values.Value.Count; i++)
                    {
                        var name = spec.Names[i].Text;
                        var valExpr = spec.Values.Value[i];
                        var constVal = TryEvalConstInt(valExpr);
                        if (constVal.HasValue)
                        {
                            _context.PendingConstInts[name] = constVal.Value;
                        }
                    }
                }
            }
        }

        private int? TryEvalConstInt(ExpressionSyntax expr)
        {
            if (expr is LiteralExpressionSyntax lit
                && lit.Token.Kind == SyntaxKind.IntLiteralToken
                && int.TryParse(lit.Token.Text, out var val))
            {
                return val;
            }

            if (expr is IdentifierNameSyntax id
                && _context.PendingConstInts.TryGetValue(id.Identifier.Text, out var idVal))
            {
                return idVal;
            }

            if (expr is BinaryExpressionSyntax bin)
            {
                var left = TryEvalConstInt(bin.Left);
                var right = TryEvalConstInt(bin.Right);
                if (left.HasValue && right.HasValue)
                {
                    return bin.OperatorToken.Kind switch
                    {
                        SyntaxKind.PlusToken => left.Value + right.Value,
                        SyntaxKind.MinusToken => left.Value - right.Value,
                        SyntaxKind.StarToken => left.Value * right.Value,
                        SyntaxKind.SlashToken when right.Value != 0 => left.Value / right.Value,
                        SyntaxKind.PercentToken when right.Value != 0 => left.Value % right.Value,
                        SyntaxKind.LessThanLessThanToken => left.Value << right.Value,
                        SyntaxKind.GreaterThanGreaterThanToken => left.Value >> right.Value,
                        SyntaxKind.AmpersandToken => left.Value & right.Value,
                        SyntaxKind.PipeToken => left.Value | right.Value,
                        SyntaxKind.CaretToken => left.Value ^ right.Value,
                        _ => (int?)null,
                    };
                }
            }

            if (expr is UnaryExpressionSyntax unary
                && unary.OperatorToken.Kind == SyntaxKind.MinusToken)
            {
                var inner = TryEvalConstInt(unary.Operand);
                if (inner.HasValue) return -inner.Value;
            }

            return null;
        }

        private void PreDeclareType(TypeSpecSyntax syntax)
        {
            var name = syntax.Name.Text;

            // Type alias — handled fully in RegisterTypeDeclaration
            if (syntax.AssignToken != null)
                return;

            // Struct and interface types get concrete symbols
            if (syntax.Type is StructTypeSyntax)
            {
                IReadOnlyList<TypeParameterSymbol>? typeParams = null;
                if (syntax.TypeParameters != null)
                    typeParams = ResolveTypeParameterList(syntax.TypeParameters);

                var structType = new StructTypeSymbol(name, new List<FieldSymbol>());
                if (typeParams != null)
                    structType.SetTypeParameters(typeParams);
                _context.Scope.TryDeclare(structType);
            }
            else if (syntax.Type is InterfaceTypeSyntax)
            {
                IReadOnlyList<TypeParameterSymbol>? typeParams = null;
                if (syntax.TypeParameters != null)
                    typeParams = ResolveTypeParameterList(syntax.TypeParameters);

                var ifaceType = new InterfaceTypeSymbol(name, new List<MethodSymbol>());
                if (typeParams != null)
                    ifaceType.SetTypeParameters(typeParams);
                _context.Scope.TryDeclare(ifaceType);
            }
            else
            {
                // Named non-struct type: declare as placeholder
                // Will be resolved fully in RegisterTypeDeclaration
                var placeholder = new TypeSymbol(name, TypeKind.Error, null);
                if (syntax.TypeParameters != null)
                {
                    var typeParams = ResolveTypeParameterList(syntax.TypeParameters);
                    placeholder.SetTypeParameters(typeParams);
                }
                _context.Scope.TryDeclare(placeholder);
            }
        }

        private void RegisterTypeDeclaration(TypeSpecSyntax syntax)
        {
            var name = syntax.Name.Text;

            // For type aliases (type Foo = Bar), we resolve the underlying type later
            // For now, create a placeholder type symbol with the name
            if (syntax.AssignToken != null)
            {
                // Type alias: resolve immediately since it just refers to an existing type
                var underlying = _typeResolver.ResolveType(syntax.Type);
                if (underlying == null)
                {
                    underlying = TypeSymbol.Error;
                }

                var alias = new TypeSymbol(name, underlying.TypeKind, underlying);
                _context.Scope.TryDeclare(alias);

                return;
            }

            // Struct and interface are already pre-declared in PreDeclareType — skip
            if (syntax.Type is StructTypeSyntax || syntax.Type is InterfaceTypeSyntax)
            {
                return;
            }

            // Non-struct type definition (e.g., type MyInt int, type Lexer Tokenizer)
            // The placeholder was already declared in PreDeclareType — now resolve underlying
            var existingPlaceholder = _context.Scope.Lookup(name) as TypeSymbol;
            bool pushedTypeParamScope = false;
            if (existingPlaceholder != null && existingPlaceholder.IsGeneric)
            {
                _context.PushScope("typeParams");
                foreach (var tp in existingPlaceholder.TypeParameters)
                    _context.Scope.TryDeclare(tp);
                pushedTypeParamScope = true;
            }

            var resolvedUnderlying = _typeResolver.ResolveType(syntax.Type);
            if (resolvedUnderlying == null)
            {
                resolvedUnderlying = TypeSymbol.Error;
            }

            if (pushedTypeParamScope)
                _context.PopScope();

            // Update the placeholder in place so existing references see the change
            var existing = existingPlaceholder;
            if (existing != null && existing.TypeKind == TypeKind.Error)
            {
                existing.TypeKind = resolvedUnderlying.TypeKind;
                existing.UnderlyingType = resolvedUnderlying;
            }
            else
            {
                _context.Scope.Replace(name, new TypeSymbol(name, resolvedUnderlying.TypeKind, resolvedUnderlying));
            }
        }

        private void UpgradeStructBasedTypes(List<TypeDeclarationSyntax> typeSyntaxes)
        {
            foreach (var typeDecl in typeSyntaxes)
            {
                foreach (var spec in typeDecl.Specs)
                {
                    if (spec.AssignToken != null) continue; // Skip aliases
                    if (spec.Type is StructTypeSyntax || spec.Type is InterfaceTypeSyntax) continue;

                    var name = spec.Name.Text;
                    var symbol = _context.Scope.Lookup(name) as TypeSymbol;
                    if (symbol == null || symbol is StructTypeSymbol) continue;

                    // If underlying type is a struct, upgrade to StructTypeSymbol
                    if (symbol.UnderlyingType is StructTypeSymbol baseStruct && baseStruct.Fields.Count > 0)
                    {
                        var newStruct = new StructTypeSymbol(name, baseStruct.Fields, baseStruct);
                        // Copy methods from the old symbol to the new one
                        foreach (var m in symbol.Methods)
                            newStruct.AddMethod(m);
                        _context.Scope.Replace(name, newStruct);
                    }
                }
            }
        }

        private TypeDeclaration ResolveTypeDeclaration(TypeSpecSyntax syntax)
        {
            var name = syntax.Name.Text;
            var symbol = _context.Scope.Lookup(name) as TypeSymbol;

            if (symbol is StructTypeSymbol structSymbol && syntax.Type is StructTypeSyntax structSyntax)
            {
                // Push type params into scope for field resolution
                if (structSymbol.IsGeneric)
                {
                    _context.PushScope("typeParams");
                    foreach (var tp in structSymbol.TypeParameters)
                        _context.Scope.TryDeclare(tp);
                }

                // Populate struct fields
                var fields = new List<FieldSymbol>();
                int ordinal = 0;

                foreach (var fieldSyntax in structSyntax.Fields)
                {
                    var fieldType = _typeResolver.ResolveType(fieldSyntax.Type);
                    if (fieldType == null)
                    {
                        fieldType = TypeSymbol.Error;
                    }

                    string? tagValue = ExtractTagString(fieldSyntax.Tag);

                    if (fieldSyntax.Names.HasValue)
                    {
                        for (int i = 0; i < fieldSyntax.Names.Value.Count; i++)
                        {
                            var fieldName = fieldSyntax.Names.Value[i].Text;
                            fields.Add(new FieldSymbol(fieldName, fieldType, ordinal++,
                                tag: tagValue));
                        }
                    }
                    else
                    {
                        // Embedded field: use the base type name as the field name
                        // For *T, the embedded name is T (not *T)
                        var embeddedName = fieldType is PointerTypeSymbol embPtr
                            ? embPtr.ElementType.Name
                            : fieldType.Name;
                        fields.Add(new FieldSymbol(embeddedName, fieldType, ordinal++,
                            isEmbedded: true, tag: tagValue));
                    }
                }

                structSymbol.SetFields(fields);

                if (structSymbol.IsGeneric)
                    _context.PopScope();
            }

            if (symbol is InterfaceTypeSymbol ifaceSymbol && syntax.Type is InterfaceTypeSyntax ifaceSyntax)
            {
                var methods = new List<MethodSymbol>();

                foreach (var member in ifaceSyntax.Members)
                {
                    if (member is MethodSpecSyntax methodSpec)
                    {
                        var parameters = _typeResolver.ResolveParameterList(methodSpec.Parameters);
                        var returnTypes = _typeResolver.ResolveResultTypes(methodSpec.Result);

                        bool isVariadic = false;
                        if (methodSpec.Parameters.Parameters.Count > 0)
                        {
                            var lastParam = methodSpec.Parameters.Parameters[methodSpec.Parameters.Parameters.Count - 1];
                            isVariadic = lastParam.Ellipsis != null;
                        }

                        var method = new MethodSymbol(methodSpec.Name.Text, ifaceSymbol, false,
                            Array.Empty<TypeParameterSymbol>(), parameters, returnTypes, isVariadic);
                        methods.Add(method);
                    }
                    else if (member is ExpressionSyntax embeddedSyntax)
                    {
                        // Embedded interface: resolve the type and merge its methods
                        var embeddedType = _typeResolver.ResolveType(embeddedSyntax);
                        if (embeddedType is InterfaceTypeSymbol embeddedIface)
                        {
                            foreach (var m in embeddedIface.Methods)
                            {
                                // Re-parent the method to the current interface
                                var promoted = new MethodSymbol(m.Name, ifaceSymbol, false,
                                    Array.Empty<TypeParameterSymbol>(), m.Parameters, m.ReturnTypes, m.IsVariadic);
                                methods.Add(promoted);
                            }
                        }
                    }
                }

                ifaceSymbol.SetMethods(methods);
            }

            return new TypeDeclaration(symbol ?? TypeSymbol.Error, _context.SpanOf(syntax));
        }

        private void PreDeclareVarSpec(VarSpecSyntax syntax)
        {
            var declaredType = syntax.Type != null ? _typeResolver.ResolveType(syntax.Type) : null;
            for (int i = 0; i < syntax.Names.Count; i++)
            {
                var name = syntax.Names[i].Text;
                if (name == "_") continue;
                // Use declared type, or a placeholder that will be updated during full resolution
                var varType = declaredType ?? TypeSymbol.Error;
                var symbol = new LocalSymbol(name, varType);
                _context.Scope.TryDeclare(symbol);
            }
        }

        private IReadOnlyList<VarDeclaration> ResolveVarSpec(VarSpecSyntax syntax)
        {
            var results = new List<VarDeclaration>();
            var declaredType = syntax.Type != null ? _typeResolver.ResolveType(syntax.Type) : null;

            // Multi-return: var a, b = f() where f returns (T1, T2)
            if (syntax.Names.Count > 1 && syntax.Values.HasValue && syntax.Values.Value.Count == 1)
            {
                var rhs = _expressionResolver.ResolveExpression(syntax.Values.Value[0]);
                var returnTypes = _context.GetCallReturnTypes(rhs);
                if (returnTypes != null && returnTypes.Count == syntax.Names.Count)
                {
                    var symbols = new LocalSymbol?[syntax.Names.Count];
                    for (int i = 0; i < syntax.Names.Count; i++)
                    {
                        var name = syntax.Names[i].Text;
                        var varType = declaredType ?? TypeChecker.DefaultType(returnTypes[i]);

                        if (name == "_")
                        {
                            symbols[i] = null;
                            continue;
                        }

                        LocalSymbol symbol;
                        if (_context.Scope.Name == "package"
                            && _context.Scope.Lookup(name) is LocalSymbol existing)
                        {
                            existing.Type = varType;
                            symbol = existing;
                        }
                        else
                        {
                            symbol = new LocalSymbol(name, varType);
                            _context.Scope.TryDeclare(symbol);
                        }
                        _context.TrackLocal(symbol, _context.SpanOf(syntax));
                        symbols[i] = symbol;
                    }

                    // Return as MultiVarDeclaration wrapped in VarDeclarations
                    // The first gets the rhs, the rest reference the same tuple
                    for (int i = 0; i < symbols.Length; i++)
                    {
                        results.Add(new VarDeclaration(
                            symbols[i] ?? new LocalSymbol("_", returnTypes[i]),
                            i == 0 ? rhs : null, _context.SpanOf(syntax)));
                    }

                    return results;
                }
            }

            for (int i = 0; i < syntax.Names.Count; i++)
            {
                var name = syntax.Names[i].Text;
                Expression? initializer = null;
                TypeSymbol varType;

                if (syntax.Values.HasValue && i < syntax.Values.Value.Count)
                {
                    initializer = _expressionResolver.ResolveExpression(syntax.Values.Value[i]);

                    if (declaredType != null)
                    {
                        varType = declaredType;
                        if (!TypeChecker.IsAssignable(initializer.Type, declaredType))
                        {
                            _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.TypeMismatch,
                                $"Cannot assign '{initializer.Type.Name}' to '{declaredType.Name}'");
                        }
                    }
                    else
                    {
                        // Infer type from initializer
                        varType = TypeChecker.DefaultType(initializer.Type);
                    }
                }
                else if (declaredType != null)
                {
                    varType = declaredType;
                }
                else
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.TypeMismatch,
                        $"Variable '{name}' requires a type or initializer");
                    varType = TypeSymbol.Error;
                }

                LocalSymbol symbol;
                // At package level, update the pre-declared placeholder if it exists
                if (_context.Scope.Name == "package" && name != "_"
                    && _context.Scope.Lookup(name) is LocalSymbol existing)
                {
                    existing.Type = varType;
                    symbol = existing;
                }
                else
                {
                    symbol = new LocalSymbol(name, varType);
                    if (name != "_" && !_context.Scope.TryDeclare(symbol))
                    {
                        if (_context.Scope.Name != "package")
                        {
                            _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                                $"Variable '{name}' is already declared");
                        }
                    }
                }
                _context.TrackLocal(symbol, _context.SpanOf(syntax));

                results.Add(new VarDeclaration(symbol, initializer, _context.SpanOf(syntax)));
            }

            return results;
        }

        public AstNode ResolveVarDeclarationStatement(VarDeclarationSyntax syntax)
        {
            // For a var declaration inside a function, bind each spec and wrap
            // in a block if there are multiple declarations
            var allDecls = new List<VarDeclaration>();
            foreach (var spec in syntax.Specs)
            {
                allDecls.AddRange(ResolveVarSpec(spec));
            }

            if (allDecls.Count == 1)
            {
                return allDecls[0];
            }

            return new BlockStatement(new List<AstNode>(allDecls), _context.SpanOf(syntax));
        }

        public AstNode ResolveConstDeclarationStatement(ConstDeclarationSyntax syntax)
        {
            var decls = ResolveConstDeclaration(syntax);
            if (decls.Count == 1)
            {
                return decls[0];
            }

            return new BlockStatement(new List<AstNode>(decls), _context.SpanOf(syntax));
        }

        private IReadOnlyList<ConstDeclaration> ResolveConstDeclaration(ConstDeclarationSyntax syntax)
        {
            var results = new List<ConstDeclaration>();
            var previousIota = _context.IotaCounter;
            _context.IotaCounter = 0;

            // Pre-declare all constant names so forward references within the block resolve.
            // This allows const blocks like: const ( a = b + 1; b = 2 )
            foreach (var spec in syntax.Specs)
            {
                for (int i = 0; i < spec.Names.Count; i++)
                {
                    var name = spec.Names[i].Text;
                    if (name != "_")
                    {
                        _context.Scope.TryDeclare(new ConstantSymbol(name, TypeSymbol.Error, null));
                    }
                }
            }

            SeparatedSyntaxList<ExpressionSyntax>? prevValues = null;
            ExpressionSyntax? prevType = null;

            foreach (var spec in syntax.Specs)
            {
                results.AddRange(ResolveConstSpec(spec, ref prevValues, ref prevType));
                _context.IotaCounter++;
            }

            _context.IotaCounter = previousIota;
            return results;
        }

        private IReadOnlyList<ConstDeclaration> ResolveConstSpec(ConstSpecSyntax spec,
            ref SeparatedSyntaxList<ExpressionSyntax>? prevValues,
            ref ExpressionSyntax? prevType)
        {
            var results = new List<ConstDeclaration>();

            var values = spec.Values.HasValue ? spec.Values : prevValues;
            // Type only carries forward when values also carry forward (iota continuation).
            // When a spec provides its own values, only its own explicit type applies.
            var typeExpr = spec.Values.HasValue ? spec.Type : (spec.Type ?? prevType);

            if (spec.Values.HasValue)
            {
                prevValues = spec.Values;
                prevType = spec.Type;
            }

            TypeSymbol? declaredType = typeExpr != null ? _typeResolver.ResolveType(typeExpr) : null;

            for (int i = 0; i < spec.Names.Count; i++)
            {
                var name = spec.Names[i].Text;
                Expression? initializer = null;

                if (values.HasValue && i < values.Value.Count)
                {
                    initializer = _expressionResolver.ResolveExpression(values.Value[i]);
                }

                var constType = declaredType
                    ?? (initializer != null ? initializer.Type : BuiltinTypes.Int);

                object? constValue = _context.TryEvaluateConstant(initializer);
                var symbol = new ConstantSymbol(name, constType, constValue);

                if (name != "_")
                {
                    // Check if this was pre-declared (forward reference support)
                    var existing = _context.Scope.Lookup(name);
                    if (existing is ConstantSymbol existingConst && existingConst.Type == TypeSymbol.Error)
                    {
                        // Replace the placeholder with the resolved constant
                        _context.Scope.Replace(name, symbol);
                    }
                    else if (!_context.Scope.TryDeclare(symbol))
                    {
                        // At package level, tolerate duplicates (build-tag compatibility).
                        // Inside function bodies, report the error.
                        if (_context.Scope.Name != "package")
                        {
                            _context.Errors.ReportError(_context.SpanOf(spec), ErrorCode.AlreadyDeclared,
                                $"Constant '{name}' is already declared");
                        }
                    }
                }

                results.Add(new ConstDeclaration(symbol, initializer, _context.SpanOf(spec)));
            }

            return results;
        }

        private void ReportUnusedLocals()
        {
            if (!_context.CheckUnused) return;
            foreach (var (symbol, span) in _context.FunctionLocals)
            {
                if (!symbol.IsUsed)
                {
                    _context.Errors.ReportError(span, ErrorCode.UnusedVariable,
                        $"'{symbol.Name}' declared but not used");
                }
            }
            _context.FunctionLocals.Clear();
        }

        private (TypeSymbol? baseType, IReadOnlyList<TypeParameterSymbol>? typeParams) ResolveReceiverType(
            SyntaxNode baseTypeExpr)
        {
            // Handle generic receiver: func (q *Deque[T]) Method(...)
            // The receiver type Deque[T] is parsed as IndexExpressionSyntax
            if (baseTypeExpr is IndexExpressionSyntax indexSyntax &&
                indexSyntax.Expression is IdentifierNameSyntax baseId)
            {
                var baseSym = _context.Scope.Lookup(baseId.Identifier.Text) as TypeSymbol;
                if (baseSym != null && baseSym.IsGeneric)
                    return (baseSym, baseSym.TypeParameters);
            }

            // Handle multi-type-arg generic receiver: func (q *Map[K, V]) Method(...)
            if (baseTypeExpr is TypeArgumentListSyntax typeArgList &&
                typeArgList.Expression is IdentifierNameSyntax baseId2)
            {
                var baseSym = _context.Scope.Lookup(baseId2.Identifier.Text) as TypeSymbol;
                if (baseSym != null && baseSym.IsGeneric)
                    return (baseSym, baseSym.TypeParameters);
            }

            var resolved = _typeResolver.ResolveType((ExpressionSyntax)baseTypeExpr);
            return (resolved, null);
        }

        private IReadOnlyList<TypeParameterSymbol> ResolveTypeParameterList(
            TypeParameterListSyntax syntax)
        {
            var result = new List<TypeParameterSymbol>();
            int ordinal = 0;

            for (int i = 0; i < syntax.Parameters.Count; i++)
            {
                var decl = syntax.Parameters[i];
                var constraint = ResolveConstraint(decl.Constraint);

                for (int j = 0; j < decl.Names.Count; j++)
                {
                    var name = decl.Names[j].Text;
                    result.Add(new TypeParameterSymbol(name, ordinal++, constraint));
                }
            }

            return result;
        }

        private ConstraintInfo ResolveConstraint(ExpressionSyntax syntax)
        {
            // Handle identifier constraints: any, comparable, or named interface
            if (syntax is IdentifierNameSyntax idSyntax)
            {
                if (idSyntax.Identifier.Text == "any")
                    return ConstraintInfo.Any;
                if (idSyntax.Identifier.Text == "comparable")
                    return ConstraintInfo.Comparable;

                // Named interface constraint
                var symbol = _context.Scope.Lookup(idSyntax.Identifier.Text);
                if (symbol is InterfaceTypeSymbol iface)
                {
                    return new ConstraintInfo(iface.Name, iface.Methods,
                        Array.Empty<TypeElement>(), isComparable: false);
                }

                // Unknown constraint — treat as any
                return ConstraintInfo.Any;
            }

            // Handle inline interface constraint
            if (syntax is InterfaceTypeSyntax ifaceSyntax)
            {
                var methods = new List<MethodSymbol>();
                var typeElements = new List<TypeElement>();

                foreach (var member in ifaceSyntax.Members)
                {
                    if (member is MethodSpecSyntax methodSpec)
                    {
                        var parameters = _typeResolver.ResolveParameterList(methodSpec.Parameters);
                        var returnTypes = _typeResolver.ResolveResultTypes(methodSpec.Result);
                        methods.Add(new MethodSymbol(methodSpec.Name.Text, TypeSymbol.Error, false,
                            parameters, returnTypes));
                    }
                    else if (member is UnionTypeSyntax unionSyntax)
                    {
                        foreach (var term in unionSyntax.Terms)
                        {
                            var termType = _typeResolver.ResolveType((ExpressionSyntax)term.Type);
                            if (termType != null)
                                typeElements.Add(new TypeElement(termType, term.Tilde != null));
                        }
                    }
                }

                return new ConstraintInfo("interface", methods, typeElements, isComparable: false);
            }

            // Handle union type constraint
            if (syntax is UnionTypeSyntax unionConstraint)
            {
                var typeElements = new List<TypeElement>();
                foreach (var term in unionConstraint.Terms)
                {
                    var termType = _typeResolver.ResolveType(term.Type);
                    if (termType != null)
                        typeElements.Add(new TypeElement(termType, term.Tilde != null));
                }

                return new ConstraintInfo("union", Array.Empty<MethodSymbol>(),
                    typeElements, isComparable: false);
            }

            return ConstraintInfo.Any;
        }

        private void ReportUnusedImports(IReadOnlyList<ImportDeclaration> imports)
        {
            if (!_context.CheckUnused) return;
            foreach (var import in imports)
            {
                if (!_context.UsedPackages.Contains(import.Package.Name))
                {
                    _context.Errors.ReportError(import.Span, ErrorCode.UnusedImport,
                        $"'{import.Path}' imported and not used");
                }
            }
        }
        private static string? ExtractTagString(SyntaxToken? tag)
        {
            if (tag == null) return null;
            var text = tag.Text;
            if (text.Length >= 2)
            {
                // Raw string: `...` → strip backticks
                if (text[0] == '`')
                    return text.Substring(1, text.Length - 2);
                // Regular string: "..." → strip quotes
                if (text[0] == '"')
                    return text.Substring(1, text.Length - 2);
            }
            return text;
        }
    }
}

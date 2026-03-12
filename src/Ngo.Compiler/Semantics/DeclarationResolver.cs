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

            // Pre-scan var declarations for auto-sized array literals (needed for len(x) in array types)
            PreScanVarArrayLens(varSyntaxes);

            // Pass 1a: pre-declare all type names as placeholders
            foreach (var typeSyntax in typeSyntaxes)
            {
                foreach (var spec in typeSyntax.Specs)
                {
                    PreDeclareType(spec);
                }
            }

            // Pass 1a2: fixup generic type parameter constraints that referenced
            // forward-declared types (e.g., nistCurve[Point nistPoint[Point]] where
            // nistPoint is declared after nistCurve)
            FixupTypeParameterConstraints(typeSyntaxes);

            // Pass 1b: resolve type underlying types and fill in struct/interface details
            foreach (var typeSyntax in typeSyntaxes)
            {
                foreach (var spec in typeSyntax.Specs)
                {
                    RegisterTypeDeclaration(spec);
                }
            }

            // Pass 1c: fixup named types whose underlying was still a placeholder when registered
            // (e.g., type MetricBytes SI where SI wasn't yet resolved when MetricBytes was processed)
            foreach (var typeSyntax in typeSyntaxes)
            {
                foreach (var spec in typeSyntax.Specs)
                {
                    if (spec.AssignToken != null || spec.Type is StructTypeSyntax || spec.Type is InterfaceTypeSyntax)
                        continue;
                    var sym = _context.Scope.Lookup(spec.Name.Text) as TypeSymbol;
                    if (sym?.UnderlyingType != null && sym.GetType() == typeof(TypeSymbol))
                    {
                        var resolved = sym.UnderlyingType;
                        // Chase through named type chains to get the final TypeKind
                        while (resolved.GetType() == typeof(TypeSymbol) && resolved.UnderlyingType != null)
                            resolved = resolved.UnderlyingType;
                        if (sym.TypeKind != resolved.TypeKind && resolved.TypeKind != TypeKind.Error)
                            sym.TypeKind = resolved.TypeKind;
                    }
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

            // Post-process: fixup interfaces with embedded interfaces that had empty method sets
            // due to forward references (e.g., boolFlag embeds Value, but Value wasn't resolved yet)
            FixupEmbeddedInterfaces(typeSyntaxes);

            // Post-process: upgrade named types based on structs to StructTypeSymbol
            // (must happen after all struct fields are populated)
            UpgradeStructBasedTypes(typeSyntaxes);

            // Pre-declare all constant names across all const blocks so cross-file references resolve.
            // e.g., active_help.go references configEnvVarGlobalPrefix defined in completions.go
            foreach (var constSyntax in constSyntaxes)
            {
                foreach (var spec in constSyntax.Specs)
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
            }

            var constants = new List<ConstDeclaration>();
            foreach (var constSyntax in constSyntaxes)
            {
                constants.AddRange(ResolveConstDeclaration(constSyntax));
            }

            // Second pass: retry constants that still have Error type (cross-file forward refs).
            // e.g., doc.go: Enabled = available; notboring.go: available = false
            bool hasUnresolved = false;
            foreach (var c in constants)
            {
                if (c.Symbol.Type == TypeSymbol.Error)
                {
                    hasUnresolved = true;
                    break;
                }
            }
            if (hasUnresolved)
            {
                var retry = new List<ConstDeclaration>();
                foreach (var constSyntax in constSyntaxes)
                {
                    retry.AddRange(ResolveConstDeclaration(constSyntax));
                }
                constants = retry;
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
                            var dotPkg = _context.Compilation?.ResolvePackage(path);
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
                        localName = CompilationContext.GetDefaultPackageName(path);
                    }

                    // Resolve the package
                    var pkg = _context.Compilation?.ResolvePackage(path);
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
                var looked = _context.Scope.Lookup(syntax.Name.Text);
                if (looked is FunctionSymbol fs)
                {
                    symbol = fs;
                }
                else
                {
                    // In rare cases (e.g., runtime package has func main() that shadows the main package),
                    // the lookup finds a non-function. Report a diagnostic and create a stub.
                    _context.Errors.ReportError(syntax.Name.Span, ErrorCode.UnsupportedSyntax,
                        $"Function '{syntax.Name.Text}' shadows a {looked?.GetType().Name ?? "null"} symbol");
                    symbol = new FunctionSymbol(syntax.Name.Text,
                        System.Array.Empty<ParameterSymbol>(),
                        System.Array.Empty<TypeSymbol>());
                }
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
                if (nr.Name != "_")
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
                var typeName = baseTypeExpr is Language.Syntax.IdentifierNameSyntax id
                    ? id.Identifier.Text : baseTypeExpr?.ToString() ?? "?";
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.InvalidMethodReceiver,
                    $"Undefined receiver type '{typeName}'");
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
                if (nr.Name != "_")
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
                            if (typeIndex < returnTypes.Count)
                            {
                                // Use actual name for named returns, "_" placeholder for blank returns
                                // Blank-named returns still count (allow bare return statements)
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
            // Multiple passes to resolve forward references within const blocks.
            // Go allows const decls like: a = 1 << b; b = 9 (forward ref to b).
            int prevCount = -1;
            for (int pass = 0; pass < 4; pass++)
            {
                int resolvedCount = _context.PendingConstInts.Count;
                if (resolvedCount == prevCount)
                    break; // No progress, stop iterating
                prevCount = resolvedCount;

                foreach (var constDecl in constSyntaxes)
                {
                    int iotaCounter = 0;
                    SeparatedSyntaxList<ExpressionSyntax>? prevValues = null;

                    foreach (var spec in constDecl.Specs)
                    {
                        var values = spec.Values.HasValue ? spec.Values : prevValues;
                        if (spec.Values.HasValue)
                            prevValues = spec.Values;

                        if (values.HasValue)
                        {
                            for (int i = 0; i < spec.Names.Count && i < values.Value.Count; i++)
                            {
                                var name = spec.Names[i].Text;
                                if (_context.PendingConstInts.ContainsKey(name))
                                {
                                    continue; // Already resolved
                                }

                                var valExpr = values.Value[i];
                                var constVal = TryEvalConstIntWithIota(valExpr, iotaCounter);
                                if (constVal.HasValue)
                                {
                                    _context.PendingConstInts[name] = constVal.Value;
                                }

                                // Track string constant lengths for len(StringConst) array sizes
                                if (pass == 0 && valExpr is LiteralExpressionSyntax strLit
                                    && strLit.Token.Kind == SyntaxKind.StringLiteralToken)
                                {
                                    var str = strLit.Token.Text;
                                    // Strip surrounding quotes for interpreted strings
                                    if (str.Length >= 2 && str[0] == '"')
                                        str = str.Substring(1, str.Length - 2);
                                    _context.PendingConstStringLens[name] = str.Length;
                                }
                            }
                        }

                        iotaCounter++;
                    }
                }
            }
        }

        private void PreScanVarArrayLens(List<VarDeclarationSyntax> varSyntaxes)
        {
            foreach (var varDecl in varSyntaxes)
            {
                foreach (var spec in varDecl.Specs)
                {
                    if (!spec.Values.HasValue) continue;
                    for (int i = 0; i < spec.Names.Count && i < spec.Values.Value.Count; i++)
                    {
                        var name = spec.Names[i].Text;
                        var val = spec.Values.Value[i];
                        // Look for composite literal with auto-sized array type: [...]T{e1, e2, ...}
                        if (val is CompositeLiteralSyntax composite
                            && composite.Type is ArrayTypeSyntax arrType
                            && arrType.Length is LiteralExpressionSyntax litLen
                            && litLen.Token.Kind == SyntaxKind.EllipsisToken)
                        {
                            _context.PendingVarArrayLens[name] = composite.Elements.Count;
                        }
                    }
                }
            }
        }

        private long? TryEvalConstIntWithIota(ExpressionSyntax expr, int iota)
        {
            if (expr is IdentifierNameSyntax id && id.Identifier.Text == "iota")
                return iota;
            return TryEvalConstInt(expr, iota);
        }

        private long? TryEvalConstInt(ExpressionSyntax expr) => TryEvalConstInt(expr, -1, 0);

        private long? TryEvalConstInt(ExpressionSyntax expr, int iota) => TryEvalConstInt(expr, iota, 0);

        private long? TryEvalConstInt(ExpressionSyntax expr, int iota, int depth)
        {
            if (depth > 50) return null; // Prevent stack overflow from deeply nested expressions

            if (expr is LiteralExpressionSyntax lit
                && lit.Token.Kind == SyntaxKind.IntLiteralToken)
            {
                var text = lit.Token.Text;
                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hexVal))
                    return hexVal;
                if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase)
                    && TryParseOctalLong(text.AsSpan(2), out var octVal))
                    return octVal;
                if (long.TryParse(text, out var val))
                    return val;
            }

            if (expr is IdentifierNameSyntax id)
            {
                if (iota >= 0 && id.Identifier.Text == "iota")
                    return iota;
                if (_context.PendingConstInts.TryGetValue(id.Identifier.Text, out var idVal))
                    return idVal;
            }

            if (expr is BinaryExpressionSyntax bin)
            {
                var left = TryEvalConstInt(bin.Left, iota, depth + 1);
                var right = TryEvalConstInt(bin.Right, iota, depth + 1);
                if (left.HasValue && right.HasValue)
                {
                    return bin.OperatorToken.Kind switch
                    {
                        SyntaxKind.PlusToken => left.Value + right.Value,
                        SyntaxKind.MinusToken => left.Value - right.Value,
                        SyntaxKind.StarToken => left.Value * right.Value,
                        SyntaxKind.SlashToken when right.Value != 0 => left.Value / right.Value,
                        SyntaxKind.PercentToken when right.Value != 0 => left.Value % right.Value,
                        SyntaxKind.LessThanLessThanToken => left.Value << (int)right.Value,
                        SyntaxKind.GreaterThanGreaterThanToken => left.Value >> (int)right.Value,
                        SyntaxKind.AmpersandToken => left.Value & right.Value,
                        SyntaxKind.PipeToken => left.Value | right.Value,
                        SyntaxKind.CaretToken => left.Value ^ right.Value,
                        SyntaxKind.AmpersandCaretToken => left.Value & ~right.Value, // &^ in Go
                        _ => (long?)null,
                    };
                }
            }

            if (expr is UnaryExpressionSyntax unary)
            {
                var inner = TryEvalConstInt(unary.Operand, iota, depth + 1);
                if (inner.HasValue)
                {
                    return unary.OperatorToken.Kind switch
                    {
                        SyntaxKind.MinusToken => -inner.Value,
                        SyntaxKind.PlusToken => inner.Value,
                        SyntaxKind.CaretToken => ~inner.Value, // bitwise NOT in Go is ^
                        _ => (long?)null,
                    };
                }
            }

            // Parenthesized expressions: (expr)
            if (expr is ParenthesizedExpressionSyntax parenExpr)
            {
                return TryEvalConstInt(parenExpr.Expression, iota, depth + 1);
            }

            // Cross-package constant: pkg.Const (e.g., goarch.PtrSize, sys.PtrSize)
            if (expr is SelectorExpressionSyntax sel
                && sel.Expression is IdentifierNameSyntax pkgId)
            {
                var pkgSym = _context.Scope.Lookup(pkgId.Identifier.Text);
                if (pkgSym is PackageSymbol pkg)
                {
                    var member = pkg.LookupExport(sel.Name.Text);
                    if (member is ConstantSymbol c)
                    {
                        return c.Value is long lv ? lv : c.Value is int iv ? (long)iv : null;
                    }
                }
            }

            // Type conversion: uintptr(expr), int(expr), uint(expr) — evaluate inner expression
            if (expr is CallExpressionSyntax convCall
                && convCall.Arguments.Count == 1
                && convCall.Function is IdentifierNameSyntax convId)
            {
                var typeName = convId.Identifier.Text;
                if (typeName is "uintptr" or "int" or "uint" or "int64" or "uint64"
                    or "int32" or "uint32" or "int16" or "uint16" or "int8" or "uint8" or "byte")
                {
                    return TryEvalConstInt(convCall.Arguments[0], iota, depth + 1);
                }
            }

            // unsafe.Sizeof(...) — compute size of type for amd64
            if (expr is CallExpressionSyntax sizeofCall
                && sizeofCall.Function is SelectorExpressionSyntax sizeofSel
                && sizeofSel.Expression is IdentifierNameSyntax sizeofPkg
                && sizeofPkg.Identifier.Text == "unsafe"
                && sizeofSel.Name.Text == "Sizeof"
                && sizeofCall.Arguments.Count == 1)
            {
                // Return 8 as default for amd64 word size; actual struct size computation
                // happens later in TypeResolver.TryEvalConstantLength when types are resolved
                return 8;
            }

            // unsafe.Offsetof(...) — return 0 as default stub
            if (expr is CallExpressionSyntax offsetCall
                && offsetCall.Function is SelectorExpressionSyntax offsetSel
                && offsetSel.Expression is IdentifierNameSyntax offsetPkg
                && offsetPkg.Identifier.Text == "unsafe"
                && offsetSel.Name.Text == "Offsetof"
                && offsetCall.Arguments.Count == 1)
            {
                return 0;
            }

            // len("string literal") — Go len on string returns UTF-8 byte count
            if (expr is CallExpressionSyntax call
                && call.Function is IdentifierNameSyntax callId
                && callId.Identifier.Text == "len"
                && call.Arguments.Count == 1)
            {
                if (call.Arguments[0] is LiteralExpressionSyntax strLit
                    && strLit.Token.Kind == SyntaxKind.StringLiteralToken)
                {
                    var raw = strLit.Token.Text;
                    if (raw.Length >= 2 && raw[0] == '"')
                    {
                        var inner = raw.Substring(1, raw.Length - 2);
                        return System.Text.Encoding.UTF8.GetByteCount(inner);
                    }
                    if (raw.Length >= 2 && raw[0] == '`')
                    {
                        var inner = raw.Substring(1, raw.Length - 2);
                        return System.Text.Encoding.UTF8.GetByteCount(inner);
                    }
                }
                // len(constIdentifier) where the const is a string
                if (call.Arguments[0] is IdentifierNameSyntax lenArgId
                    && _context.PendingConstStringLens.TryGetValue(lenArgId.Identifier.Text, out var strLen))
                    return strLen;
            }

            return null;
        }

        private static bool TryParseOctal(ReadOnlySpan<char> s, out int result)
        {
            result = 0;
            foreach (var c in s)
            {
                if (c == '_') continue;
                if (c < '0' || c > '7') return false;
                result = result * 8 + (c - '0');
            }
            return s.Length > 0;
        }

        private static bool TryParseOctalLong(ReadOnlySpan<char> s, out long result)
        {
            result = 0;
            foreach (var c in s)
            {
                if (c == '_') continue;
                if (c < '0' || c > '7') return false;
                result = result * 8 + (c - '0');
            }
            return s.Length > 0;
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

                var alias = new TypeSymbol(name, underlying.TypeKind, underlying) { IsAlias = true };
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

        private void FixupTypeParameterConstraints(List<TypeDeclarationSyntax> typeSyntaxes)
        {
            foreach (var typeDecl in typeSyntaxes)
            {
                foreach (var spec in typeDecl.Specs)
                {
                    if (spec.TypeParameters == null) continue;
                    var sym = _context.Scope.Lookup(spec.Name.Text) as TypeSymbol;
                    if (sym == null || !sym.IsGeneric) continue;

                    bool needsFixup = false;
                    int idx = 0;
                    for (int i = 0; i < spec.TypeParameters.Parameters.Count; i++)
                    {
                        var decl = spec.TypeParameters.Parameters[i];
                        for (int j = 0; j < decl.Names.Count; j++, idx++)
                        {
                            if (idx < sym.TypeParameters.Count
                                && sym.TypeParameters[idx].Constraint == ConstraintInfo.Any
                                && decl.Constraint is not IdentifierNameSyntax idCheck)
                            {
                                needsFixup = true;
                            }
                            else if (idx < sym.TypeParameters.Count
                                && sym.TypeParameters[idx].Constraint == ConstraintInfo.Any
                                && decl.Constraint is IdentifierNameSyntax idSyntax2
                                && idSyntax2.Identifier.Text != "any")
                            {
                                // Constraint was an identifier like a named interface that wasn't found
                                needsFixup = true;
                            }
                        }
                    }

                    if (!needsFixup) continue;

                    // Re-resolve constraints with all types now in scope
                    _context.PushScope("typeParamFixup");
                    foreach (var tp in sym.TypeParameters)
                        _context.Scope.TryDeclare(tp);

                    idx = 0;
                    for (int i = 0; i < spec.TypeParameters.Parameters.Count; i++)
                    {
                        var decl = spec.TypeParameters.Parameters[i];
                        var constraint = ResolveConstraint(decl.Constraint);
                        for (int j = 0; j < decl.Names.Count; j++, idx++)
                        {
                            if (idx < sym.TypeParameters.Count && constraint != ConstraintInfo.Any)
                                sym.TypeParameters[idx].Constraint = constraint;
                        }
                    }

                    _context.PopScope();
                }
            }
        }

        private void FixupEmbeddedInterfaces(List<TypeDeclarationSyntax> typeSyntaxes)
        {
            // Re-process interfaces that embed other interfaces, in case the embedded
            // interface hadn't been fully resolved due to forward references.
            foreach (var typeDecl in typeSyntaxes)
            {
                foreach (var spec in typeDecl.Specs)
                {
                    if (spec.Type is not InterfaceTypeSyntax ifaceSyntax) continue;

                    var symbol = _context.Scope.Lookup(spec.Name.Text) as InterfaceTypeSymbol;
                    if (symbol == null) continue;

                    // Check if any embedded interface member exists
                    bool hasEmbedded = false;
                    foreach (var member in ifaceSyntax.Members)
                    {
                        if (member is not MethodSpecSyntax)
                        {
                            hasEmbedded = true;
                            break;
                        }
                    }

                    if (!hasEmbedded) continue;

                    // Re-resolve: rebuild method list with now-populated embedded interfaces
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

                            var method = new MethodSymbol(methodSpec.Name.Text, symbol, false,
                                Array.Empty<TypeParameterSymbol>(), parameters, returnTypes, isVariadic);
                            methods.Add(method);
                        }
                        else if (member is ExpressionSyntax embeddedSyntax)
                        {
                            var embeddedType = _typeResolver.ResolveType(embeddedSyntax);
                            if (embeddedType is InterfaceTypeSymbol embeddedIface)
                            {
                                foreach (var m in embeddedIface.Methods)
                                {
                                    // Avoid duplicates (already added in first pass)
                                    bool exists = false;
                                    foreach (var existing in methods)
                                    {
                                        if (existing.Name == m.Name)
                                        {
                                            exists = true;
                                            break;
                                        }
                                    }

                                    if (!exists)
                                    {
                                        var promoted = new MethodSymbol(m.Name, symbol, false,
                                            Array.Empty<TypeParameterSymbol>(), m.Parameters, m.ReturnTypes, m.IsVariadic);
                                        methods.Add(promoted);
                                    }
                                }
                            }
                        }
                    }

                    // Only update if we found more methods than before
                    if (methods.Count > symbol.Methods.Count)
                    {
                        symbol.SetMethods(methods);
                    }
                }
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
                        // For generic instantiations like node[N, T], use "node" (the base name)
                        var embType = fieldType is PointerTypeSymbol embPtr
                            ? embPtr.ElementType : fieldType;
                        var embeddedName = embType is InstantiatedTypeSymbol inst
                            ? inst.GenericType.Name
                            : embType.Name;
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
                if (ifaceSymbol.IsGeneric)
                {
                    _context.PushScope("typeParams");
                    foreach (var tp in ifaceSymbol.TypeParameters)
                        _context.Scope.TryDeclare(tp);
                }

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

                if (ifaceSymbol.IsGeneric)
                    _context.PopScope();
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
                    if (name != "_")
                    {
                        _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.TypeMismatch,
                            $"Variable '{name}' requires a type or initializer");
                    }
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

            // First pass: create all type parameters with Any constraint and
            // push them into scope so that constraints like ~map[K]V can
            // reference type parameters declared later in the list.
            _context.PushScope("typeParamPre");
            for (int i = 0; i < syntax.Parameters.Count; i++)
            {
                var decl = syntax.Parameters[i];
                for (int j = 0; j < decl.Names.Count; j++)
                {
                    var name = decl.Names[j].Text;
                    var tp = new TypeParameterSymbol(name, ordinal++, ConstraintInfo.Any);
                    result.Add(tp);
                    _context.Scope.TryDeclare(tp);
                }
            }

            // Second pass: resolve constraints now that all type params are in scope
            int idx = 0;
            for (int i = 0; i < syntax.Parameters.Count; i++)
            {
                var decl = syntax.Parameters[i];
                var constraint = ResolveConstraint(decl.Constraint);

                for (int j = 0; j < decl.Names.Count; j++)
                {
                    result[idx++].Constraint = constraint;
                }
            }

            _context.PopScope();
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

            // Handle generic interface constraint: e.g., nistPoint[Point]
            if (syntax is IndexExpressionSyntax indexConstraint)
            {
                TypeSymbol? baseSym = null;
                string? baseName = null;
                if (indexConstraint.Expression is IdentifierNameSyntax baseId)
                {
                    baseName = baseId.Identifier.Text;
                    baseSym = _context.Scope.Lookup(baseName) as TypeSymbol;
                }
                if (baseSym is InterfaceTypeSymbol genericIface && genericIface.TypeParameters.Count > 0)
                {
                    var argType = _typeResolver.ResolveType(indexConstraint.Index);
                    // Store the interface reference for lazy method resolution
                    // (interface methods may not be populated yet during PreDeclareType)
                    var constraint = new ConstraintInfo(genericIface.Name, Array.Empty<MethodSymbol>(),
                        Array.Empty<TypeElement>(), isComparable: false);
                    constraint.InterfaceType = genericIface;
                    constraint.InterfaceTypeArgs = new[] { argType ?? TypeSymbol.Error };
                    return constraint;
                }
                // If the base type isn't declared yet (forward reference), treat as any
                // The constraint will be checked at instantiation time when the type exists
                if (baseSym == null && baseName != null)
                    return ConstraintInfo.Any;
            }

            // Handle type expression constraints like *T, []T, map[K]V, etc.
            // These define the type set for the parameter (e.g., P *T means P's type set is {*T})
            var constraintType = _typeResolver.ResolveType(syntax);
            if (constraintType != null && constraintType != TypeSymbol.Error)
            {
                var typeElements = new List<TypeElement> { new TypeElement(constraintType, false) };
                return new ConstraintInfo("type", Array.Empty<MethodSymbol>(),
                    typeElements, isComparable: false);
            }

            return ConstraintInfo.Any;
        }

        private static TypeSymbol SubstituteTypeParam(TypeSymbol type, TypeParameterSymbol param, TypeSymbol? arg)
        {
            if (arg == null) return type;
            if (type == param) return arg;
            if (type is SliceTypeSymbol slice)
                return new SliceTypeSymbol(SubstituteTypeParam(slice.ElementType, param, arg));
            if (type is PointerTypeSymbol ptr)
                return new PointerTypeSymbol(SubstituteTypeParam(ptr.ElementType, param, arg));
            if (type is MapTypeSymbol map)
                return new MapTypeSymbol(
                    SubstituteTypeParam(map.KeyType, param, arg),
                    SubstituteTypeParam(map.ValueType, param, arg));
            if (type is ChannelTypeSymbol ch)
                return new ChannelTypeSymbol(SubstituteTypeParam(ch.ElementType, param, arg));
            return type;
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

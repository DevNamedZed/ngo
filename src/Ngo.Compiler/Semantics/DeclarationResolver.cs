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
                        foreach (var spec in typeSyntax.Specs)
                        {
                            RegisterTypeDeclaration(spec);
                        }
                    }
                    else if (member is ConstDeclarationSyntax constSyntax)
                    {
                        constSyntaxes.Add(constSyntax);
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

            var constants = new List<ConstDeclaration>();
            foreach (var constSyntax in constSyntaxes)
            {
                constants.AddRange(ResolveConstDeclaration(constSyntax));
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
                        _context.Errors.ReportError(span, ErrorCode.AlreadyDeclared,
                            $"'{localName}' is already declared in this scope");
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
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                    $"Function '{syntax.Name.Text}' is already declared");
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

            var body = _statementResolver.ResolveBlock(syntax.Body!);

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
            var receiverParam = syntax.Receiver.Parameters[0];
            var receiverTypeExpr = receiverParam.Type;

            bool isPointerReceiver = receiverTypeExpr is PointerTypeSyntax;
            var baseTypeExpr = isPointerReceiver
                ? ((PointerTypeSyntax)receiverTypeExpr!).ElementType
                : receiverTypeExpr;

            var baseType = _typeResolver.ResolveType(baseTypeExpr!);
            if (baseType == null)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.InvalidMethodReceiver,
                    "Undefined receiver type");
                return;
            }

            var parameters = _typeResolver.ResolveParameterList(syntax.Parameters);
            var returnTypes = _typeResolver.ResolveResultTypes(syntax.Result);

            var method = new MethodSymbol(syntax.Name.Text, baseType, isPointerReceiver,
                parameters, returnTypes);

            var existing = baseType.LookupMethod(syntax.Name.Text);
            if (existing != null)
            {
                _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                    $"Method '{syntax.Name.Text}' is already declared on type '{baseType.Name}'");
                return;
            }

            baseType.AddMethod(method);
        }

        private MethodDeclaration ResolveMethodDeclaration(MethodDeclarationSyntax syntax)
        {
            var receiverParam = syntax.Receiver.Parameters[0];
            var receiverTypeExpr = receiverParam.Type;

            bool isPointerReceiver = receiverTypeExpr is PointerTypeSyntax;
            var baseTypeExpr = isPointerReceiver
                ? ((PointerTypeSyntax)receiverTypeExpr!).ElementType
                : receiverTypeExpr;

            var baseType = _typeResolver.ResolveType(baseTypeExpr!);
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

            var body = _statementResolver.ResolveBlock(syntax.Body!);

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
                if (!_context.Scope.TryDeclare(alias))
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                        $"Type '{name}' is already declared");
                }

                return;
            }

            // Resolve type parameters if present
            IReadOnlyList<TypeParameterSymbol>? typeParams = null;
            if (syntax.TypeParameters != null)
            {
                typeParams = ResolveTypeParameterList(syntax.TypeParameters);
            }

            // Named type definition: create the type symbol now, fill in fields/methods in pass 2
            if (syntax.Type is StructTypeSyntax)
            {
                var structType = new StructTypeSymbol(name, new List<FieldSymbol>());
                if (typeParams != null)
                    structType.SetTypeParameters(typeParams);
                if (!_context.Scope.TryDeclare(structType))
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                        $"Type '{name}' is already declared");
                }
            }
            else if (syntax.Type is InterfaceTypeSyntax)
            {
                var ifaceType = new InterfaceTypeSymbol(name, new List<MethodSymbol>());
                if (typeParams != null)
                    ifaceType.SetTypeParameters(typeParams);
                if (!_context.Scope.TryDeclare(ifaceType))
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                        $"Type '{name}' is already declared");
                }
            }
            else
            {
                // Non-struct type definition (e.g., type MyInt int)
                var underlying = _typeResolver.ResolveType(syntax.Type);
                if (underlying == null)
                {
                    underlying = TypeSymbol.Error;
                }

                var namedType = new TypeSymbol(name, underlying.TypeKind, underlying);
                if (!_context.Scope.TryDeclare(namedType))
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                        $"Type '{name}' is already declared");
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
                        // Embedded field: use the type name as the field name
                        var embeddedName = fieldType.Name;
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
                        var method = new MethodSymbol(methodSpec.Name.Text, ifaceSymbol, false,
                            parameters, returnTypes);
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
                                    m.Parameters, m.ReturnTypes);
                                methods.Add(promoted);
                            }
                        }
                    }
                }

                ifaceSymbol.SetMethods(methods);
            }

            return new TypeDeclaration(symbol ?? TypeSymbol.Error, _context.SpanOf(syntax));
        }

        private IReadOnlyList<VarDeclaration> ResolveVarSpec(VarSpecSyntax syntax)
        {
            var results = new List<VarDeclaration>();
            var declaredType = syntax.Type != null ? _typeResolver.ResolveType(syntax.Type) : null;

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

                var symbol = new LocalSymbol(name, varType);
                if (!_context.Scope.TryDeclare(symbol))
                {
                    _context.Errors.ReportError(_context.SpanOf(syntax), ErrorCode.AlreadyDeclared,
                        $"Variable '{name}' is already declared");
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
            var typeExpr = spec.Type ?? prevType;

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
                    ?? (initializer != null ? TypeChecker.DefaultType(initializer.Type) : BuiltinTypes.Int);

                object? constValue = _context.TryEvaluateConstant(initializer);
                var symbol = new ConstantSymbol(name, constType, constValue);

                if (!_context.Scope.TryDeclare(symbol))
                {
                    _context.Errors.ReportError(_context.SpanOf(spec), ErrorCode.AlreadyDeclared,
                        $"Constant '{name}' is already declared");
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

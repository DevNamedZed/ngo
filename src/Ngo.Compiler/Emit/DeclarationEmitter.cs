// -----------------------------------------------------------------------
// <copyright file="DeclarationEmitter.cs" company="Ziad">
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
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Ast;
using Ngo.Compiler.Emit.Builder;
using Ngo.Compiler.Symbols;
using Ngo.Runtime;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Emits type shells and method signatures (first pass, no bodies).
    /// </summary>
    internal sealed class DeclarationEmitter
    {
        private readonly EmitContext _ctx;
        private int _initCounter;

        public DeclarationEmitter(EmitContext ctx)
        {
            _ctx = ctx;
        }

        public void EmitBuiltinErrorInterface()
        {
            EmitInterfaceType(BuiltinTypes.Error);
        }

        public void EmitTypeDeclaration(TypeDeclaration decl)
        {
            if (decl.Symbol is StructTypeSymbol structType)
            {
                EmitStructType(structType);
            }
            else if (decl.Symbol is InterfaceTypeSymbol interfaceType)
            {
                EmitInterfaceType(interfaceType);
            }
        }

        /// <summary>
        /// Phase 1: Define the TypeBuilder and register it, but don't add fields yet.
        /// This allows all struct types to be forward-declared before any field types are resolved.
        /// </summary>
        public void DefineStructType(StructTypeSymbol structType)
        {
            // Skip if already defined (by identity or by qualified name)
            if (_ctx.StructTypes.ContainsKey(structType))
            {
                return;
            }
            var qualifiedName = _ctx.QualifyName(structType.Name);
            foreach (var kvp in _ctx.StructTypes)
            {
                if (kvp.Value.AsType().FullName == qualifiedName)
                {
                    // Already defined under a different symbol — register the mapping
                    _ctx.StructTypes[structType] = kvp.Value;
                    _ctx.Mapper.Register(structType, kvp.Value.AsType());
                    return;
                }
            }

            var typeVisibility = (_ctx.Options.IsLibrary && !_ctx.IsExported(structType.Name))
                ? TypeAttributes.NotPublic
                : TypeAttributes.Public;
            var typeBuilder = _ctx.Module.DefineType(
                qualifiedName,
                typeVisibility | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
                typeof(System.ValueType));

            // Define generic type parameters if this is a generic struct
            if (structType.IsGeneric)
            {
                var paramNames = new string[structType.TypeParameters.Count];
                for (int i = 0; i < structType.TypeParameters.Count; i++)
                {
                    paramNames[i] = structType.TypeParameters[i].Name;
                }

                var genericParams = typeBuilder.DefineGenericParameters(paramNames);
                for (int i = 0; i < structType.TypeParameters.Count; i++)
                {
                    _ctx.Mapper.Register(structType.TypeParameters[i], genericParams[i]);
                    ApplyConstraints(genericParams[i], structType.TypeParameters[i].Constraint);
                }
            }

            _ctx.Mapper.Register(structType, typeBuilder.AsType());
            _ctx.StructTypes[structType] = typeBuilder;
        }

        /// <summary>
        /// Phase 2: Add fields and String() override to an already-defined struct TypeBuilder.
        /// Called after all struct types have been defined so cross-references resolve.
        /// </summary>
        public void PopulateStructFields(StructTypeSymbol structType)
        {
            if (!_ctx.StructTypes.TryGetValue(structType, out var typeBuilder))
                return;

            foreach (var field in structType.Fields)
            {
                // Skip fields already defined (e.g., if called twice)
                if (_ctx.StructFields.ContainsKey(field))
                    continue;

                var fieldType = _ctx.Mapper.Map(field.Type);
                var fieldVisibility = (_ctx.Options.IsLibrary && !_ctx.IsExported(field.Name))
                    ? FieldAttributes.Assembly
                    : FieldAttributes.Public;
                var fb = typeBuilder.DefineField(field.Name, fieldType, fieldVisibility);
                if (field.Tag != null)
                {
                    var tagCtor = typeof(GoTagAttribute).GetConstructor(new[] { typeof(string) })!;
                    fb.SetCustomAttribute(new CustomAttributeBuilder(tagCtor, new object[] { field.Tag }));
                }
                _ctx.StructFields[field] = fb;
            }

            // If the struct has a String() string method, add a ToString() override
            var stringMethod = structType.LookupMethod("String");
            if (stringMethod != null && stringMethod.Parameters.Count == 0
                && stringMethod.ReturnTypes.Count == 1
                && stringMethod.ReturnTypes[0].TypeKind == TypeKind.String
                && !_ctx.Methods.ContainsKey(stringMethod))
            {
                var staticMethodName = structType.Name + "_String";
                var staticStringMethod = _ctx.PackageType.DefineMethod(staticMethodName,
                    MethodAttributes.Public | MethodAttributes.Static,
                    typeof(string), new[] { typeBuilder.AsType() });
                _ctx.Methods[stringMethod] = staticStringMethod;

                var toString = typeBuilder.DefineMethod("ToString",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    typeof(string), Type.EmptyTypes);
                var il = toString.GetILWriter();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldobj, typeBuilder.AsType());
                il.Emit(OpCodes.Call, staticStringMethod.AsMethodInfo());
                il.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(toString,
                    typeof(object).GetMethod("ToString")!);
            }
        }

        private void EmitStructType(StructTypeSymbol structType)
        {
            DefineStructType(structType);
            PopulateStructFields(structType);
        }

        private void EmitInterfaceType(InterfaceTypeSymbol interfaceType)
        {
            // Skip if already defined
            if (_ctx.InterfaceTypes.ContainsKey(interfaceType))
            {
                return;
            }
            var qualifiedIfaceName = _ctx.QualifyName(interfaceType.Name);
            foreach (var kvp in _ctx.InterfaceTypes)
            {
                if (kvp.Value.AsType().FullName == qualifiedIfaceName)
                {
                    _ctx.InterfaceTypes[interfaceType] = kvp.Value;
                    _ctx.Mapper.Register(interfaceType, kvp.Value.AsType());
                    return;
                }
            }

            var typeVisibility = (_ctx.Options.IsLibrary && !_ctx.IsExported(interfaceType.Name))
                ? TypeAttributes.NotPublic
                : TypeAttributes.Public;
            var typeBuilder = _ctx.Module.DefineType(
                qualifiedIfaceName,
                typeVisibility | TypeAttributes.Interface | TypeAttributes.Abstract);

            foreach (var method in interfaceType.Methods)
            {
                var paramTypes = new Type[method.Parameters.Count];
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    paramTypes[i] = _ctx.Mapper.Map(method.Parameters[i].Type);
                }

                var returnType = _ctx.Mapper.MapReturnType(method.ReturnTypes);
                typeBuilder.DefineMethod(
                    method.Name,
                    MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
                    returnType,
                    paramTypes);
            }

            _ctx.Mapper.Register(interfaceType, typeBuilder.AsType());
            _ctx.InterfaceTypes[interfaceType] = typeBuilder;
        }

        public void EmitFunction(FunctionDeclaration func)
        {
            // Go allows multiple init() functions — give each a unique IL name
            var methodName = func.Symbol.Name;
            if (methodName == "init")
            {
                methodName = "init$" + _initCounter++;
            }

            var isInitOrMain = methodName.StartsWith("init$") || methodName == "main";
            var methodVisibility = (_ctx.Options.IsLibrary && !isInitOrMain && !_ctx.IsExported(func.Symbol.Name))
                ? MethodAttributes.Assembly
                : MethodAttributes.Public;

            if (func.Symbol.IsGeneric)
            {
                // Generic function: define method with placeholder types first,
                // then add generic parameters, then set real types
                var method = _ctx.PackageType.DefineMethod(
                    methodName,
                    methodVisibility | MethodAttributes.Static);

                var typeParams = func.Symbol.TypeParameters;
                var paramNames = new string[typeParams.Count];
                for (int i = 0; i < typeParams.Count; i++)
                {
                    paramNames[i] = typeParams[i].Name;
                }

                var genericParams = method.DefineGenericParameters(paramNames);
                for (int i = 0; i < typeParams.Count; i++)
                {
                    _ctx.Mapper.Register(typeParams[i], genericParams[i]);
                    ApplyConstraints(genericParams[i], typeParams[i].Constraint);
                }

                // Now set return type and parameter types (type params now resolve)
                var parameters = func.Symbol.Parameters;
                var paramTypes = new Type[parameters.Count];
                for (int i = 0; i < parameters.Count; i++)
                {
                    paramTypes[i] = _ctx.Mapper.Map(parameters[i].Type);
                }

                var returnType = _ctx.Mapper.MapReturnType(func.Symbol.ReturnTypes);
                method.SetReturnType(returnType);
                method.SetParameters(paramTypes);

                for (int i = 0; i < parameters.Count; i++)
                {
                    method.DefineParameter(i + 1, ParameterAttributes.None, parameters[i].Name);
                }

                _ctx.Methods[func.Symbol] = method;
            }
            else
            {
                var parameters = func.Symbol.Parameters;
                var paramTypes = new Type[parameters.Count];
                for (int i = 0; i < parameters.Count; i++)
                {
                    paramTypes[i] = _ctx.Mapper.Map(parameters[i].Type);
                }

                var returnType = _ctx.Mapper.MapReturnType(func.Symbol.ReturnTypes);
                var method = _ctx.PackageType.DefineMethod(
                    methodName,
                    methodVisibility | MethodAttributes.Static,
                    returnType,
                    paramTypes);

                for (int i = 0; i < parameters.Count; i++)
                {
                    method.DefineParameter(i + 1, ParameterAttributes.None, parameters[i].Name);
                }

                _ctx.Methods[func.Symbol] = method;
            }
        }

        public void EmitMethod(MethodDeclaration decl)
        {
            // Skip if already pre-created (e.g., Stringer ToString override)
            if (_ctx.Methods.ContainsKey(decl.Symbol))
                return;

            // Methods are emitted as static methods on the package type
            // with the receiver as the first parameter

            // Name format: ReceiverType_MethodName
            var methodName = $"{decl.Symbol.ReceiverType.Name}_{decl.Symbol.Name}";
            var methodVisibility = (_ctx.Options.IsLibrary && !_ctx.IsExported(decl.Symbol.Name))
                ? MethodAttributes.Assembly
                : MethodAttributes.Public;

            // Check if receiver type is generic — if so, mirror type params on method
            var receiverBaseType = decl.Symbol.ReceiverType;
            if (receiverBaseType is PointerTypeSymbol ptrSym)
            {
                receiverBaseType = ptrSym.ElementType;
            }

            StructTypeSymbol? genericStruct = receiverBaseType is StructTypeSymbol sts && sts.IsGeneric
                ? sts
                : null;

            if (genericStruct != null)
            {
                var method = _ctx.PackageType.DefineMethod(
                    methodName,
                    methodVisibility | MethodAttributes.Static);

                // Mirror the type's generic parameters onto the method
                var typeParams = genericStruct.TypeParameters;
                var paramNames = new string[typeParams.Count];
                for (int i = 0; i < typeParams.Count; i++)
                {
                    paramNames[i] = typeParams[i].Name;
                }

                var genericParams = method.DefineGenericParameters(paramNames);
                for (int i = 0; i < typeParams.Count; i++)
                {
                    _ctx.Mapper.Register(typeParams[i], genericParams[i]);
                    ApplyConstraints(genericParams[i], typeParams[i].Constraint);
                }

                // Now set types (type params now resolve)
                var receiverType = _ctx.Mapper.Map(decl.Receiver.Type);
                var parameters = decl.Symbol.Parameters;
                var paramTypes = new Type[parameters.Count + 1];
                paramTypes[0] = receiverType;
                for (int i = 0; i < parameters.Count; i++)
                {
                    paramTypes[i + 1] = _ctx.Mapper.Map(parameters[i].Type);
                }

                var returnType = _ctx.Mapper.MapReturnType(decl.Symbol.ReturnTypes);
                method.SetReturnType(returnType);
                method.SetParameters(paramTypes);

                method.DefineParameter(1, ParameterAttributes.None, decl.Receiver.Name);
                for (int i = 0; i < parameters.Count; i++)
                {
                    method.DefineParameter(i + 2, ParameterAttributes.None, parameters[i].Name);
                }

                _ctx.Methods[decl.Symbol] = method;
            }
            else
            {
                var receiverType = _ctx.Mapper.Map(decl.Receiver.Type);
                var parameters = decl.Symbol.Parameters;
                var paramTypes = new Type[parameters.Count + 1];
                paramTypes[0] = receiverType;
                for (int i = 0; i < parameters.Count; i++)
                {
                    paramTypes[i + 1] = _ctx.Mapper.Map(parameters[i].Type);
                }

                var returnType = _ctx.Mapper.MapReturnType(decl.Symbol.ReturnTypes);
                var method = _ctx.PackageType.DefineMethod(
                    methodName,
                    methodVisibility | MethodAttributes.Static,
                    returnType,
                    paramTypes);

                method.DefineParameter(1, ParameterAttributes.None, decl.Receiver.Name);
                for (int i = 0; i < parameters.Count; i++)
                {
                    method.DefineParameter(i + 2, ParameterAttributes.None, parameters[i].Name);
                }

                _ctx.Methods[decl.Symbol] = method;
            }
        }

        private static void ApplyConstraints(
            Type genericParam,
            Symbols.ConstraintInfo constraint)
        {
            // any, comparable, union types → unconstrained in .NET
            if (constraint == Symbols.ConstraintInfo.Any || constraint == Symbols.ConstraintInfo.Comparable)
            {
                return;
            }

            // Union type elements can't be expressed in .NET
            if (constraint.TypeElements.Count > 0)
            {
                return;
            }

            // Interface method constraints → SetInterfaceConstraints
            // Only if we can map the constraint to a .NET interface type
            // For now, leave unconstrained (Go validates at semantic analysis level)
        }

        public void EmitPackageVar(VarDeclaration decl)
        {
            var fieldType = _ctx.Mapper.Map(decl.Symbol.Type);
            var fieldVisibility = (_ctx.Options.IsLibrary && !_ctx.IsExported(decl.Symbol.Name))
                ? FieldAttributes.Assembly
                : FieldAttributes.Public;
            var field = _ctx.PackageType.DefineField(
                decl.Symbol.Name,
                fieldType,
                fieldVisibility | FieldAttributes.Static);
            _ctx.PackageFields[decl.Symbol] = field;
        }

        /// <summary>
        /// Generates a wrapper class that bridges a concrete type to a .NET interface.
        /// The wrapper implements the interface and delegates method calls to the
        /// corresponding static Go-style methods on the package type.
        /// </summary>
        public Type GenerateWrapper(TypeSymbol concreteType, InterfaceTypeSymbol interfaceType)
        {
            var key = new WrapperTypeKey(concreteType, interfaceType);
            if (_ctx.WrapperTypes.TryGetValue(key, out var cached))
                return cached.Type;

            // Check for an existing wrapper with same name (symbol identity mismatch)
            foreach (var existing in _ctx.WrapperTypes)
            {
                if (existing.Key.SourceType.Name == concreteType.Name
                    && existing.Key.InterfaceType.Name == interfaceType.Name)
                {
                    _ctx.WrapperTypes[key] = existing.Value;
                    return existing.Value.Type;
                }
            }

            var concreteClrType = _ctx.Mapper.Map(concreteType);
            var interfaceClrType = _ctx.Mapper.Map(interfaceType);

            var wrapperName = $"{concreteType.Name}__{interfaceType.Name}__Wrapper";

            var wrapperBuilder = _ctx.Module.DefineType(
                _ctx.QualifyName(wrapperName),
                TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(object),
                new[] { interfaceClrType });

            // Field to hold the wrapped value
            var valueField = wrapperBuilder.DefineField(
                "_value",
                concreteClrType,
                FieldAttributes.Public);

            // Constructor: takes the concrete value, stores in _value
            var ctorBuilder = wrapperBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { concreteClrType });

            var ctorIL = ctorBuilder.GetILWriter();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1);
            ctorIL.Emit(OpCodes.Stfld, valueField.AsFieldInfo());
            ctorIL.Emit(OpCodes.Ret);

            // Implement each interface method by delegating to the Go static method
            foreach (var ifaceMethod in interfaceType.Methods)
            {
                var concreteMethod = concreteType.LookupMethod(ifaceMethod.Name);
                FieldSymbol? embeddedField = null;

                // For pointer types, look up method on element type
                if (concreteMethod == null && concreteType is PointerTypeSymbol ptrSymbol)
                {
                    concreteMethod = ptrSymbol.ElementType.LookupMethod(ifaceMethod.Name);
                }

                // Check promoted methods from embedded structs
                var structLookupType = concreteType is PointerTypeSymbol ptrStruct
                    ? ptrStruct.ElementType as StructTypeSymbol
                    : concreteType as StructTypeSymbol;
                if (concreteMethod == null && structLookupType != null)
                {
                    var promoted = structLookupType.LookupPromotedMethod(ifaceMethod.Name);
                    if (promoted != null)
                    {
                        concreteMethod = promoted.Method;
                        embeddedField = promoted.EmbeddedField;
                    }
                }

                IMethodBuilder? goMethod = null;
                if (concreteMethod != null)
                {
                    _ctx.Methods.TryGetValue(concreteMethod, out goMethod);
                }
                if (goMethod == null)
                {
                    _ctx.Methods.TryGetValue(ifaceMethod, out goMethod);
                }
                if (goMethod == null)
                {
                    // Try CLR reflection on the concrete type for runtime methods
                    var concreteClrType2 = _ctx.Mapper.Map(concreteType);
                    MethodInfo? clrMethod = null;
                    if (concreteClrType2 != typeof(object))
                    {
                        try
                        {
                            clrMethod = concreteClrType2.GetMethod(ifaceMethod.Name);
                        }
                        catch (NotSupportedException)
                        {
                            // TypeBuilder doesn't support GetMethod in some contexts
                        }
                    }
                    // Also try CachedMethods
                    if (clrMethod == null && concreteMethod != null
                        && _ctx.CachedMethods.TryGetValue(concreteMethod, out var cachedClrMethod))
                    {
                        clrMethod = cachedClrMethod;
                    }
                    if (clrMethod != null)
                    {
                        // Match the CLR interface method's signature (not the Go type mapping)
                        // to avoid type mismatches between C# annotations and Go type system
                        var clrParams = clrMethod.GetParameters();
                        var wrapperParamTypes = new Type[clrParams.Length];
                        for (int pi = 0; pi < clrParams.Length; pi++)
                        {
                            wrapperParamTypes[pi] = clrParams[pi].ParameterType;
                        }
                        var wrapperReturnType = clrMethod.ReturnType;

                        // Check if the interface CLR type has this method — if so, match THAT signature
                        if (interfaceClrType.IsInterface)
                        {
                            var ifaceClrMethod = interfaceClrType.GetMethod(ifaceMethod.Name);
                            if (ifaceClrMethod != null)
                            {
                                var ifaceClrParams = ifaceClrMethod.GetParameters();
                                wrapperParamTypes = new Type[ifaceClrParams.Length];
                                for (int pi = 0; pi < ifaceClrParams.Length; pi++)
                                {
                                    wrapperParamTypes[pi] = ifaceClrParams[pi].ParameterType;
                                }
                                wrapperReturnType = ifaceClrMethod.ReturnType;
                            }
                        }
                        var wrapperMethod = wrapperBuilder.DefineMethod(
                            ifaceMethod.Name,
                            MethodAttributes.Public | MethodAttributes.Virtual,
                            wrapperReturnType, wrapperParamTypes);
                        var wrapperIL = wrapperMethod.GetILWriter();
                        wrapperIL.Emit(OpCodes.Ldarg_0);
                        wrapperIL.Emit(OpCodes.Ldfld, valueField.AsFieldInfo());
                        for (int pi = 0; pi < ifaceMethod.Parameters.Count; pi++)
                        {
                            wrapperIL.Emit(OpCodes.Ldarg, pi + 1);
                        }
                        wrapperIL.Emit(concreteClrType2.IsValueType ? OpCodes.Call : OpCodes.Callvirt, clrMethod);
                        wrapperIL.Emit(OpCodes.Ret);
                        continue;
                    }

                    // Method not found — emit a stub that returns default value
                    // This prevents "method has no implementation" runtime errors
                    var stubParamTypes = new Type[ifaceMethod.Parameters.Count];
                    for (int i = 0; i < ifaceMethod.Parameters.Count; i++)
                        stubParamTypes[i] = _ctx.Mapper.Map(ifaceMethod.Parameters[i].Type);
                    var stubReturnType = _ctx.Mapper.MapReturnType(ifaceMethod.ReturnTypes);
                    var stubMethod = wrapperBuilder.DefineMethod(
                        ifaceMethod.Name,
                        MethodAttributes.Public | MethodAttributes.Virtual,
                        stubReturnType, stubParamTypes);
                    var stubIL = stubMethod.GetILWriter();
                    if (stubReturnType == typeof(string))
                        stubIL.Emit(OpCodes.Ldstr, "");
                    else if (stubReturnType == typeof(void))
                    { /* no return value */ }
                    else if (stubReturnType.IsValueType)
                    {
                        var loc = stubIL.DeclareLocal(stubReturnType);
                        stubIL.Emit(OpCodes.Ldloca, loc);
                        stubIL.Emit(OpCodes.Initobj, stubReturnType);
                        stubIL.Emit(OpCodes.Ldloc, loc);
                    }
                    else
                        stubIL.Emit(OpCodes.Ldnull);
                    stubIL.Emit(OpCodes.Ret);
                    continue;
                }

                var paramTypes = new Type[ifaceMethod.Parameters.Count];
                for (int i = 0; i < ifaceMethod.Parameters.Count; i++)
                    paramTypes[i] = _ctx.Mapper.Map(ifaceMethod.Parameters[i].Type);

                var returnType = _ctx.Mapper.MapReturnType(ifaceMethod.ReturnTypes);

                var methodBuilder = wrapperBuilder.DefineMethod(
                    ifaceMethod.Name,
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    returnType,
                    paramTypes);

                var methodIL = methodBuilder.GetILWriter();

                if (embeddedField != null)
                {
                    // Promoted method: load _value then access the embedded field
                    methodIL.Emit(OpCodes.Ldarg_0);
                    methodIL.Emit(OpCodes.Ldfld, valueField.AsFieldInfo());
                    methodIL.Emit(OpCodes.Ldfld, _ctx.StructFields[embeddedField].AsFieldInfo());
                }
                else
                {
                    // Direct method: load _value as the receiver
                    methodIL.Emit(OpCodes.Ldarg_0);
                    methodIL.Emit(OpCodes.Ldfld, valueField.AsFieldInfo());
                }

                // Load method arguments
                for (int i = 0; i < ifaceMethod.Parameters.Count; i++)
                    methodIL.Emit(OpCodes.Ldarg, i + 1);

                // Call the static Go method
                methodIL.Emit(OpCodes.Call, goMethod.AsMethodInfo());
                methodIL.Emit(OpCodes.Ret);
            }

            // If this wraps the error interface, override ToString() to call Error()
            if (interfaceType.Name == "error" && interfaceType.Methods.Count == 1
                && interfaceType.Methods[0].Name == "Error")
            {
                var toStringBuilder = wrapperBuilder.DefineMethod(
                    "ToString",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    typeof(string),
                    Type.EmptyTypes);
                var tsIL = toStringBuilder.GetILWriter();

                // Find the Error() method — check direct, pointer element, then promoted
                var errorMethod = concreteType.LookupMethod("Error");
                FieldSymbol? errorEmbeddedField = null;
                if (errorMethod == null && concreteType is PointerTypeSymbol errorPtrType)
                {
                    errorMethod = errorPtrType.ElementType.LookupMethod("Error");
                }
                if (errorMethod == null)
                {
                    var errorStructType = concreteType is PointerTypeSymbol errorPtrStruct
                        ? errorPtrStruct.ElementType as StructTypeSymbol
                        : concreteType as StructTypeSymbol;
                    if (errorStructType != null)
                    {
                        var promoted = errorStructType.LookupPromotedMethod("Error");
                        if (promoted != null)
                        {
                            errorMethod = promoted.Method;
                            errorEmbeddedField = promoted.EmbeddedField;
                        }
                    }
                }

                if (errorMethod != null && _ctx.Methods.TryGetValue(errorMethod, out var goErrorMethod))
                {
                    tsIL.Emit(OpCodes.Ldarg_0);
                    tsIL.Emit(OpCodes.Ldfld, valueField.AsFieldInfo());
                    if (errorEmbeddedField != null)
                        tsIL.Emit(OpCodes.Ldfld, _ctx.StructFields[errorEmbeddedField].AsFieldInfo());
                    tsIL.Emit(OpCodes.Call, goErrorMethod.AsMethodInfo());
                    tsIL.Emit(OpCodes.Ret);
                }
                else
                {
                    tsIL.Emit(OpCodes.Ldstr, "");
                    tsIL.Emit(OpCodes.Ret);
                }
            }

            Type wrapperType;
            try
            {
                wrapperType = wrapperBuilder.CreateType()!;
            }
            catch (TypeLoadException)
            {
                // Wrapper type creation failed (method signature mismatch) — register as failed and return object
                _ctx.WrapperTypes[key] = new WrapperTypeInfo(typeof(object), typeof(object).GetConstructors()[0]);
                return typeof(object);
            }
            var ctor = wrapperType.GetConstructor(new[] { concreteClrType })!;
            _ctx.WrapperTypes[key] = new WrapperTypeInfo(wrapperType, ctor);

            return wrapperType;
        }

        public void EmitStringerOverrides(System.Collections.Generic.IReadOnlyList<MethodDeclaration> methods)
        {
            // No-op: stringer overrides are now emitted inline during EmitStructType
        }
    }
}

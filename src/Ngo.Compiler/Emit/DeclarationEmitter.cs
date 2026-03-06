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

        private void EmitStructType(StructTypeSymbol structType)
        {
            var typeVisibility = (_ctx.Options.IsLibrary && !_ctx.IsExported(structType.Name))
                ? TypeAttributes.NotPublic
                : TypeAttributes.Public;
            var typeBuilder = _ctx.Module.DefineType(
                _ctx.QualifyName(structType.Name),
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

            // Register early so self-referential fields (e.g. Next *Node) can resolve
            _ctx.Mapper.Register(structType, typeBuilder);

            foreach (var field in structType.Fields)
            {
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
            // This enables fmt.Stringer dispatch via FormatValue reflection
            var stringMethod = structType.LookupMethod("String");
            if (stringMethod != null && stringMethod.Parameters.Count == 0
                && stringMethod.ReturnTypes.Count == 1
                && stringMethod.ReturnTypes[0].TypeKind == TypeKind.String)
            {
                // Pre-create the static method that will hold the String() body
                var staticMethodName = structType.Name + "_String";
                var staticStringMethod = _ctx.PackageType.DefineMethod(staticMethodName,
                    MethodAttributes.Public | MethodAttributes.Static,
                    typeof(string), new[] { typeBuilder });
                _ctx.Methods[stringMethod] = staticStringMethod;

                // Define ToString() override on the struct that delegates to the static method
                var toString = typeBuilder.DefineMethod("ToString",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    typeof(string), Type.EmptyTypes);
                var il = toString.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldobj, typeBuilder);
                il.Emit(OpCodes.Call, staticStringMethod);
                il.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(toString,
                    typeof(object).GetMethod("ToString")!);
            }

            _ctx.StructTypes[structType] = typeBuilder;
        }

        private void EmitInterfaceType(InterfaceTypeSymbol interfaceType)
        {
            var typeVisibility = (_ctx.Options.IsLibrary && !_ctx.IsExported(interfaceType.Name))
                ? TypeAttributes.NotPublic
                : TypeAttributes.Public;
            var typeBuilder = _ctx.Module.DefineType(
                _ctx.QualifyName(interfaceType.Name),
                typeVisibility | TypeAttributes.Interface | TypeAttributes.Abstract);

            foreach (var method in interfaceType.Methods)
            {
                var paramTypes = new Type[method.Parameters.Count];
                for (int i = 0; i < method.Parameters.Count; i++)
                {
                    paramTypes[i] = _ctx.Mapper.Map(method.Parameters[i].Type);
                }

                var returnType = _ctx.Mapper.Map(method.ReturnType);
                typeBuilder.DefineMethod(
                    method.Name,
                    MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
                    returnType,
                    paramTypes);
            }

            _ctx.Mapper.Register(interfaceType, typeBuilder);
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
            GenericTypeParameterBuilder genericParam,
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
            var key = (concreteType, interfaceType);
            if (_ctx.WrapperTypes.TryGetValue(key, out var cached))
                return cached.type;

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

            var ctorIL = ctorBuilder.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1);
            ctorIL.Emit(OpCodes.Stfld, valueField);
            ctorIL.Emit(OpCodes.Ret);

            // Implement each interface method by delegating to the Go static method
            foreach (var ifaceMethod in interfaceType.Methods)
            {
                var concreteMethod = concreteType.LookupMethod(ifaceMethod.Name);
                FieldSymbol? embeddedField = null;

                // Check promoted methods from embedded structs
                if (concreteMethod == null && concreteType is StructTypeSymbol structType)
                {
                    var promoted = structType.LookupPromotedMethod(ifaceMethod.Name);
                    if (promoted != null)
                    {
                        concreteMethod = promoted.Value.method;
                        embeddedField = promoted.Value.embeddedField;
                    }
                }

                if (concreteMethod == null)
                    continue;

                if (!_ctx.Methods.TryGetValue(concreteMethod, out var goMethod))
                    continue;

                var paramTypes = new Type[ifaceMethod.Parameters.Count];
                for (int i = 0; i < ifaceMethod.Parameters.Count; i++)
                    paramTypes[i] = _ctx.Mapper.Map(ifaceMethod.Parameters[i].Type);

                var returnType = _ctx.Mapper.Map(ifaceMethod.ReturnType);

                var methodBuilder = wrapperBuilder.DefineMethod(
                    ifaceMethod.Name,
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    returnType,
                    paramTypes);

                var methodIL = methodBuilder.GetILGenerator();

                if (embeddedField != null)
                {
                    // Promoted method: load _value then access the embedded field
                    methodIL.Emit(OpCodes.Ldarg_0);
                    methodIL.Emit(OpCodes.Ldfld, valueField);
                    methodIL.Emit(OpCodes.Ldfld, _ctx.StructFields[embeddedField]);
                }
                else
                {
                    // Direct method: load _value as the receiver
                    methodIL.Emit(OpCodes.Ldarg_0);
                    methodIL.Emit(OpCodes.Ldfld, valueField);
                }

                // Load method arguments
                for (int i = 0; i < ifaceMethod.Parameters.Count; i++)
                    methodIL.Emit(OpCodes.Ldarg, i + 1);

                // Call the static Go method
                methodIL.Emit(OpCodes.Call, goMethod);
                methodIL.Emit(OpCodes.Ret);
            }

            // If this wraps the error interface, override ToString() to call Error()
            if (ReferenceEquals(interfaceType, BuiltinTypes.Error))
            {
                var toStringBuilder = wrapperBuilder.DefineMethod(
                    "ToString",
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                    typeof(string),
                    Type.EmptyTypes);
                var tsIL = toStringBuilder.GetILGenerator();

                // Find the Error() method — check direct then promoted
                var errorMethod = concreteType.LookupMethod("Error");
                FieldSymbol? errorEmbeddedField = null;
                if (errorMethod == null && concreteType is StructTypeSymbol errorStructType)
                {
                    var promoted = errorStructType.LookupPromotedMethod("Error");
                    if (promoted != null)
                    {
                        errorMethod = promoted.Value.method;
                        errorEmbeddedField = promoted.Value.embeddedField;
                    }
                }

                if (errorMethod != null && _ctx.Methods.TryGetValue(errorMethod, out var goErrorMethod))
                {
                    tsIL.Emit(OpCodes.Ldarg_0);
                    tsIL.Emit(OpCodes.Ldfld, valueField);
                    if (errorEmbeddedField != null)
                        tsIL.Emit(OpCodes.Ldfld, _ctx.StructFields[errorEmbeddedField]);
                    tsIL.Emit(OpCodes.Call, goErrorMethod);
                    tsIL.Emit(OpCodes.Ret);
                }
                else
                {
                    tsIL.Emit(OpCodes.Ldstr, "");
                    tsIL.Emit(OpCodes.Ret);
                }
            }

            var wrapperType = wrapperBuilder.CreateType()!;
            var ctor = wrapperType.GetConstructor(new[] { concreteClrType })!;
            _ctx.WrapperTypes[key] = (wrapperType, ctor);

            return wrapperType;
        }

        public void EmitStringerOverrides(System.Collections.Generic.IReadOnlyList<MethodDeclaration> methods)
        {
            // No-op: stringer overrides are now emitted inline during EmitStructType
        }
    }
}

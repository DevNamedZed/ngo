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
            var typeBuilder = _ctx.Module.DefineType(
                structType.Name,
                TypeAttributes.Public | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
                typeof(System.ValueType));

            // Register early so self-referential fields (e.g. Next *Node) can resolve
            _ctx.Mapper.Register(structType, typeBuilder);

            foreach (var field in structType.Fields)
            {
                var fieldType = _ctx.Mapper.Map(field.Type);
                var fb = typeBuilder.DefineField(field.Name, fieldType, FieldAttributes.Public);
                _ctx.StructFields[field] = fb;
            }
            _ctx.StructTypes[structType] = typeBuilder;
        }

        private void EmitInterfaceType(InterfaceTypeSymbol interfaceType)
        {
            var typeBuilder = _ctx.Module.DefineType(
                interfaceType.Name,
                TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);

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
            var parameters = func.Symbol.Parameters;
            var paramTypes = new Type[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                paramTypes[i] = _ctx.Mapper.Map(parameters[i].Type);
            }

            var returnType = _ctx.Mapper.MapReturnType(func.Symbol.ReturnTypes);

            // Go allows multiple init() functions — give each a unique IL name
            var methodName = func.Symbol.Name;
            if (methodName == "init")
            {
                methodName = "init$" + _initCounter++;
            }

            var method = _ctx.PackageType.DefineMethod(
                methodName,
                MethodAttributes.Public | MethodAttributes.Static,
                returnType,
                paramTypes);

            for (int i = 0; i < parameters.Count; i++)
            {
                method.DefineParameter(i + 1, ParameterAttributes.None, parameters[i].Name);
            }

            _ctx.Methods[func.Symbol] = method;
        }

        public void EmitMethod(MethodDeclaration decl)
        {
            // Methods are emitted as static methods on the package type
            // with the receiver as the first parameter
            var receiverType = _ctx.Mapper.Map(decl.Receiver.Type);
            var parameters = decl.Symbol.Parameters;
            var paramTypes = new Type[parameters.Count + 1];
            paramTypes[0] = receiverType;
            for (int i = 0; i < parameters.Count; i++)
            {
                paramTypes[i + 1] = _ctx.Mapper.Map(parameters[i].Type);
            }

            var returnType = _ctx.Mapper.MapReturnType(decl.Symbol.ReturnTypes);

            // Name format: ReceiverType_MethodName
            var methodName = $"{decl.Symbol.ReceiverType.Name}_{decl.Symbol.Name}";
            var method = _ctx.PackageType.DefineMethod(
                methodName,
                MethodAttributes.Public | MethodAttributes.Static,
                returnType,
                paramTypes);

            method.DefineParameter(1, ParameterAttributes.None, decl.Receiver.Name);
            for (int i = 0; i < parameters.Count; i++)
            {
                method.DefineParameter(i + 2, ParameterAttributes.None, parameters[i].Name);
            }

            _ctx.Methods[decl.Symbol] = method;
        }

        public void EmitPackageVar(VarDeclaration decl)
        {
            var fieldType = _ctx.Mapper.Map(decl.Symbol.Type);
            var field = _ctx.PackageType.DefineField(
                decl.Symbol.Name,
                fieldType,
                FieldAttributes.Public | FieldAttributes.Static);
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
                wrapperName,
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
    }
}

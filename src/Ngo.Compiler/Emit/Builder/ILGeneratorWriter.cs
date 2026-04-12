// -----------------------------------------------------------------------
// <copyright file="ILGeneratorWriter.cs" company="Ziad">
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

namespace Ngo.Compiler.Emit.Builder
{
    /// <summary>
    /// CilWriter that forwards all calls to a real ILGenerator.
    /// Used for main package emission (ngo run / ngo build).
    /// </summary>
    internal sealed class ILGeneratorWriter : CilWriter
    {
        private readonly ILGenerator _il;

        public ILGeneratorWriter(ILGenerator il) => _il = il;

        public override void Emit(OpCode op) => _il.Emit(op);
        public override void Emit(OpCode op, int arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, long arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, float arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, double arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, string arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, byte arg) => _il.Emit(op, arg);
        public override void Emit(OpCode op, Type type) => _il.Emit(op, type);

        public override void Emit(OpCode op, MethodInfo method)
        {
            _il.Emit(op, ResolveMethodForEmit(method));
        }

        public override void Emit(OpCode op, ConstructorInfo ctor)
        {
            _il.Emit(op, ResolveConstructorForEmit(ctor));
        }

        public override void Emit(OpCode op, FieldInfo field)
        {
            _il.Emit(op, ResolveFieldForEmit(field));
        }

        public override void Emit(OpCode op, Label label) => _il.Emit(op, label);
        public override void Emit(OpCode op, Label[] labels) => _il.Emit(op, labels);
        public override void Emit(OpCode op, LocalBuilder local) => _il.Emit(op, local);

        public override LocalBuilder DeclareLocal(Type type) => _il.DeclareLocal(type);
        public override Label DefineLabel() => _il.DefineLabel();
        public override void MarkLabel(Label label) => _il.MarkLabel(label);

        public override void BeginExceptionBlock() => _il.BeginExceptionBlock();
        public override void BeginCatchBlock(Type type) => _il.BeginCatchBlock(type);
        public override void BeginFinallyBlock() => _il.BeginFinallyBlock();
        public override void BeginFaultBlock() => _il.BeginFaultBlock();
        public override void BeginExceptFilterBlock() => _il.BeginExceptFilterBlock();
        public override void EndExceptionBlock() => _il.EndExceptionBlock();

        private static bool IsTypeBuilderGenericInstantiation(Type type)
        {
            if (!type.IsGenericType || type.IsGenericTypeDefinition)
            {
                return false;
            }

            if (type.GetGenericTypeDefinition() is TypeBuilder)
            {
                return true;
            }

            foreach (var argument in type.GetGenericArguments())
            {
                if (argument is TypeBuilder || argument is GenericTypeParameterBuilder)
                {
                    return true;
                }
                if (argument.IsGenericType && IsTypeBuilderGenericInstantiation(argument))
                {
                    return true;
                }
            }
            return false;
        }

        private static ConstructorInfo ResolveConstructorForEmit(ConstructorInfo constructor)
        {
            var declaringType = constructor.DeclaringType;
            if (declaringType == null || !IsTypeBuilderGenericInstantiation(declaringType))
            {
                return constructor;
            }

            var genericDefinition = declaringType.GetGenericTypeDefinition();

            try
            {
                var parameterCount = constructor.GetParameters().Length;
                foreach (var baseConstructor in genericDefinition.GetConstructors(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (baseConstructor.GetParameters().Length == parameterCount)
                    {
                        return TypeBuilder.GetConstructor(declaringType, baseConstructor);
                    }
                }
            }
            catch (NotSupportedException)
            {
            }

            return constructor;
        }

        private static MethodInfo ResolveMethodForEmit(MethodInfo method)
        {
            var declaringType = method.DeclaringType;
            if (declaringType == null || !IsTypeBuilderGenericInstantiation(declaringType))
            {
                return method;
            }

            var genericDefinition = declaringType.GetGenericTypeDefinition();

            if (genericDefinition is TypeBuilder typeBuilderDef)
            {
                var baseMethod = FindMethodOnTypeBuilder(typeBuilderDef, method.Name);
                if (baseMethod != null)
                {
                    return TypeBuilder.GetMethod(declaringType, baseMethod);
                }
                return method;
            }

            try
            {
                var sourceParams = method.GetParameters();
                MethodInfo? fallbackMatch = null;
                foreach (var baseMethod in genericDefinition.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (baseMethod.Name != method.Name || baseMethod.GetParameters().Length != sourceParams.Length)
                    {
                        continue;
                    }
                    // When multiple overloads match by name+count, compare parameter shapes
                    // to pick the right one (e.g. Append(Slice<T>,Slice<T>) vs Append(Slice<T>,T[])).
                    var baseParams = baseMethod.GetParameters();
                    bool shapesMatch = true;
                    for (int pi = 0; pi < sourceParams.Length; pi++)
                    {
                        if (!ParameterShapeMatches(baseParams[pi].ParameterType, sourceParams[pi].ParameterType))
                        {
                            shapesMatch = false;
                            break;
                        }
                    }
                    if (shapesMatch)
                    {
                        return TypeBuilder.GetMethod(declaringType, baseMethod);
                    }
                    fallbackMatch ??= baseMethod;
                }
                if (fallbackMatch != null)
                {
                    return TypeBuilder.GetMethod(declaringType, fallbackMatch);
                }
            }
            catch (NotSupportedException)
            {
            }

            return method;
        }

        /// <summary>
        /// Compares the shape of a base (open-generic) parameter type against the source
        /// method's parameter type to disambiguate overloads. For example, distinguishes
        /// Append(Slice&lt;T&gt;, T[]) from Append(Slice&lt;T&gt;, Slice&lt;T&gt;).
        /// </summary>
        private static bool ParameterShapeMatches(Type baseParamType, Type sourceParamType)
        {
            // Generic parameter (!0, !1) — matches anything at this level
            if (baseParamType.IsGenericParameter)
            {
                return true;
            }
            // Array (!0[]) — source must also be array-shaped
            if (baseParamType.IsArray)
            {
                return sourceParamType.IsArray;
            }
            // Generic type (Slice<!0>) — source must be generic with matching definition
            if (baseParamType.IsGenericType)
            {
                if (!sourceParamType.IsGenericType)
                {
                    return false;
                }
                return baseParamType.GetGenericTypeDefinition() == sourceParamType.GetGenericTypeDefinition();
            }
            // Concrete types — match directly
            return baseParamType == sourceParamType;
        }

        private static MethodInfo? FindMethodOnTypeBuilder(TypeBuilder typeBuilder, string methodName)
        {
            try
            {
                foreach (var definedMethod in typeBuilder.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (definedMethod.Name == methodName)
                    {
                        return definedMethod;
                    }
                }
            }
            catch (NotSupportedException)
            {
            }

            return null;
        }

        private static FieldInfo ResolveFieldForEmit(FieldInfo field)
        {
            var declaringType = field.DeclaringType;
            if (declaringType == null || !IsTypeBuilderGenericInstantiation(declaringType))
            {
                return field;
            }

            var genericDefinition = declaringType.GetGenericTypeDefinition();
            foreach (var baseField in genericDefinition.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (baseField.Name == field.Name)
                {
                    return TypeBuilder.GetField(declaringType, baseField);
                }
            }

            return field;
        }
    }
}

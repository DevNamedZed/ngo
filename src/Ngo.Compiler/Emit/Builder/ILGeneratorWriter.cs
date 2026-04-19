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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Ngo.Compiler.Emit.Refs;

namespace Ngo.Compiler.Emit.Builder
{
    /// <summary>
    /// CilWriter that forwards all calls to a real ILGenerator.
    /// Maintains private LocalBuilder/Label instances indexed by LocalSlot.Index / LabelSlot.Id.
    /// </summary>
    internal sealed class ILGeneratorWriter : CilWriter
    {
        private readonly ILGenerator _il;
        private readonly List<LocalBuilder> _locals = new();
        private readonly List<Label> _labels = new();

        public ILGeneratorWriter(ILGenerator il)
        {
            _il = il ?? throw new ArgumentNullException(nameof(il));
        }

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

        public override void Emit(OpCode op, LabelSlot label)
        {
            _il.Emit(op, GetLabel(label));
        }

        public override void Emit(OpCode op, LabelSlot[] labels)
        {
            var resolved = new Label[labels.Length];
            for (int index = 0; index < labels.Length; index++)
            {
                resolved[index] = GetLabel(labels[index]);
            }
            _il.Emit(op, resolved);
        }

        public override void Emit(OpCode op, LocalSlot local)
        {
            _il.Emit(op, GetLocal(local));
        }

        public override void Emit(OpCode op, TypeRef typeRef)
        {
            _il.Emit(op, ResolveTypeRef(typeRef));
        }

        public override void Emit(OpCode op, MethodRef methodRef)
        {
            _il.Emit(op, ResolveMethodForEmit(ResolveMethodRef(methodRef)));
        }

        public override void Emit(OpCode op, CtorRef ctorRef)
        {
            _il.Emit(op, ResolveConstructorForEmit(ResolveCtorRef(ctorRef)));
        }

        public override void Emit(OpCode op, FieldRef fieldRef)
        {
            _il.Emit(op, ResolveFieldForEmit(ResolveFieldRef(fieldRef)));
        }

        public override LocalSlot DeclareLocal(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            var builder = _il.DeclareLocal(type);
            var slot = new LocalSlot(_locals.Count, type);
            _locals.Add(builder);
            return slot;
        }

        public override LabelSlot DefineLabel()
        {
            var label = _il.DefineLabel();
            var slot = new LabelSlot(_labels.Count);
            _labels.Add(label);
            return slot;
        }

        public override void MarkLabel(LabelSlot label)
        {
            _il.MarkLabel(GetLabel(label));
        }

        public override void BeginExceptionBlock()
        {
            _il.BeginExceptionBlock();
        }

        public override void BeginCatchBlock(Type type)
        {
            _il.BeginCatchBlock(type);
        }

        public override void BeginFinallyBlock() => _il.BeginFinallyBlock();
        public override void BeginFaultBlock() => _il.BeginFaultBlock();
        public override void BeginExceptFilterBlock() => _il.BeginExceptFilterBlock();
        public override void EndExceptionBlock() => _il.EndExceptionBlock();

        private LocalBuilder GetLocal(LocalSlot slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }
            if (slot.Index < 0 || slot.Index >= _locals.Count)
            {
                throw new InvalidOperationException(
                    $"ILGeneratorWriter: local slot {slot.Index} is out of range (declared {_locals.Count})");
            }
            return _locals[slot.Index];
        }

        private Label GetLabel(LabelSlot slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }
            if (slot.Id < 0 || slot.Id >= _labels.Count)
            {
                throw new InvalidOperationException(
                    $"ILGeneratorWriter: label slot {slot.Id} is out of range (defined {_labels.Count})");
            }
            return _labels[slot.Id];
        }

        private static Type ResolveTypeRef(TypeRef typeRef)
        {
            if (typeRef == null)
            {
                throw new ArgumentNullException(nameof(typeRef));
            }
            switch (typeRef.Kind)
            {
                case TypeRefKind.Runtime:
                {
                    return typeRef.RuntimeType!;
                }
                case TypeRefKind.Builder:
                {
                    return typeRef.Builder!.AsType();
                }
                case TypeRefKind.Array:
                {
                    return ResolveTypeRef(typeRef.ElementType!).MakeArrayType();
                }
                case TypeRefKind.Pointer:
                {
                    return ResolveTypeRef(typeRef.ElementType!).MakePointerType();
                }
                case TypeRefKind.ByRef:
                {
                    return ResolveTypeRef(typeRef.ElementType!).MakeByRefType();
                }
                case TypeRefKind.GenericInstantiation:
                {
                    var definition = ResolveTypeRef(typeRef.GenericDefinition!);
                    var argumentCount = typeRef.GenericArguments.Length;
                    var arguments = new Type[argumentCount];
                    for (int index = 0; index < argumentCount; index++)
                    {
                        arguments[index] = ResolveTypeRef(typeRef.GenericArguments[index]);
                    }
                    return definition.MakeGenericType(arguments);
                }
                default:
                {
                    throw new NotSupportedException(
                        $"ILGeneratorWriter cannot resolve TypeRef kind '{typeRef.Kind}' in the live emit path");
                }
            }
        }

        private static MethodInfo ResolveMethodRef(MethodRef methodRef)
        {
            if (methodRef == null)
            {
                throw new ArgumentNullException(nameof(methodRef));
            }
            switch (methodRef.Kind)
            {
                case MethodRefKind.Runtime:
                {
                    return methodRef.RuntimeMethod!;
                }
                case MethodRefKind.Defined:
                {
                    if (methodRef.Builder is LiveMethodBuilder liveBuilder)
                    {
                        if (methodRef.DeclaringType?.Kind == TypeRefKind.GenericInstantiation)
                        {
                            var closedType = ResolveTypeRef(methodRef.DeclaringType);
                            return TypeBuilder.GetMethod(closedType, liveBuilder.Inner);
                        }
                        return liveBuilder.Inner;
                    }
                    throw new NotSupportedException(
                        "ILGeneratorWriter cannot resolve MethodRef: builder is not a LiveMethodBuilder");
                }
                case MethodRefKind.GenericInstantiation:
                {
                    var definition = ResolveMethodRef(methodRef.GenericDefinition!);
                    var argumentCount = methodRef.GenericTypeArguments.Length;
                    var arguments = new Type[argumentCount];
                    for (int index = 0; index < argumentCount; index++)
                    {
                        arguments[index] = ResolveTypeRef(methodRef.GenericTypeArguments[index]);
                    }
                    return definition.MakeGenericMethod(arguments);
                }
                default:
                {
                    throw new NotSupportedException(
                        $"ILGeneratorWriter cannot resolve MethodRef kind '{methodRef.Kind}' in the live emit path");
                }
            }
        }

        private static ConstructorInfo ResolveCtorRef(CtorRef ctorRef)
        {
            if (ctorRef == null)
            {
                throw new ArgumentNullException(nameof(ctorRef));
            }
            switch (ctorRef.Kind)
            {
                case CtorRefKind.Runtime:
                {
                    return ctorRef.RuntimeConstructor!;
                }
                case CtorRefKind.Defined:
                {
                    if (ctorRef.Builder is LiveConstructorBuilder liveBuilder)
                    {
                        if (ctorRef.DeclaringType?.Kind == TypeRefKind.GenericInstantiation)
                        {
                            var closedType = ResolveTypeRef(ctorRef.DeclaringType);
                            return TypeBuilder.GetConstructor(closedType, liveBuilder.Inner);
                        }
                        return liveBuilder.Inner;
                    }
                    throw new NotSupportedException(
                        "ILGeneratorWriter cannot resolve CtorRef: builder is not a LiveConstructorBuilder");
                }
                default:
                {
                    throw new NotSupportedException(
                        $"ILGeneratorWriter cannot resolve CtorRef kind '{ctorRef.Kind}' in the live emit path");
                }
            }
        }

        private static FieldInfo ResolveFieldRef(FieldRef fieldRef)
        {
            if (fieldRef == null)
            {
                throw new ArgumentNullException(nameof(fieldRef));
            }
            switch (fieldRef.Kind)
            {
                case FieldRefKind.Runtime:
                {
                    return fieldRef.RuntimeField!;
                }
                case FieldRefKind.Defined:
                {
                    if (fieldRef.Builder is LiveFieldBuilder liveBuilder)
                    {
                        if (fieldRef.DeclaringType?.Kind == TypeRefKind.GenericInstantiation)
                        {
                            var closedType = ResolveTypeRef(fieldRef.DeclaringType);
                            return TypeBuilder.GetField(closedType, liveBuilder.Inner);
                        }
                        return liveBuilder.Inner;
                    }
                    throw new NotSupportedException(
                        "ILGeneratorWriter cannot resolve FieldRef: builder is not a LiveFieldBuilder");
                }
                default:
                {
                    throw new NotSupportedException(
                        $"ILGeneratorWriter cannot resolve FieldRef kind '{fieldRef.Kind}' in the live emit path");
                }
            }
        }

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

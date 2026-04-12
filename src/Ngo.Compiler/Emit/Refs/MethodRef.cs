// -----------------------------------------------------------------------
// <copyright file="MethodRef.cs" company="Ziad">
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
using Ngo.Compiler.Emit.Builder;

namespace Ngo.Compiler.Emit.Refs
{
    /// <summary>
    /// A structured reference to a method used during emission.
    /// Carries a real MethodInfo (for runtime references), a builder (for definitions we own),
    /// a generic instantiation of another MethodRef, or a MemberRef pointing at a method on a
    /// type declared in another package (ECMA-335 MemberRef).
    /// </summary>
    internal sealed class MethodRef
    {
        public MethodRefKind Kind { get; }
        public MethodInfo? RuntimeMethod { get; }
        public IMethodBuilder? Builder { get; }
        public TypeRef? DeclaringType { get; }
        public MethodRef? GenericDefinition { get; }
        public TypeRef[] GenericTypeArguments { get; }
        public string? MemberName { get; }
        public TypeRef[] MemberParameterTypes { get; }
        public TypeRef? MemberReturnType { get; }
        public bool MemberIsStatic { get; }

        private MethodRef(MethodRefKind kind, MethodInfo? runtimeMethod = null,
            IMethodBuilder? builder = null, TypeRef? declaringType = null,
            MethodRef? genericDefinition = null, TypeRef[]? genericTypeArguments = null,
            string? memberName = null, TypeRef[]? memberParameterTypes = null,
            TypeRef? memberReturnType = null, bool memberIsStatic = false)
        {
            Kind = kind;
            RuntimeMethod = runtimeMethod;
            Builder = builder;
            DeclaringType = declaringType;
            GenericDefinition = genericDefinition;
            GenericTypeArguments = genericTypeArguments ?? Array.Empty<TypeRef>();
            MemberName = memberName;
            MemberParameterTypes = memberParameterTypes ?? Array.Empty<TypeRef>();
            MemberReturnType = memberReturnType;
            MemberIsStatic = memberIsStatic;
        }

        public static MethodRef FromRuntime(MethodInfo runtimeMethod)
        {
            if (runtimeMethod == null)
            {
                throw new ArgumentNullException(nameof(runtimeMethod));
            }
            return new MethodRef(MethodRefKind.Runtime, runtimeMethod: runtimeMethod);
        }

        public static MethodRef FromBuilder(IMethodBuilder builder, TypeRef declaringType)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (declaringType == null)
            {
                throw new ArgumentNullException(nameof(declaringType));
            }
            return new MethodRef(MethodRefKind.Defined, builder: builder, declaringType: declaringType);
        }

        public static MethodRef MemberRef(TypeRef declaringType, string name,
            TypeRef[] parameterTypes, TypeRef returnType, bool isStatic)
        {
            if (declaringType == null)
            {
                throw new ArgumentNullException(nameof(declaringType));
            }
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name cannot be null or empty", nameof(name));
            }
            if (parameterTypes == null)
            {
                throw new ArgumentNullException(nameof(parameterTypes));
            }
            if (returnType == null)
            {
                throw new ArgumentNullException(nameof(returnType));
            }
            return new MethodRef(MethodRefKind.MemberRef,
                declaringType: declaringType,
                memberName: name,
                memberParameterTypes: parameterTypes,
                memberReturnType: returnType,
                memberIsStatic: isStatic);
        }

        public MethodRef MakeGenericMethod(TypeRef[] typeArguments)
        {
            if (typeArguments == null)
            {
                throw new ArgumentNullException(nameof(typeArguments));
            }
            if (Kind == MethodRefKind.GenericInstantiation)
            {
                throw new InvalidOperationException(
                    "MethodRef.MakeGenericMethod called on an already-instantiated method reference");
            }
            return new MethodRef(MethodRefKind.GenericInstantiation,
                genericDefinition: this, genericTypeArguments: typeArguments);
        }

        public string Name
        {
            get
            {
                switch (Kind)
                {
                    case MethodRefKind.Runtime:
                    {
                        return RuntimeMethod!.Name;
                    }
                    case MethodRefKind.Defined:
                    {
                        return Builder!.Name;
                    }
                    case MethodRefKind.GenericInstantiation:
                    {
                        return GenericDefinition!.Name;
                    }
                    case MethodRefKind.MemberRef:
                    {
                        return MemberName!;
                    }
                    default:
                    {
                        throw new InvalidOperationException($"Unknown MethodRefKind: {Kind}");
                    }
                }
            }
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case MethodRefKind.Runtime:
                {
                    return RuntimeMethod!.DeclaringType?.FullName + "." + RuntimeMethod.Name;
                }
                case MethodRefKind.Defined:
                {
                    return DeclaringType!.DisplayName + "." + Builder!.Name;
                }
                case MethodRefKind.GenericInstantiation:
                {
                    var argNames = new string[GenericTypeArguments.Length];
                    for (int index = 0; index < GenericTypeArguments.Length; index++)
                    {
                        argNames[index] = GenericTypeArguments[index].DisplayName;
                    }
                    return GenericDefinition!.ToString() + "<" + string.Join(",", argNames) + ">";
                }
                case MethodRefKind.MemberRef:
                {
                    return DeclaringType!.DisplayName + "." + MemberName;
                }
                default:
                {
                    return Kind.ToString();
                }
            }
        }
    }
}

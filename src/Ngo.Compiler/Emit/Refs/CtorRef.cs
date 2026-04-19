// -----------------------------------------------------------------------
// <copyright file="CtorRef.cs" company="Ziad">
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
    /// A structured reference to a constructor used during emission.
    /// Carries a real ConstructorInfo (for runtime references), a builder (for constructors we own),
    /// or a MemberRef pointing at a constructor on a generic instantiation whose arguments cannot be
    /// resolved through <see cref="System.Reflection.Emit.TypeBuilder"/>.GetConstructor (for example
    /// when one of the arguments is an archive-mode NgoBuilderType).
    /// </summary>
    internal sealed class CtorRef
    {
        public CtorRefKind Kind { get; }
        public ConstructorInfo? RuntimeConstructor { get; }
        public IConstructorBuilder? Builder { get; }
        public TypeRef? DeclaringType { get; }
        public TypeRef[] MemberParameterTypes { get; }

        private CtorRef(CtorRefKind kind, ConstructorInfo? runtimeConstructor = null,
            IConstructorBuilder? builder = null, TypeRef? declaringType = null,
            TypeRef[]? memberParameterTypes = null)
        {
            Kind = kind;
            RuntimeConstructor = runtimeConstructor;
            Builder = builder;
            DeclaringType = declaringType;
            MemberParameterTypes = memberParameterTypes ?? Array.Empty<TypeRef>();
        }

        public static CtorRef FromRuntime(ConstructorInfo runtimeConstructor)
        {
            if (runtimeConstructor == null)
            {
                throw new ArgumentNullException(nameof(runtimeConstructor));
            }
            return new CtorRef(CtorRefKind.Runtime, runtimeConstructor: runtimeConstructor);
        }

        public static CtorRef FromBuilder(IConstructorBuilder builder, TypeRef declaringType)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (declaringType == null)
            {
                throw new ArgumentNullException(nameof(declaringType));
            }
            return new CtorRef(CtorRefKind.Defined, builder: builder, declaringType: declaringType);
        }

        public static CtorRef MemberRef(TypeRef declaringType, TypeRef[] parameterTypes)
        {
            if (declaringType == null)
            {
                throw new ArgumentNullException(nameof(declaringType));
            }
            if (parameterTypes == null)
            {
                throw new ArgumentNullException(nameof(parameterTypes));
            }
            return new CtorRef(CtorRefKind.MemberRef,
                declaringType: declaringType,
                memberParameterTypes: parameterTypes);
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case CtorRefKind.Runtime:
                {
                    return RuntimeConstructor!.DeclaringType?.FullName + "..ctor";
                }
                case CtorRefKind.Defined:
                {
                    return DeclaringType!.DisplayName + "..ctor";
                }
                case CtorRefKind.MemberRef:
                {
                    return DeclaringType!.DisplayName + "..ctor";
                }
                default:
                {
                    return Kind.ToString();
                }
            }
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="FieldRef.cs" company="Ziad">
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
    /// A structured reference to a field used during emission.
    /// Carries a real FieldInfo (for runtime references), a builder (for fields we own), or a
    /// MemberRef pointing at a field on a type declared in another package (ECMA-335 MemberRef).
    /// </summary>
    internal sealed class FieldRef
    {
        public FieldRefKind Kind { get; }
        public FieldInfo? RuntimeField { get; }
        public IFieldBuilder? Builder { get; }
        public TypeRef? DeclaringType { get; }
        public string? MemberName { get; }
        public TypeRef? MemberFieldType { get; }

        private FieldRef(FieldRefKind kind, FieldInfo? runtimeField = null,
            IFieldBuilder? builder = null, TypeRef? declaringType = null,
            string? memberName = null, TypeRef? memberFieldType = null)
        {
            Kind = kind;
            RuntimeField = runtimeField;
            Builder = builder;
            DeclaringType = declaringType;
            MemberName = memberName;
            MemberFieldType = memberFieldType;
        }

        public static FieldRef FromRuntime(FieldInfo runtimeField)
        {
            if (runtimeField == null)
            {
                throw new ArgumentNullException(nameof(runtimeField));
            }
            return new FieldRef(FieldRefKind.Runtime, runtimeField: runtimeField);
        }

        public static FieldRef FromBuilder(IFieldBuilder builder, TypeRef declaringType)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (declaringType == null)
            {
                throw new ArgumentNullException(nameof(declaringType));
            }
            return new FieldRef(FieldRefKind.Defined, builder: builder, declaringType: declaringType);
        }

        public static FieldRef MemberRef(TypeRef declaringType, string name, TypeRef fieldType)
        {
            if (declaringType == null)
            {
                throw new ArgumentNullException(nameof(declaringType));
            }
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name cannot be null or empty", nameof(name));
            }
            if (fieldType == null)
            {
                throw new ArgumentNullException(nameof(fieldType));
            }
            return new FieldRef(FieldRefKind.MemberRef,
                declaringType: declaringType,
                memberName: name,
                memberFieldType: fieldType);
        }

        public override string ToString()
        {
            switch (Kind)
            {
                case FieldRefKind.Runtime:
                {
                    return RuntimeField!.DeclaringType?.FullName + "." + RuntimeField.Name;
                }
                case FieldRefKind.Defined:
                {
                    return DeclaringType!.DisplayName + "." + Builder!.Name;
                }
                case FieldRefKind.MemberRef:
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

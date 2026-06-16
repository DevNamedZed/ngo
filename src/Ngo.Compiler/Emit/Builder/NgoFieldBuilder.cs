// -----------------------------------------------------------------------
// <copyright file="NgoFieldBuilder.cs" company="Ziad">
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
using Ngo.Compiler.Archive;
using Ngo.Compiler.Emit.Refs;

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoFieldBuilder : IFieldBuilder
    {
        private readonly Type _declaringType;
        private readonly NgoTypeBuilder? _declaringTypeBuilder;
        private readonly string _name;
        private readonly Type _fieldType;
        private readonly FieldAttributes _attrs;

        public NgoFieldBuilder(Type declaringType, string name, Type fieldType, FieldAttributes attrs,
            NgoTypeBuilder? declaringTypeBuilder = null)
        {
            _declaringType = declaringType;
            _declaringTypeBuilder = declaringTypeBuilder;
            _name = name;
            _fieldType = fieldType;
            _attrs = attrs;
        }

        public string FieldName => _name;
        public string Name => _name;
        public FieldAttributes FieldAttributes => _attrs;
        public Type FieldType => _fieldType;
        public Type? DeclaringType => _declaringType;
        public int GoArrayLength { get; set; }
        public string? GoArrayElementTypeName { get; set; }
        public Type? GoArrayElementType { get; set; }

        // Structured, index-based field type token (the .NET VAR encoding) built from the declaring
        // type's generic context. A field has no method-level generic parameters.
        public TypeToken FieldTypeToken => BuildSignatureWriter().BuildTypeToken(_fieldType);

        // For a Go inline-array field ([N]T) the element type T is serialized as a token so the
        // array is re-synthesized at link time without parsing a bare type name (which loses a
        // generic parameter like 'K'). Null for non-array fields.
        public TypeToken? GoArrayElementTypeToken =>
            GoArrayElementType != null ? BuildSignatureWriter().BuildTypeToken(GoArrayElementType) : null;

        private NgoWriter BuildSignatureWriter()
        {
            var typeGenericParams = _declaringTypeBuilder?.GenericParamTypes ?? Type.EmptyTypes;
            return new NgoWriter(new SerializationContext(Type.EmptyTypes, typeGenericParams));
        }

        public void SetCustomAttribute(CustomAttributeBuilder attr) { }

        public FieldRef AsFieldRef()
        {
            var declaringTypeRef = _declaringTypeBuilder != null
                ? TypeRef.FromBuilder(_declaringTypeBuilder)
                : TypeRef.FromRuntime(_declaringType);
            return FieldRef.FromBuilder(this, declaringTypeRef);
        }
    }
}

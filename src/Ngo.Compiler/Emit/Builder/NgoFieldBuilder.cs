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

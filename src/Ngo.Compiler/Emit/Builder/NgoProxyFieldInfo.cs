// -----------------------------------------------------------------------
// <copyright file="NgoProxyFieldInfo.cs" company="Ziad">
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
using System.Globalization;
using System.Reflection;

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoProxyFieldInfo : FieldInfo
    {
        private readonly Type _declaringType;
        private readonly string _name;
        private readonly Type _fieldType;

        public NgoProxyFieldInfo(Type declaringType, string name, Type fieldType)
        {
            _declaringType = declaringType;
            _name = name;
            _fieldType = fieldType;
        }

        public override Type DeclaringType => _declaringType;
        public override string Name => _name;
        public override Type FieldType => _fieldType;
        public override Type ReflectedType => _declaringType;
        public override FieldAttributes Attributes => FieldAttributes.Public;
        public override RuntimeFieldHandle FieldHandle => throw new NotSupportedException();
        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
        public override object? GetValue(object? obj) => throw new NotSupportedException();
        public override bool IsDefined(Type attributeType, bool inherit) => false;
        public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, CultureInfo? culture)
            => throw new NotSupportedException();
    }
}

// -----------------------------------------------------------------------
// <copyright file="NgoProxyType.cs" company="Ziad">
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
    internal sealed class NgoProxyType : TypeDelegator
    {
        private readonly string _fullName;
        private readonly string _name;
        private readonly bool _isValueType;

        public NgoProxyType(string fullName, bool isValueType = false)
            : base(typeof(object))
        {
            _fullName = fullName;
            var dot = fullName.LastIndexOf('.');
            _name = dot >= 0 ? fullName.Substring(dot + 1) : fullName;
            _isValueType = isValueType;
        }

        public override string FullName => _fullName;
        public override string Name => _name;
        public override string Namespace => _fullName.Contains('.') ? _fullName.Substring(0, _fullName.LastIndexOf('.')) : "";
        protected override bool IsValueTypeImpl() => _isValueType;
        public override Type UnderlyingSystemType => this;
        public override int GetHashCode() => _fullName.GetHashCode();
        public override bool Equals(object? o) => o is NgoProxyType other && other._fullName == _fullName;
        public override bool Equals(Type? o) => o is NgoProxyType other && other._fullName == _fullName;
        public override string ToString() => _fullName;
    }
}

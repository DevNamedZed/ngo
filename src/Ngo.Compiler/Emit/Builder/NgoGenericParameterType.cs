// -----------------------------------------------------------------------
// <copyright file="NgoGenericParameterType.cs" company="Ziad">
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

namespace Ngo.Compiler.Emit.Builder
{
    /// <summary>
    /// Generic-parameter placeholder returned from
    /// <see cref="NgoTypeBuilder.DefineGenericParameters"/> and
    /// <see cref="NgoMethodBuilder.DefineGenericParameters"/> in the archive emit path.
    /// Carries the name, ordinal position, and whether the parameter belongs to a method
    /// (as opposed to a type). Used by the archive serializer to emit
    /// <c>GenericTypeParam</c> / <c>GenericMethodParam</c> tokens. The live-mode equivalent
    /// is <see cref="System.Reflection.Emit.GenericTypeParameterBuilder"/>.
    /// </summary>
    internal sealed class NgoGenericParameterType : TypeDelegator
    {
        private readonly string _name;
        private readonly int _index;
        private readonly bool _isMethodGenericParam;

        public NgoGenericParameterType(string name, int index, bool isMethodGenericParam)
            : base(typeof(object))
        {
            _name = name;
            _index = index;
            _isMethodGenericParam = isMethodGenericParam;
        }

        internal int Index => _index;
        internal bool IsMethodGenericParam => _isMethodGenericParam;

        public override string Name => _name;
        public override string FullName => _name;
        public override string Namespace => "";
        public override Type UnderlyingSystemType => this;
        public override bool IsGenericParameter => true;
        public override int GenericParameterPosition => _index;
        public override int GetHashCode() => HashCode.Combine(_name, _index, _isMethodGenericParam);

        public override bool Equals(object? o) =>
            o is NgoGenericParameterType other
            && other._name == _name
            && other._index == _index
            && other._isMethodGenericParam == _isMethodGenericParam;

        public override bool Equals(Type? o) =>
            o is NgoGenericParameterType other
            && other._name == _name
            && other._index == _index
            && other._isMethodGenericParam == _isMethodGenericParam;

        public override string ToString() => _name;
    }
}

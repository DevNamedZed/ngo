// -----------------------------------------------------------------------
// <copyright file="NgoBuilderType.cs" company="Ziad">
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
    /// Type reference returned from <see cref="NgoTypeBuilder.AsType"/> in the archive emit path.
    /// Carries the bookkeeping (full name, value-type flag, generic shape, element type) that
    /// consumers like ILGenerator and DefinitionTable expect on a <see cref="Type"/>, without
    /// implying a real CLR metadata row. The corresponding live-mode type is the actual
    /// <see cref="System.Reflection.Emit.TypeBuilder"/> returned by LiveTypeBuilder.AsType.
    /// </summary>
    internal sealed class NgoBuilderType : TypeDelegator
    {
        private readonly string _fullName;
        private readonly string _name;
        private readonly bool _isValueType;
        private int _genericParamCount;
        private Type[]? _genericTypeArgs;
        private Type? _genericDefinition;
        private readonly Type? _elementType;
        private readonly bool _isArray;
        private readonly bool _isPointer;
        private readonly bool _isByRef;

        public NgoBuilderType(string fullName, bool isValueType = false)
            : base(typeof(object))
        {
            _fullName = fullName;
            var lastDot = fullName.LastIndexOf('.');
            _name = lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
            _isValueType = isValueType;
        }

        private NgoBuilderType(string fullName, Type elementType, bool isArray, bool isPointer, bool isByRef)
            : base(typeof(object))
        {
            _fullName = fullName;
            var lastDot = fullName.LastIndexOf('.');
            _name = lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
            _elementType = elementType;
            _isArray = isArray;
            _isPointer = isPointer;
            _isByRef = isByRef;
        }

        internal void SetGenericParamCount(int count)
        {
            _genericParamCount = count;
        }

        public override string FullName => _fullName;

        // A4.3: the defining Go import path of this source-compiled type (write-once). Lets NgoWriter emit a
        // canonical PackageTypeRef(PackagePath, name) instead of a consumer-qualified TypeDef, so the type
        // identity is invariant across archives. Null for composites / types whose package is unknown.
        public string? PackagePath { get; private set; }

        public void StampPackagePath(string importPath)
        {
            PackagePath ??= importPath;
        }
        public override string Name => _name;
        public override string Namespace =>
            _fullName.Contains('.') ? _fullName.Substring(0, _fullName.LastIndexOf('.')) : "";
        protected override bool IsValueTypeImpl() => _isValueType;
        public override Type UnderlyingSystemType => this;
        public override int GetHashCode() => _fullName.GetHashCode();
        public override bool Equals(object? o) => o is NgoBuilderType other && other._fullName == _fullName;
        public override bool Equals(Type? o) => o is NgoBuilderType other && other._fullName == _fullName;
        public override string ToString() => _fullName;

        public override Type MakeArrayType() => CreateArrayOf(this);

        public override Type MakeArrayType(int rank) => CreateArrayOf(this, rank);

        public override Type MakeByRefType() => CreateByRefOf(this);

        public override Type MakePointerType() => CreatePointerOf(this);

        internal static NgoBuilderType CreateArrayOf(Type elementType)
        {
            var elementFullName = elementType.FullName ?? elementType.Name;
            return new NgoBuilderType(elementFullName + "[]", elementType, isArray: true, isPointer: false, isByRef: false);
        }

        internal static NgoBuilderType CreateArrayOf(Type elementType, int rank)
        {
            var elementFullName = elementType.FullName ?? elementType.Name;
            return new NgoBuilderType(elementFullName + $"[{new string(',', rank - 1)}]", elementType, isArray: true, isPointer: false, isByRef: false);
        }

        internal static NgoBuilderType CreateByRefOf(Type elementType)
        {
            var elementFullName = elementType.FullName ?? elementType.Name;
            return new NgoBuilderType(elementFullName + "&", elementType, isArray: false, isPointer: false, isByRef: true);
        }

        internal static NgoBuilderType CreatePointerOf(Type elementType)
        {
            var elementFullName = elementType.FullName ?? elementType.Name;
            return new NgoBuilderType(elementFullName + "*", elementType, isArray: false, isPointer: true, isByRef: false);
        }

        public override Type MakeGenericType(params Type[] typeArguments)
        {
            var argNames = new string[typeArguments.Length];
            for (int index = 0; index < typeArguments.Length; index++)
            {
                argNames[index] = typeArguments[index].FullName ?? typeArguments[index].Name;
            }
            var instantiation = new NgoBuilderType($"{_fullName}[{string.Join(",", argNames)}]", _isValueType);
            instantiation._genericTypeArgs = typeArguments;
            instantiation._genericDefinition = this;
            return instantiation;
        }

        protected override bool IsArrayImpl() => _isArray;
        protected override bool IsPointerImpl() => _isPointer;
        protected override bool IsByRefImpl() => _isByRef;
        protected override bool HasElementTypeImpl() => _elementType != null;
        public override Type? GetElementType() => _elementType;

        // Genericness is explicit state, never inferred from the name string: a generic
        // definition has type parameters (_genericParamCount), an instantiation has type
        // arguments (set by MakeGenericType). Inferring from a '[' in the name misfires on
        // anonymous-struct fingerprints whose Go array/slice fields legitimately contain '['.
        public override bool IsGenericType => _genericParamCount > 0 || _genericTypeArgs != null;

        public override bool IsGenericTypeDefinition => _genericParamCount > 0;

        public override Type GetGenericTypeDefinition()
        {
            return _genericDefinition ?? this;
        }

        public override Type[] GetGenericArguments()
        {
            if (_genericTypeArgs != null)
            {
                return _genericTypeArgs;
            }
            if (_genericParamCount > 0)
            {
                var placeholders = new Type[_genericParamCount];
                for (int index = 0; index < _genericParamCount; index++)
                {
                    placeholders[index] = new NgoGenericParameterType($"T{index}", index, isMethodGenericParam: false);
                }
                return placeholders;
            }
            return Array.Empty<Type>();
        }
    }
}

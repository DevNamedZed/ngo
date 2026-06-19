// -----------------------------------------------------------------------
// <copyright file="TypeIdentity.cs" company="Ziad">
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

namespace Ngo.Compiler.Archive.Identity
{
    /// <summary>
    /// A structural, value-equality identity for a type that is stable across .ngo archive
    /// boundaries. The same logical type produces an equal <see cref="TypeIdentity"/> whether it is
    /// reached from a serialized <see cref="TypeToken"/>, a <see cref="Symbols.TypeSymbol"/>, or a
    /// resolved CLR <see cref="Type"/> — so registries keyed on it match by identity, never by a
    /// rendered name. Construction is centralised in <see cref="IdentityBuilder"/>.
    ///
    /// A named type is identified by <c>(PackagePath, Name)</c> where <see cref="Name"/> is the
    /// SHORT logical name (no package-qualifier prefix). Structural recursion bottoms out at a named
    /// type / primitive / generic parameter (Go has no infinitely-anonymous types), so no cycle
    /// guard is needed. The hash deliberately excludes <see cref="PackagePath"/> (it may be stamped
    /// after construction); <see cref="Equals(TypeIdentity)"/> disambiguates same-named types from
    /// different packages — the same rationale as <see cref="Symbols.TypeSymbolEqualityComparer"/>.
    /// </summary>
    internal sealed class TypeIdentity : IEquatable<TypeIdentity>
    {
        public TypeIdentityKind Kind { get; }
        public string PackagePath { get; }
        public string Name { get; }
        public PrimitiveTypeKind PrimitiveKind { get; }
        public TypeIdentity? Element { get; }
        public TypeIdentity? GenericDefinition { get; }
        public IReadOnlyList<TypeIdentity> GenericArguments { get; }
        public int ParamIndex { get; }

        private TypeIdentity(TypeIdentityKind kind, string packagePath = "", string name = "",
            PrimitiveTypeKind primitiveKind = default, TypeIdentity? element = null,
            TypeIdentity? genericDefinition = null, IReadOnlyList<TypeIdentity>? genericArguments = null,
            int paramIndex = 0)
        {
            Kind = kind;
            PackagePath = packagePath;
            Name = name;
            PrimitiveKind = primitiveKind;
            Element = element;
            GenericDefinition = genericDefinition;
            GenericArguments = genericArguments ?? System.Array.Empty<TypeIdentity>();
            ParamIndex = paramIndex;
        }

        public static TypeIdentity Named(string packagePath, string name) =>
            new(TypeIdentityKind.Named, packagePath: packagePath ?? "", name: name);

        public static TypeIdentity Primitive(PrimitiveTypeKind kind) =>
            new(TypeIdentityKind.Primitive, primitiveKind: kind);

        public static TypeIdentity Array(TypeIdentity element) =>
            new(TypeIdentityKind.Array, element: element);

        public static TypeIdentity Pointer(TypeIdentity element) =>
            new(TypeIdentityKind.Pointer, element: element);

        public static TypeIdentity ByRef(TypeIdentity element) =>
            new(TypeIdentityKind.ByRef, element: element);

        public static TypeIdentity GenericInstance(TypeIdentity definition, IReadOnlyList<TypeIdentity> arguments) =>
            new(TypeIdentityKind.GenericInstance, genericDefinition: definition, genericArguments: arguments);

        public static TypeIdentity GenericTypeParam(int index) =>
            new(TypeIdentityKind.GenericTypeParam, paramIndex: index);

        public static TypeIdentity GenericMethodParam(int index) =>
            new(TypeIdentityKind.GenericMethodParam, paramIndex: index);

        public bool Equals(TypeIdentity? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other is null || other.Kind != Kind)
            {
                return false;
            }

            switch (Kind)
            {
                case TypeIdentityKind.Named:
                    return string.Equals(Name, other.Name, StringComparison.Ordinal)
                        && string.Equals(PackagePath, other.PackagePath, StringComparison.Ordinal);
                case TypeIdentityKind.Primitive:
                    return PrimitiveKind == other.PrimitiveKind;
                case TypeIdentityKind.Array:
                case TypeIdentityKind.Pointer:
                case TypeIdentityKind.ByRef:
                    return Element!.Equals(other.Element);
                case TypeIdentityKind.GenericInstance:
                    return GenericDefinition!.Equals(other.GenericDefinition)
                        && SequenceEquals(GenericArguments, other.GenericArguments);
                case TypeIdentityKind.GenericTypeParam:
                case TypeIdentityKind.GenericMethodParam:
                    return ParamIndex == other.ParamIndex;
                default:
                    return false;
            }
        }

        public override bool Equals(object? obj) => Equals(obj as TypeIdentity);

        public override int GetHashCode()
        {
            switch (Kind)
            {
                case TypeIdentityKind.Named:
                    return HashCode.Combine(Kind, Name.GetHashCode(StringComparison.Ordinal));
                case TypeIdentityKind.Primitive:
                    return HashCode.Combine(Kind, PrimitiveKind);
                case TypeIdentityKind.Array:
                case TypeIdentityKind.Pointer:
                case TypeIdentityKind.ByRef:
                    return HashCode.Combine(Kind, Element);
                case TypeIdentityKind.GenericInstance:
                    return HashCode.Combine(Kind, GenericDefinition, GenericArguments.Count);
                default:
                    return HashCode.Combine(Kind, ParamIndex);
            }
        }

        public override string ToString()
        {
            return Kind switch
            {
                TypeIdentityKind.Named => string.IsNullOrEmpty(PackagePath) ? Name : PackagePath + "::" + Name,
                TypeIdentityKind.Primitive => PrimitiveKind.ToString(),
                TypeIdentityKind.Array => Element + "[]",
                TypeIdentityKind.Pointer => "*" + Element,
                TypeIdentityKind.ByRef => "ref " + Element,
                TypeIdentityKind.GenericInstance => GenericDefinition + "<" + string.Join(",", GenericArguments) + ">",
                TypeIdentityKind.GenericTypeParam => "!" + ParamIndex,
                TypeIdentityKind.GenericMethodParam => "!!" + ParamIndex,
                _ => Kind.ToString(),
            };
        }

        private static bool SequenceEquals(IReadOnlyList<TypeIdentity> first, IReadOnlyList<TypeIdentity> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }
            for (int index = 0; index < first.Count; index++)
            {
                if (!first[index].Equals(second[index]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}

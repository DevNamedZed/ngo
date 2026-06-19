// -----------------------------------------------------------------------
// <copyright file="MethodIdentity.cs" company="Ziad">
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
    /// A structural, value-equality identity for a method: its declaring <see cref="TypeIdentity"/>,
    /// name, and parameter <see cref="TypeIdentity"/> list. Return type is excluded (Go method identity,
    /// like the .NET overload rules, is declaring type + name + parameters). Method-level generic arity
    /// is deliberately NOT part of the key: a <c>MethodDef</c> reference token cannot carry the callee's
    /// own arity (only an instantiating <c>MethodSpec</c> can), and the parameter identities already
    /// distinguish generic methods via their <c>GenericMethodParam</c> entries — so including arity would
    /// desync registration (real arity) from reference (0). Go permits at most one method of a given name
    /// per type, so (declaring, name, parameters) is unambiguous. Built only via
    /// <see cref="IdentityBuilder"/> so all four sides (serialized registration, linker token, emit
    /// symbol, IL-body correlation) agree by construction.
    /// </summary>
    internal sealed class MethodIdentity : IEquatable<MethodIdentity>
    {
        public TypeIdentity DeclaringType { get; }
        public string Name { get; }
        public IReadOnlyList<TypeIdentity> Parameters { get; }

        public MethodIdentity(TypeIdentity declaringType, string name, IReadOnlyList<TypeIdentity> parameters)
        {
            DeclaringType = declaringType;
            Name = name;
            Parameters = parameters ?? Array.Empty<TypeIdentity>();
        }

        public bool Equals(MethodIdentity? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other is null
                || !string.Equals(Name, other.Name, StringComparison.Ordinal)
                || Parameters.Count != other.Parameters.Count
                || !DeclaringType.Equals(other.DeclaringType))
            {
                return false;
            }
            for (int index = 0; index < Parameters.Count; index++)
            {
                if (!Parameters[index].Equals(other.Parameters[index]))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as MethodIdentity);

        public override int GetHashCode() =>
            HashCode.Combine(DeclaringType, Name.GetHashCode(StringComparison.Ordinal), Parameters.Count);

        public override string ToString() =>
            DeclaringType + "." + Name + "(" + string.Join(",", Parameters) + ")";
    }
}

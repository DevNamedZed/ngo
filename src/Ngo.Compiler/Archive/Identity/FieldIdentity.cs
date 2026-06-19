// -----------------------------------------------------------------------
// <copyright file="FieldIdentity.cs" company="Ziad">
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

namespace Ngo.Compiler.Archive.Identity
{
    /// <summary>
    /// A structural, value-equality identity for a field: its declaring <see cref="TypeIdentity"/>
    /// and name. Built only via <see cref="IdentityBuilder"/>.
    /// </summary>
    internal sealed class FieldIdentity : IEquatable<FieldIdentity>
    {
        public TypeIdentity DeclaringType { get; }
        public string Name { get; }

        public FieldIdentity(TypeIdentity declaringType, string name)
        {
            DeclaringType = declaringType;
            Name = name;
        }

        public bool Equals(FieldIdentity? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return other is not null
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && DeclaringType.Equals(other.DeclaringType);
        }

        public override bool Equals(object? obj) => Equals(obj as FieldIdentity);

        public override int GetHashCode() =>
            HashCode.Combine(DeclaringType, Name.GetHashCode(StringComparison.Ordinal));

        public override string ToString() => DeclaringType + "." + Name;
    }
}

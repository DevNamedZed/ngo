// -----------------------------------------------------------------------
// <copyright file="ClrTypeKey.cs" company="Ziad">
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

namespace Ngo.Compiler.Semantics
{
    /// <summary>
    /// Dictionary key for CLR type resolution: (importPath, typeName).
    /// Used by RuntimePackageResolver to map Go types to their .NET CLR types.
    /// </summary>
    public readonly struct ClrTypeKey : IEquatable<ClrTypeKey>
    {
        public ClrTypeKey(string importPath, string typeName)
        {
            ImportPath = importPath;
            TypeName = typeName;
        }

        public string ImportPath { get; }

        public string TypeName { get; }

        public bool Equals(ClrTypeKey other)
        {
            return ImportPath == other.ImportPath && TypeName == other.TypeName;
        }

        public override bool Equals(object? obj) => obj is ClrTypeKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ImportPath, TypeName);
    }
}

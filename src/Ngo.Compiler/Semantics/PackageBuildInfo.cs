// -----------------------------------------------------------------------
// <copyright file="PackageBuildInfo.cs" company="Ziad">
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
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Semantics
{
    /// <summary>
    /// Intermediate state for building a runtime package: the CLR type backing
    /// the package, its PackageSymbol, and a map of declared Go type names.
    /// </summary>
    public sealed class PackageBuildInfo
    {
        public PackageBuildInfo(Type clrType, PackageSymbol package, Dictionary<string, TypeSymbol> typeMap)
        {
            ClrType = clrType;
            Package = package;
            TypeMap = typeMap;
        }

        public Type ClrType { get; }

        public PackageSymbol Package { get; }

        public Dictionary<string, TypeSymbol> TypeMap { get; }
    }
}

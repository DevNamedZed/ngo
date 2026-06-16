// -----------------------------------------------------------------------
// <copyright file="PackageEmitContext.cs" company="Ziad">
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

using Ngo.Compiler.Emit.Builder;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Per-package emit state — one created fresh for each Go package emitted (the main package
    /// and every dependency). Holds the package's static class and its identity. Because a fresh
    /// instance is created per package rather than toggling shared flags on
    /// <see cref="EmitContext"/>, there is no reset (spec/F4-EMIT-CONTEXT-HIERARCHY.md, step 2).
    /// Cross-package shared state stays on EmitContext/EmitSession.
    /// </summary>
    internal sealed class PackageEmitContext
    {
        public PackageEmitContext(string? importPath, bool isDependency)
        {
            ImportPath = importPath;
            IsDependency = isDependency;
        }

        /// <summary>Go import path of the package being emitted; null for the main package.</summary>
        public string? ImportPath { get; }

        /// <summary>True when this package is a dependency (errors are recoverable, naming differs).</summary>
        public bool IsDependency { get; }

        /// <summary>The package's static class. Assigned once when the class is defined during emit.</summary>
        public ITypeBuilder PackageType { get; set; } = null!;
    }
}

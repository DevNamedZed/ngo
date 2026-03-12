// -----------------------------------------------------------------------
// <copyright file="ModuleMatch.cs" company="Ziad">
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

namespace Ngo.Compiler.Semantics
{
    /// <summary>
    /// Represents a resolved Go module match from go.mod requirements.
    /// Returned by GoModuleResolver.FindModule when a module path matches an import path.
    /// </summary>
    public sealed class ModuleMatch
    {
        public ModuleMatch(string module, string version)
        {
            Module = module;
            Version = version;
        }

        public string Module { get; }

        public string Version { get; }
    }
}

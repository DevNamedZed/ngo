// -----------------------------------------------------------------------
// <copyright file="NgoMethodOverride.cs" company="Ziad">
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

namespace Ngo.Compiler.Emit.Builder
{
    /// <summary>
    /// Stores a method override mapping for NgoTypeBuilder serialization.
    /// Maps a body method (on the type being built) to a declaration method (on a base/interface type).
    /// </summary>
    internal sealed class NgoMethodOverride
    {
        public NgoMethodOverride(string bodyMethodName, string declarationTypeName, string declarationMethodName)
        {
            BodyMethodName = bodyMethodName;
            DeclarationTypeName = declarationTypeName;
            DeclarationMethodName = declarationMethodName;
        }

        public string BodyMethodName { get; }
        public string DeclarationTypeName { get; }
        public string DeclarationMethodName { get; }
    }
}

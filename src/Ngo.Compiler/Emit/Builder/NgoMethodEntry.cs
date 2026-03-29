// -----------------------------------------------------------------------
// <copyright file="NgoMethodEntry.cs" company="Ziad">
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

using System.Reflection;

namespace Ngo.Compiler.Emit.Builder
{
    /// <summary>
    /// Represents a method entry captured by NgoModuleBuilder for IL serialization.
    /// Contains the method signature metadata and an index into the body table.
    /// </summary>
    internal sealed class NgoMethodEntry
    {
        public NgoMethodEntry(string methodName, MethodAttributes attributes, string returnType,
            string[] paramTypes, int bodyIndex, string[] genericParamNames)
        {
            MethodName = methodName;
            Attributes = attributes;
            ReturnType = returnType;
            ParamTypes = paramTypes;
            BodyIndex = bodyIndex;
            GenericParamNames = genericParamNames;
        }

        public string MethodName { get; }

        public MethodAttributes Attributes { get; }

        public string ReturnType { get; }

        public string[] ParamTypes { get; }

        public string[] GenericParamNames { get; }

        /// <summary>
        /// Index into the IL body table (Section 3), or -1 if the method has no body.
        /// </summary>
        public int BodyIndex { get; }
    }
}

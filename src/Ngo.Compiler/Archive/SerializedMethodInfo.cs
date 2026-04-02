// -----------------------------------------------------------------------
// <copyright file="SerializedMethodInfo.cs" company="Ziad">
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

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// Serialized method metadata read from an .ngo archive, before
    /// the MethodBuilder is created (types may not be resolved yet).
    /// </summary>
    public sealed class SerializedMethodInfo
    {
        public SerializedMethodInfo(string methodName, MethodAttributes attributes,
            string returnTypeName, string[] paramTypeNames, int bodyIndex,
            string[] genericParamNames)
        {
            MethodName = methodName;
            Attributes = attributes;
            ReturnTypeName = returnTypeName;
            ParamTypeNames = paramTypeNames;
            BodyIndex = bodyIndex;
            GenericParamNames = genericParamNames;
        }

        public string MethodName { get; }

        public MethodAttributes Attributes { get; }

        public string ReturnTypeName { get; }

        public string[] ParamTypeNames { get; }

        public int BodyIndex { get; }

        public string[] GenericParamNames { get; }
    }
}

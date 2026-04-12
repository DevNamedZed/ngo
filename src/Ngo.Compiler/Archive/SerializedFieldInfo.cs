// -----------------------------------------------------------------------
// <copyright file="SerializedFieldInfo.cs" company="Ziad">
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
    internal sealed class SerializedFieldInfo
    {
        public SerializedFieldInfo(string name, FieldAttributes attributes, string typeName,
            int goArrayLength, string elementTypeName)
        {
            Name = name;
            Attributes = attributes;
            TypeName = typeName;
            GoArrayLength = goArrayLength;
            ElementTypeName = elementTypeName;
        }

        public string Name { get; }

        public FieldAttributes Attributes { get; }

        public string TypeName { get; }

        public int GoArrayLength { get; }

        public string ElementTypeName { get; }
    }
}

// -----------------------------------------------------------------------
// <copyright file="WrapperTypeInfo.cs" company="Ziad">
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
using System.Reflection;

namespace Ngo.Compiler.Emit
{
    /// <summary>
    /// Holds the generated wrapper type and its constructor for interface boxing.
    /// Used when a concrete type needs to be wrapped to satisfy a Go interface.
    /// </summary>
    internal sealed class WrapperTypeInfo
    {
        public WrapperTypeInfo(Type type, ConstructorInfo constructor)
        {
            Type = type;
            Constructor = constructor;
        }

        public Type Type { get; }

        public ConstructorInfo Constructor { get; }
    }
}

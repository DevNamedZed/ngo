// -----------------------------------------------------------------------
// <copyright file="SerializationContext.cs" company="Ziad">
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

namespace Ngo.Compiler.Archive
{
    /// <summary>
    /// Immutable context for IL serialization. Tells the NgoWriter which generic
    /// parameters are in scope for the current method being serialized.
    /// Created by the method/constructor builder and passed to the NgoWriter at construction.
    /// </summary>
    internal sealed class SerializationContext
    {
        public Type[] MethodGenericParams { get; }
        public Type[] TypeGenericParams { get; }

        public SerializationContext(Type[] methodGenericParams, Type[] typeGenericParams)
        {
            MethodGenericParams = methodGenericParams;
            TypeGenericParams = typeGenericParams;
        }

        public static SerializationContext Empty { get; } = new(Type.EmptyTypes, Type.EmptyTypes);

        public int FindMethodGenericParamIndex(Type type)
        {
            for (int i = 0; i < MethodGenericParams.Length; i++)
            {
                if (MethodGenericParams[i] == type)
                {
                    return i;
                }
            }
            return -1;
        }

        public int FindTypeGenericParamIndex(Type type)
        {
            for (int i = 0; i < TypeGenericParams.Length; i++)
            {
                if (TypeGenericParams[i] == type)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Creates a child context for a closure type inside this method.
        /// The closure's type generic params replace the method generic params.
        /// </summary>
        public SerializationContext ForClosureType(Type[] closureTypeGenericParams)
        {
            return new SerializationContext(Type.EmptyTypes, closureTypeGenericParams);
        }

        /// <summary>
        /// Creates a child context for a lambda method inside this method.
        /// The lambda's method generic params replace the enclosing method's.
        /// </summary>
        public SerializationContext ForLambdaMethod(Type[] lambdaMethodGenericParams)
        {
            return new SerializationContext(lambdaMethodGenericParams, TypeGenericParams);
        }
    }
}

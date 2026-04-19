// -----------------------------------------------------------------------
// <copyright file="SignatureSubstitution.cs" company="Ziad">
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
    /// Captures the declaring-type generic argument mapping for a single method or
    /// constructor signature, so parameter types and the return type can be resolved
    /// to the outer method's scope (e.g. an open Slice&lt;!0&gt; return becomes
    /// Slice&lt;NgoGenericParam(T)&gt; when the declaring type is Slice&lt;T&gt;).
    /// </summary>
    internal sealed class SignatureSubstitution
    {
        public Type? DeclaringType { get; }

        public Type[]? DeclaringTypeArguments { get; }

        public Type[]? DeclaringTypeDefParameters { get; }

        public SignatureSubstitution(Type? declaringType)
        {
            DeclaringType = declaringType;

            if (declaringType == null || !declaringType.IsGenericType || declaringType.IsGenericTypeDefinition)
            {
                return;
            }

            try
            {
                DeclaringTypeArguments = declaringType.GetGenericArguments();
                DeclaringTypeDefParameters = declaringType.GetGenericTypeDefinition().GetGenericArguments();
            }
            catch (NotSupportedException)
            {
            }
        }

        public Type Substitute(Type type)
        {
            return NgoWriter.SubstituteSignatureType(
                type, DeclaringType, DeclaringTypeArguments, DeclaringTypeDefParameters);
        }
    }
}

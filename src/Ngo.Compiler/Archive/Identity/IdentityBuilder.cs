// -----------------------------------------------------------------------
// <copyright file="IdentityBuilder.cs" company="Ziad">
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
using Ngo.Compiler.Emit;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Archive.Identity
{
    /// <summary>
    /// The single canonicalizer for <see cref="TypeIdentity"/>/<see cref="MethodIdentity"/>/
    /// <see cref="FieldIdentity"/>. Every side that registers or references a type/method/field funnels
    /// through here, so the same logical entity yields an equal key whether reached from a serialized
    /// <see cref="TypeToken"/>, a registration's name string + tokens, or a resolved CLR <see cref="Type"/>/
    /// <see cref="TypeSymbol"/>. This is the ONLY place that knows the TypeDef-vs-PackageTypeRef
    /// unification rule, so the four sides cannot drift.
    /// </summary>
    internal sealed class IdentityBuilder
    {
        private readonly TypeMapper _mapper;

        public IdentityBuilder(TypeMapper mapper)
        {
            _mapper = mapper;
        }

        // --- Serialized type-token side (linker register + reference) -------------------------------

        /// <summary>
        /// A type-token interpreted in the context of the archive that produced it. A bare
        /// <c>TypeDef(name)</c> belongs to <paramref name="archiveImportPath"/>; a
        /// <c>PackageTypeRef(importPath, name)</c> carries its own package. Both yield the same
        /// <c>Named(importPath, shortName)</c> identity for the same logical type.
        /// </summary>
        public TypeIdentity FromToken(TypeToken token, string archiveImportPath)
        {
            switch (token.Kind)
            {
                case TypeTokenKind.TypeDef:
                    return TypeIdentity.Named(archiveImportPath, StripQualifier(token.TypeName, archiveImportPath));
                case TypeTokenKind.PackageTypeRef:
                    return TypeIdentity.Named(token.PackageImportPath,
                        StripQualifier(token.TypeName, token.PackageImportPath));
                case TypeTokenKind.Primitive:
                    return TypeIdentity.Primitive(token.PrimitiveKind);
                case TypeTokenKind.Array:
                    return TypeIdentity.Array(FromToken(token.ElementType!, archiveImportPath));
                case TypeTokenKind.Pointer:
                    return TypeIdentity.Pointer(FromToken(token.ElementType!, archiveImportPath));
                case TypeTokenKind.ByRef:
                    return TypeIdentity.ByRef(FromToken(token.ElementType!, archiveImportPath));
                case TypeTokenKind.GenericInst:
                {
                    var definition = FromToken(token.GenericDefinition!, archiveImportPath);
                    var arguments = new TypeIdentity[token.GenericArguments.Length];
                    for (int index = 0; index < arguments.Length; index++)
                    {
                        arguments[index] = FromToken(token.GenericArguments[index], archiveImportPath);
                    }
                    return TypeIdentity.GenericInstance(definition, arguments);
                }
                case TypeTokenKind.GenericTypeParam:
                    return TypeIdentity.GenericTypeParam(token.GenericParamIndex);
                case TypeTokenKind.GenericMethodParam:
                    return TypeIdentity.GenericMethodParam(token.GenericParamIndex);
                default:
                    throw new InvalidOperationException($"IdentityBuilder: unknown TypeToken kind {token.Kind}");
            }
        }

        /// <summary>
        /// The identity of a method referenced by a <see cref="MethodToken"/>. A <c>MethodSpec</c>
        /// (instantiated generic call) resolves to its generic definition's identity — the instantiation
        /// arguments do not change which method it is.
        /// </summary>
        public MethodIdentity FromMethodToken(MethodToken token, string archiveImportPath)
        {
            if (token.Kind == MethodTokenKind.MethodSpec && token.GenericDefinition != null)
            {
                return FromMethodToken(token.GenericDefinition, archiveImportPath);
            }

            var declaring = FromToken(token.DeclaringType!, archiveImportPath);
            var parameters = new TypeIdentity[token.ParameterTypes.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                parameters[index] = FromToken(token.ParameterTypes[index], archiveImportPath);
            }
            return new MethodIdentity(declaring, token.MethodName, parameters);
        }

        public FieldIdentity FromFieldToken(FieldToken token, string archiveImportPath)
        {
            return new FieldIdentity(FromToken(token.DeclaringType!, archiveImportPath), token.FieldName);
        }

        /// <summary>
        /// The identity a method is REGISTERED under, from its serialized metadata. The declaring
        /// identity is supplied by the caller (built from the type's full name + archive package);
        /// the parameters come from the structured <see cref="SerializedMethodInfo.ParamTypes"/> tokens
        /// (NOT the rendered name strings), so they match a reference's token-built parameters.
        /// </summary>
        public MethodIdentity FromSerialized(TypeIdentity declaring, SerializedMethodInfo methodInfo, string archiveImportPath)
        {
            var parameters = new TypeIdentity[methodInfo.ParamTypes.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                parameters[index] = FromToken(methodInfo.ParamTypes[index], archiveImportPath);
            }
            return new MethodIdentity(declaring, methodInfo.MethodName, parameters);
        }

        /// <summary>
        /// The identity of a type that the linker registered under <paramref name="fullTypeName"/>
        /// (a possibly package-qualified name) in archive <paramref name="archiveImportPath"/>.
        /// </summary>
        public TypeIdentity NamedFromRegisteredType(string fullTypeName, string archiveImportPath)
        {
            return TypeIdentity.Named(archiveImportPath, StripQualifier(fullTypeName, archiveImportPath));
        }

        // --- Resolved CLR-type / symbol side (emit reference) ---------------------------------------

        /// <summary>
        /// The identity of a fully-resolved (concrete) CLR type. Builds the same structured token the
        /// writer used at registration (so a slice → <c>Slice&lt;T&gt;</c>, a string → primitive, etc.
        /// agree), then canonicalizes it. <paramref name="contextImportPath"/> is the package owning a
        /// bare local <c>TypeDef</c> (cross-package/runtime types carry their own import path).
        /// </summary>
        public TypeIdentity FromClrType(Type type, string contextImportPath)
        {
            return FromToken(NgoWriter.BuildConcreteTypeToken(type), contextImportPath);
        }

        /// <summary>The identity of a resolved type symbol, via its mapped CLR type.</summary>
        public TypeIdentity FromTypeSymbol(TypeSymbol symbol, string contextImportPath)
        {
            return FromClrType(_mapper.Map(symbol), contextImportPath);
        }

        // --- Helpers --------------------------------------------------------------------------------

        /// <summary>
        /// Removes the exact package-path prefix the linker prepends when it qualifies a colliding
        /// short name (<c>importPath.Replace('/','.') + "."</c>), yielding the short logical name. A
        /// deterministic inverse of the known qualification — not a name guess.
        /// </summary>
        private static string StripQualifier(string name, string importPath)
        {
            if (string.IsNullOrEmpty(importPath) || string.IsNullOrEmpty(name))
            {
                return name;
            }
            var prefix = importPath.Replace('/', '.') + ".";
            return name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name;
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="NgoConstructorBuilder.cs" company="Ziad">
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
using Ngo.Compiler.Archive;
using System.Collections.Generic;
using System.Reflection;
using Ngo.Compiler.Emit.Refs;

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoConstructorBuilder : IConstructorBuilder
    {
        private readonly MethodAttributes _attrs;
        private readonly CallingConventions _callingConvention;
        private readonly List<string> _paramTypeNames;
        private readonly Type[] _paramTypes;
        private readonly NgoTypeBuilder? _declaringTypeBuilder;
        private NgoWriter? _writer;

        public NgoConstructorBuilder(MethodAttributes attrs, CallingConventions callingConvention, Type[] paramTypes,
            NgoTypeBuilder? declaringTypeBuilder = null)
        {
            _attrs = attrs;
            _callingConvention = callingConvention;
            _declaringTypeBuilder = declaringTypeBuilder;
            _paramTypes = paramTypes ?? Type.EmptyTypes;
            _paramTypeNames = new List<string>();
            foreach (var pt in _paramTypes)
            {
                _paramTypeNames.Add(NgoWriter.GetTypeNameStatic(pt));
            }
        }

        public MethodAttributes Attributes => _attrs;

        public CallingConventions CallingConvention => _callingConvention;

        public IReadOnlyList<string> ParamTypeNames => _paramTypeNames;

        public Type[] ParameterTypes => _paramTypes;

        // Structured, index-based signature tokens (the .NET VAR/MVAR encoding) built from the
        // declaring type's generic context. A constructor has no method-level generic parameters.
        public TypeToken[] ParamTypeTokens
        {
            get
            {
                var writer = BuildSignatureWriter();
                var tokens = new TypeToken[_paramTypes.Length];
                for (int i = 0; i < _paramTypes.Length; i++)
                {
                    tokens[i] = writer.BuildTypeToken(_paramTypes[i]);
                }
                return tokens;
            }
        }

        private NgoWriter BuildSignatureWriter()
        {
            var typeGenericParams = _declaringTypeBuilder?.GenericParamTypes ?? Type.EmptyTypes;
            return new NgoWriter(new SerializationContext(Type.EmptyTypes, typeGenericParams));
        }

        public NgoWriter? Writer => _writer;

        public CilWriter GetILWriter()
        {
            if (_writer == null)
            {
                var typeGenericParams = _declaringTypeBuilder?.GenericParamTypes ?? Type.EmptyTypes;
                var context = new Archive.SerializationContext(Type.EmptyTypes, typeGenericParams);
                _writer = new NgoWriter(context);
            }
            return _writer;
        }

        public CtorRef AsCtorRef()
        {
            if (_declaringTypeBuilder == null)
            {
                throw new InvalidOperationException(
                    "NgoConstructorBuilder.AsCtorRef: constructor was created without a declaring NgoTypeBuilder");
            }
            return CtorRef.FromBuilder(this, TypeRef.FromBuilder(_declaringTypeBuilder));
        }
    }
}

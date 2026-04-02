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

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoConstructorBuilder : IConstructorBuilder
    {
        private readonly MethodAttributes _attrs;
        private readonly CallingConventions _callingConvention;
        private readonly List<string> _paramTypeNames;
        private readonly NgoTypeBuilder? _declaringTypeBuilder;
        private NgoWriter? _writer;

        public NgoConstructorBuilder(MethodAttributes attrs, CallingConventions callingConvention, Type[] paramTypes,
            NgoTypeBuilder? declaringTypeBuilder = null)
        {
            _attrs = attrs;
            _callingConvention = callingConvention;
            _declaringTypeBuilder = declaringTypeBuilder;
            _paramTypeNames = new List<string>();
            if (paramTypes != null)
            {
                foreach (var pt in paramTypes)
                {
                    _paramTypeNames.Add(NgoWriter.GetTypeNameStatic(pt));
                }
            }
        }

        public MethodAttributes Attributes => _attrs;

        public CallingConventions CallingConvention => _callingConvention;

        public IReadOnlyList<string> ParamTypeNames => _paramTypeNames;

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
    }
}

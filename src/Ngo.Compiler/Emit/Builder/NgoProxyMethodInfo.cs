// -----------------------------------------------------------------------
// <copyright file="NgoProxyMethodInfo.cs" company="Ziad">
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
using System.Globalization;
using System.Reflection;

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoProxyMethodInfo : MethodInfo
    {
        private readonly Type _declaringType;
        private readonly string _name;
        private Type[] _parameterTypes;
        private Type _returnType;

        public NgoProxyMethodInfo(Type declaringType, string name)
            : this(declaringType, name, Type.EmptyTypes, typeof(void))
        {
        }

        public NgoProxyMethodInfo(Type declaringType, string name, Type[] parameterTypes, Type returnType)
        {
            _declaringType = declaringType;
            _name = name;
            _parameterTypes = parameterTypes ?? Type.EmptyTypes;
            _returnType = returnType ?? typeof(void);
        }

        public override Type DeclaringType => _declaringType;
        public override string Name => _name;
        public override Type ReflectedType => _declaringType;
        public override MethodAttributes Attributes => MethodAttributes.Public | MethodAttributes.Static;
        public override RuntimeMethodHandle MethodHandle => throw new NotSupportedException();
        public override Type ReturnType => _returnType;
        public override MethodInfo GetBaseDefinition() => this;
        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplAttributes.IL;

        public override ParameterInfo[] GetParameters()
        {
            var result = new ParameterInfo[_parameterTypes.Length];
            for (int i = 0; i < _parameterTypes.Length; i++)
            {
                result[i] = new NgoProxyParameterInfo(_parameterTypes[i], i);
            }
            return result;
        }
        public override object Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
            => throw new NotSupportedException();
        public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotSupportedException();

        internal void UpdateParameterTypes(Type[] types)
        {
            _parameterTypes = types ?? Type.EmptyTypes;
        }

        internal void UpdateReturnType(Type type)
        {
            _returnType = type ?? typeof(void);
        }
        public override bool IsDefined(Type attributeType, bool inherit) => false;

        public override bool IsGenericMethod => _genericArgs != null;
        public override bool IsGenericMethodDefinition => _isGenericDef;

        private Type[]? _genericArgs;
        private bool _isGenericDef;
        private NgoProxyMethodInfo? _genericDefinition;

        internal void SetIsGenericDefinition()
        {
            _isGenericDef = true;
        }

        public override MethodInfo MakeGenericMethod(params Type[] typeArguments)
        {
            var definition = _isGenericDef ? this : _genericDefinition ?? this;
            var instantiated = new NgoProxyMethodInfo(_declaringType, _name, _parameterTypes, _returnType);
            instantiated._genericArgs = typeArguments;
            instantiated._genericDefinition = definition;
            return instantiated;
        }

        public override MethodInfo GetGenericMethodDefinition()
        {
            if (_isGenericDef)
            {
                return this;
            }
            if (_genericDefinition != null)
            {
                return _genericDefinition;
            }
            return base.GetGenericMethodDefinition();
        }

        public override Type[] GetGenericArguments()
        {
            return _genericArgs ?? Type.EmptyTypes;
        }
    }

    internal sealed class NgoProxyParameterInfo : ParameterInfo
    {
        public NgoProxyParameterInfo(Type parameterType, int position)
        {
            ClassImpl = parameterType;
            PositionImpl = position;
        }
    }

}

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

        public NgoProxyMethodInfo(Type declaringType, string name)
        {
            _declaringType = declaringType;
            _name = name;
        }

        public override Type DeclaringType => _declaringType;
        public override string Name => _name;
        public override Type ReflectedType => _declaringType;
        public override MethodAttributes Attributes => MethodAttributes.Public | MethodAttributes.Static;
        public override RuntimeMethodHandle MethodHandle => throw new NotSupportedException();
        public override Type ReturnType => typeof(void);
        public override MethodInfo GetBaseDefinition() => this;
        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplAttributes.IL;
        public override ParameterInfo[] GetParameters() => Array.Empty<ParameterInfo>();
        public override object Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
            => throw new NotSupportedException();
        public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotSupportedException();
        public override bool IsDefined(Type attributeType, bool inherit) => false;
    }
}

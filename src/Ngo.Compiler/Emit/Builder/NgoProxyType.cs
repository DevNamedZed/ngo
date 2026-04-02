// -----------------------------------------------------------------------
// <copyright file="NgoProxyType.cs" company="Ziad">
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
using System.Globalization;
using System.Reflection;

namespace Ngo.Compiler.Emit.Builder
{
    internal sealed class NgoProxyType : TypeDelegator
    {
        private readonly string _fullName;
        private readonly string _name;
        private readonly bool _isValueType;
        private readonly List<NgoProxyMethodInfo> _definedMethods = new();
        private int _genericParamCount;
        private Type[]? _genericTypeArgs;

        public NgoProxyType(string fullName, bool isValueType = false)
            : base(typeof(object))
        {
            _fullName = fullName;
            var dot = fullName.LastIndexOf('.');
            _name = dot >= 0 ? fullName.Substring(dot + 1) : fullName;
            _isValueType = isValueType;
        }

        internal void SetGenericParamCount(int count)
        {
            _genericParamCount = count;
        }

        private readonly List<NgoProxyFieldInfo> _definedFields = new();

        internal void AddMethod(string name, Type returnType, Type[] paramTypes)
        {
            _definedMethods.Add(new NgoProxyMethodInfo(this, name, paramTypes, returnType));
        }

        internal void AddField(string name, Type fieldType)
        {
            _definedFields.Add(new NgoProxyFieldInfo(this, name, fieldType));
        }

        public override FieldInfo? GetField(string name, BindingFlags bindingAttr)
        {
            foreach (var f in _definedFields)
            {
                if (f.Name == name)
                {
                    return f;
                }
            }
            return null;
        }

        public override MethodInfo[] GetMethods(System.Reflection.BindingFlags bindingAttr)
        {
            return _definedMethods.ToArray();
        }

        protected override MethodInfo? GetMethodImpl(string name, System.Reflection.BindingFlags bindingAttr,
            Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers)
        {
            foreach (var m in _definedMethods)
            {
                if (m.Name == name)
                {
                    return m;
                }
            }
            return null;
        }

        public override string FullName => _fullName;
        public override string Name => _name;
        public override string Namespace => _fullName.Contains('.') ? _fullName.Substring(0, _fullName.LastIndexOf('.')) : "";
        protected override bool IsValueTypeImpl() => _isValueType;
        public override Type UnderlyingSystemType => this;
        public override int GetHashCode() => _fullName.GetHashCode();
        public override bool Equals(object? o) => o is NgoProxyType other && other._fullName == _fullName;
        public override bool Equals(Type? o) => o is NgoProxyType other && other._fullName == _fullName;
        public override string ToString() => _fullName;

        public override Type MakeArrayType() => new NgoProxyType(_fullName + "[]");
        public override Type MakeArrayType(int rank) => new NgoProxyType(_fullName + $"[{new string(',', rank - 1)}]");
        public override Type MakeByRefType() => new NgoProxyType(_fullName + "&");
        public override Type MakePointerType() => new NgoProxyType(_fullName + "*");
        public override Type MakeGenericType(params Type[] typeArguments)
        {
            var argNames = new string[typeArguments.Length];
            for (int i = 0; i < typeArguments.Length; i++)
            {
                argNames[i] = typeArguments[i].FullName ?? typeArguments[i].Name;
            }
            var result = new NgoProxyType($"{_fullName}[{string.Join(",", argNames)}]", _isValueType);
            result._genericTypeArgs = typeArguments;
            return result;
        }
        public override bool IsGenericType => _genericParamCount > 0 || (_fullName.Contains('[') && !_fullName.EndsWith("[]"));
        public override bool IsGenericTypeDefinition => _genericParamCount > 0;
        public new bool IsArray => _fullName.EndsWith("[]");
        public override Type GetGenericTypeDefinition()
        {
            // Extract the base name before [...]
            var bracketIdx = _fullName.IndexOf('[');
            if (bracketIdx > 0)
            {
                return new NgoProxyType(_fullName.Substring(0, bracketIdx));
            }
            return this;
        }
        public override Type[] GetGenericArguments()
        {
            if (_genericTypeArgs != null)
            {
                return _genericTypeArgs;
            }
            if (_genericParamCount > 0)
            {
                var args = new Type[_genericParamCount];
                for (int i = 0; i < _genericParamCount; i++)
                {
                    args[i] = new NgoProxyType($"T{i}");
                }
                return args;
            }
            return System.Array.Empty<Type>();
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
        {
            return new[] { new NgoProxyConstructorInfo(this) };
        }

        protected override ConstructorInfo? GetConstructorImpl(
            BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention,
            Type[]? types, ParameterModifier[]? modifiers)
        {
            return new NgoProxyConstructorInfo(this, types);
        }
    }

    internal sealed class NgoProxyConstructorInfo : ConstructorInfo
    {
        private readonly Type _declaringType;
        private readonly Type[] _parameterTypes;

        public NgoProxyConstructorInfo(Type declaringType, Type[]? parameterTypes = null)
        {
            _declaringType = declaringType;
            _parameterTypes = parameterTypes ?? System.Type.EmptyTypes;
        }

        public override Type? DeclaringType => _declaringType;
        public override string Name => ".ctor";
        public override Type? ReflectedType => _declaringType;
        public override MethodAttributes Attributes => MethodAttributes.Public;
        public override RuntimeMethodHandle MethodHandle => throw new NotSupportedException();

        public override ParameterInfo[] GetParameters()
        {
            var result = new ParameterInfo[_parameterTypes.Length];
            for (int i = 0; i < _parameterTypes.Length; i++)
            {
                result[i] = new NgoProxyParameterInfo(_parameterTypes[i], i);
            }
            return result;
        }
        public override object Invoke(BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) => throw new NotSupportedException();
        public override object Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) => throw new NotSupportedException();
        public override MethodImplAttributes GetMethodImplementationFlags() => MethodImplAttributes.IL;
        public override object[] GetCustomAttributes(bool inherit) => System.Array.Empty<object>();
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => System.Array.Empty<object>();
        public override bool IsDefined(Type attributeType, bool inherit) => false;
    }

}

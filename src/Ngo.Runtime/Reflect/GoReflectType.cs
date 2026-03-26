// -----------------------------------------------------------------------
// <copyright file="GoReflectType.cs" company="Ziad">
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
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Reflect
{
    /// <summary>
    /// Go reflect.Type — wraps .NET System.Type.
    /// </summary>
    [GoType("interface", Name = "Type", Package = "reflect")]
    public sealed class GoReflectType
    {
        private readonly Type _clrType;
        private readonly string _goName;

        internal GoReflectType(Type clrType, string? goName = null)
        {
            _clrType = clrType;
            _goName = goName ?? DeriveGoName(clrType);
        }

        [GoMethod]
        public string Name() => _goName;

        [GoMethod]
        [return: GoReturn("Kind")]
        public long Kind() => DeriveKind(_clrType);

        [GoMethod]
        public string String() => _goName;

        [GoMethod]
        [return: GoReturn("int")]
        public long NumField()
        {
            if (Kind() != GoReflectKinds.Struct)
                throw new GoPanicException("reflect: NumField of non-struct type " + _goName);
            return _clrType.GetFields(BindingFlags.Public | BindingFlags.Instance).Length;
        }

        [GoMethod]
        [return: GoReturn("StructField")]
        public GoReflectStructField Field(long i)
        {
            if (Kind() != GoReflectKinds.Struct)
                throw new GoPanicException("reflect: Field of non-struct type " + _goName);
            var fields = _clrType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (i < 0 || i >= fields.Length)
                throw new GoPanicException("reflect: Field index out of range");
            var f = fields[(int)i];
            return new GoReflectStructField(f.Name, new GoReflectType(f.FieldType), "", (int)i, false);
        }

        [GoMethod]
        public long NumMethod()
        {
            return _clrType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length;
        }

        [GoMethod]
        [return: GoReturn("Method", "bool")]
        public (GoReflectMethod, bool) MethodByName(string name)
        {
            var methods = _clrType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == name)
                {
                    var method = new GoReflectMethod
                    {
                        Name = methods[i].Name,
                        Type = new GoReflectType(methods[i].ReturnType),
                        Index = i,
                    };
                    return (method, true);
                }
            }
            return (new GoReflectMethod(), false);
        }

        [GoMethod]
        [return: GoReturn("Method")]
        public GoReflectMethod Method(long index)
        {
            var methods = _clrType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (index < 0 || index >= methods.Length)
            {
                throw new GoPanicException("reflect: Method index out of range");
            }
            var mi = methods[(int)index];
            return new GoReflectMethod
            {
                Name = mi.Name,
                Type = new GoReflectType(mi.ReturnType),
                Index = index,
            };
        }

        [GoMethod]
        [return: GoReturn("Type")]
        public GoReflectType Elem()
        {
            var k = Kind();
            if (k == GoReflectKinds.Pointer)
            {
                // Ptr<T> → T
                if (_clrType.IsGenericType && _clrType.GetGenericTypeDefinition() == typeof(Ptr<>))
                    return new GoReflectType(_clrType.GetGenericArguments()[0]);
                return new GoReflectType(_clrType);
            }
            if (k == GoReflectKinds.Slice)
            {
                // Slice<T> → T
                if (_clrType.IsGenericType && _clrType.GetGenericTypeDefinition() == typeof(Slice<>))
                    return new GoReflectType(_clrType.GetGenericArguments()[0]);
            }
            if (k == GoReflectKinds.Array)
            {
                return new GoReflectType(_clrType.GetElementType()!);
            }
            if (k == GoReflectKinds.Map)
            {
                // Map<K,V> → V
                if (_clrType.IsGenericType && _clrType.GetGenericTypeDefinition() == typeof(Map<,>))
                    return new GoReflectType(_clrType.GetGenericArguments()[1]);
            }
            throw new GoPanicException("reflect: Elem of invalid type " + _goName);
        }

        [GoMethod]
        [return: GoReturn("Type")]
        public GoReflectType Key()
        {
            if (Kind() != GoReflectKinds.Map)
                throw new GoPanicException("reflect: Key of non-map type " + _goName);
            if (_clrType.IsGenericType && _clrType.GetGenericTypeDefinition() == typeof(Map<,>))
                return new GoReflectType(_clrType.GetGenericArguments()[0]);
            throw new GoPanicException("reflect: Key of non-map type " + _goName);
        }

        [GoMethod]
        public long Len()
        {
            if (Kind() != GoReflectKinds.Array)
                throw new GoPanicException("reflect: Len of non-array type " + _goName);
            // Fixed-size arrays are rare in ngo, return 0 as fallback
            return 0;
        }

        [GoMethod]
        public bool Comparable()
        {
            var k = Kind();
            return k == GoReflectKinds.Bool || k == GoReflectKinds.Int ||
                   k == GoReflectKinds.Int8 || k == GoReflectKinds.Int16 ||
                   k == GoReflectKinds.Int32 || k == GoReflectKinds.Int64 ||
                   k == GoReflectKinds.Uint || k == GoReflectKinds.Uint8 ||
                   k == GoReflectKinds.Uint16 || k == GoReflectKinds.Uint32 ||
                   k == GoReflectKinds.Uint64 || k == GoReflectKinds.Float32 ||
                   k == GoReflectKinds.Float64 || k == GoReflectKinds.String ||
                   k == GoReflectKinds.Pointer;
        }

        [GoMethod]
        public bool AssignableTo(GoReflectType u) => u._clrType.IsAssignableFrom(_clrType);
        [GoMethod]
        public bool ConvertibleTo(GoReflectType u)
        {
            if (_clrType == u._clrType)
            {
                return true;
            }
            if (u._clrType.IsAssignableFrom(_clrType))
            {
                return true;
            }
            // Numeric conversions
            bool srcNumeric = IsNumericType(_clrType);
            bool dstNumeric = IsNumericType(u._clrType);
            if (srcNumeric && dstNumeric)
            {
                return true;
            }
            // string <-> []byte
            if (_clrType == typeof(string) && u._clrType == typeof(Slice<byte>))
            {
                return true;
            }
            if (_clrType == typeof(Slice<byte>) && u._clrType == typeof(string))
            {
                return true;
            }
            return false;
        }

        private static bool IsNumericType(System.Type t)
        {
            return t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) ||
                   t == typeof(int) || t == typeof(uint) || t == typeof(long) || t == typeof(ulong) ||
                   t == typeof(float) || t == typeof(double);
        }
        [GoMethod]
        public bool Implements(GoReflectType u) => u._clrType.IsAssignableFrom(_clrType);

        [GoMethod]
        public long NumIn()
        {
            if (Kind() != GoReflectKinds.Func)
                throw new GoPanicException("reflect: NumIn of non-func type " + _goName);
            var invoke = _clrType.GetMethod("Invoke");
            return invoke?.GetParameters().Length ?? 0;
        }

        public GoReflectType In(long i)
        {
            if (Kind() != GoReflectKinds.Func)
                throw new GoPanicException("reflect: In of non-func type " + _goName);
            var invoke = _clrType.GetMethod("Invoke");
            var parms = invoke?.GetParameters();
            if (parms == null || i < 0 || i >= parms.Length)
                throw new GoPanicException("reflect: In index out of range");
            return new GoReflectType(parms[(int)i].ParameterType);
        }

        public long NumOut()
        {
            if (Kind() != GoReflectKinds.Func)
                throw new GoPanicException("reflect: NumOut of non-func type " + _goName);
            var invoke = _clrType.GetMethod("Invoke");
            if (invoke == null || invoke.ReturnType == typeof(void)) return 0;
            return 1;
        }

        public GoReflectType Out(long i)
        {
            if (Kind() != GoReflectKinds.Func)
                throw new GoPanicException("reflect: Out of non-func type " + _goName);
            var invoke = _clrType.GetMethod("Invoke");
            if (invoke == null) throw new GoPanicException("reflect: Out index out of range");
            return new GoReflectType(invoke.ReturnType);
        }

        public bool IsVariadic()
        {
            if (Kind() != GoReflectKinds.Func)
            {
                return false;
            }
            var invoke = _clrType.GetMethod("Invoke");
            if (invoke != null)
            {
                var parameters = invoke.GetParameters();
                if (parameters.Length > 0)
                {
                    var lastParam = parameters[parameters.Length - 1];
                    return lastParam.IsDefined(typeof(ParamArrayAttribute), false);
                }
            }
            return false;
        }

        public long Size()
        {
            try
            {
                return System.Runtime.InteropServices.Marshal.SizeOf(_clrType);
            }
            catch
            {
                return 0;
            }
        }

        [GoMethod]
        public long Bits()
        {
            if (_clrType == typeof(sbyte) || _clrType == typeof(byte)) return 8;
            if (_clrType == typeof(short) || _clrType == typeof(ushort)) return 16;
            if (_clrType == typeof(int) || _clrType == typeof(uint) || _clrType == typeof(float)) return 32;
            if (_clrType == typeof(long) || _clrType == typeof(ulong) || _clrType == typeof(double)) return 64;
            throw new GoPanicException("reflect: Bits of non-arithmetic Type " + _goName);
        }

        [GoMethod]
        public string PkgPath()
        {
            return "";
        }

        [GoMethod]
        public (GoReflectStructField, bool) FieldByName(string name)
        {
            if (Kind() != GoReflectKinds.Struct)
                throw new GoPanicException("reflect: FieldByName of non-struct type " + _goName);
            var fields = _clrType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Name == name)
                    return (new GoReflectStructField(fields[i].Name, new GoReflectType(fields[i].FieldType), "", i, false), true);
            }
            return (new GoReflectStructField("", new GoReflectType(typeof(object)), "", 0, false), false);
        }

        [GoMethod]
        public (GoReflectStructField, bool) FieldByNameFunc(Func<string, bool> match)
        {
            if (Kind() != GoReflectKinds.Struct)
                throw new GoPanicException("reflect: FieldByNameFunc of non-struct type " + _goName);
            var fields = _clrType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                if (match(fields[i].Name))
                    return (new GoReflectStructField(fields[i].Name, new GoReflectType(fields[i].FieldType), "", i, false), true);
            }
            return (new GoReflectStructField("", new GoReflectType(typeof(object)), "", 0, false), false);
        }

        [GoMethod]
        public GoReflectStructField FieldByIndex(Slice<long> index)
        {
            var t = this;
            GoReflectStructField? f = null;
            for (int i = 0; i < index.Len; i++)
                f = t.Field(index[i]);
            return f ?? new GoReflectStructField("", new GoReflectType(typeof(object)), "", 0, false);
        }

        [GoMethod]
        public long ChanDir()
        {
            // BothDir = 3, SendDir = 2, RecvDir = 1
            return 3; // stub — assume BothDir
        }

        [GoMethod]
        public (GoReflectStructField, bool) FieldByIndexErr(Slice<long> index)
        {
            try
            {
                var f = FieldByIndex(index);
                return (f, true);
            }
            catch
            {
                return (new GoReflectStructField("", new GoReflectType(typeof(object)), "", 0, false), false);
            }
        }

        internal Type ClrType => _clrType;

        public override string ToString() => _goName;

        public override bool Equals(object? obj) => obj is GoReflectType other && _clrType == other._clrType;
        public override int GetHashCode() => _clrType.GetHashCode();

        internal static long DeriveKind(Type t)
        {
            if (t == typeof(bool)) return GoReflectKinds.Bool;
            if (t == typeof(long)) return GoReflectKinds.Int;
            if (t == typeof(sbyte)) return GoReflectKinds.Int8;
            if (t == typeof(short)) return GoReflectKinds.Int16;
            if (t == typeof(int)) return GoReflectKinds.Int32;
            if (t == typeof(long)) return GoReflectKinds.Int64;
            if (t == typeof(ulong)) return GoReflectKinds.Uint64;
            if (t == typeof(uint)) return GoReflectKinds.Uint32;
            if (t == typeof(ushort)) return GoReflectKinds.Uint16;
            if (t == typeof(byte)) return GoReflectKinds.Uint8;
            if (t == typeof(float)) return GoReflectKinds.Float32;
            if (t == typeof(double)) return GoReflectKinds.Float64;
            if (t == typeof(string)) return GoReflectKinds.String;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Slice<>)) return GoReflectKinds.Slice;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Map<,>)) return GoReflectKinds.Map;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Channel<>)) return GoReflectKinds.Chan;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Ptr<>)) return GoReflectKinds.Pointer;
            if (t.IsArray) return GoReflectKinds.Array;
            if (t.IsInterface || t == typeof(object)) return GoReflectKinds.Interface;
            if (typeof(Delegate).IsAssignableFrom(t)) return GoReflectKinds.Func;
            if (t.IsValueType) return GoReflectKinds.Struct;
            // Classes (reference types) that aren't special → struct in Go terms
            if (t.IsClass && t != typeof(string) && t != typeof(object)) return GoReflectKinds.Struct;
            return GoReflectKinds.Interface;
        }

        internal static string DeriveGoName(Type t)
        {
            if (t == typeof(bool)) return "bool";
            if (t == typeof(long)) return "int";
            if (t == typeof(sbyte)) return "int8";
            if (t == typeof(short)) return "int16";
            if (t == typeof(int)) return "int32";
            if (t == typeof(long)) return "int64";
            if (t == typeof(ulong)) return "uint64";
            if (t == typeof(uint)) return "uint32";
            if (t == typeof(ushort)) return "uint16";
            if (t == typeof(byte)) return "uint8";
            if (t == typeof(float)) return "float32";
            if (t == typeof(double)) return "float64";
            if (t == typeof(string)) return "string";
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Slice<>))
                return "[]" + DeriveGoName(t.GetGenericArguments()[0]);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Map<,>))
                return "map[" + DeriveGoName(t.GetGenericArguments()[0]) + "]" + DeriveGoName(t.GetGenericArguments()[1]);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Channel<>))
                return "chan " + DeriveGoName(t.GetGenericArguments()[0]);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Ptr<>))
                return "*" + DeriveGoName(t.GetGenericArguments()[0]);
            if (t == typeof(object)) return "interface {}";
            if (t.IsInterface) return "interface {}";
            return t.Name;
        }
    }
}

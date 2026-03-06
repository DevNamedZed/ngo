// -----------------------------------------------------------------------
// <copyright file="GoReflect.cs" company="Ziad">
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Ngo.Runtime
{
    /// <summary>
    /// Go reflect package: Kind constants.
    /// </summary>
    public static class GoReflectKinds
    {
        public static readonly long Invalid = 0;
        public static readonly long Bool = 1;
        public static readonly long Int = 2;
        public static readonly long Int8 = 3;
        public static readonly long Int16 = 4;
        public static readonly long Int32 = 5;
        public static readonly long Int64 = 6;
        public static readonly long Uint = 7;
        public static readonly long Uint8 = 8;
        public static readonly long Uint16 = 9;
        public static readonly long Uint32 = 10;
        public static readonly long Uint64 = 11;
        public static readonly long Uintptr = 12;
        public static readonly long Float32 = 13;
        public static readonly long Float64 = 14;
        public static readonly long Complex64 = 15;
        public static readonly long Complex128 = 16;
        public static readonly long Array = 17;
        public static readonly long Chan = 18;
        public static readonly long Func = 19;
        public static readonly long Interface = 20;
        public static readonly long Map = 21;
        public static readonly long Pointer = 22;
        public static readonly long Slice = 23;
        public static readonly long String = 24;
        public static readonly long Struct = 25;
        public static readonly long UnsafePointer = 26;

        // Alias
        public static readonly long Ptr = Pointer;

        public static string KindToString(long kind)
        {
            return kind switch
            {
                0 => "invalid",
                1 => "bool",
                2 => "int",
                3 => "int8",
                4 => "int16",
                5 => "int32",
                6 => "int64",
                7 => "uint",
                8 => "uint8",
                9 => "uint16",
                10 => "uint32",
                11 => "uint64",
                12 => "uintptr",
                13 => "float32",
                14 => "float64",
                15 => "complex64",
                16 => "complex128",
                17 => "array",
                18 => "chan",
                19 => "func",
                20 => "interface",
                21 => "map",
                22 => "ptr",
                23 => "slice",
                24 => "string",
                25 => "struct",
                26 => "unsafe.Pointer",
                _ => "invalid",
            };
        }
    }

    /// <summary>
    /// Go reflect.Type — wraps .NET System.Type.
    /// </summary>
    public sealed class GoReflectType
    {
        private readonly Type _clrType;
        private readonly string _goName;

        internal GoReflectType(Type clrType, string? goName = null)
        {
            _clrType = clrType;
            _goName = goName ?? DeriveGoName(clrType);
        }

        public string Name() => _goName;

        public long Kind() => DeriveKind(_clrType);

        public string String() => _goName;

        public long NumField()
        {
            if (Kind() != GoReflectKinds.Struct)
                throw new GoPanicException("reflect: NumField of non-struct type " + _goName);
            return _clrType.GetFields(BindingFlags.Public | BindingFlags.Instance).Length;
        }

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

        public long NumMethod()
        {
            return _clrType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length;
        }

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

        public GoReflectType Key()
        {
            if (Kind() != GoReflectKinds.Map)
                throw new GoPanicException("reflect: Key of non-map type " + _goName);
            if (_clrType.IsGenericType && _clrType.GetGenericTypeDefinition() == typeof(Map<,>))
                return new GoReflectType(_clrType.GetGenericArguments()[0]);
            throw new GoPanicException("reflect: Key of non-map type " + _goName);
        }

        public long Len()
        {
            if (Kind() != GoReflectKinds.Array)
                throw new GoPanicException("reflect: Len of non-array type " + _goName);
            // Fixed-size arrays are rare in ngo, return 0 as fallback
            return 0;
        }

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

        public bool AssignableTo(GoReflectType u) => u._clrType.IsAssignableFrom(_clrType);
        public bool Implements(GoReflectType u) => u._clrType.IsAssignableFrom(_clrType);

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

    /// <summary>
    /// Go reflect.StructField.
    /// </summary>
    public sealed class GoReflectStructField
    {
        public string Name { get; }
        public GoReflectType Type { get; }
        public string Tag { get; }
        public long Index { get; }
        public bool Anonymous { get; }

        internal GoReflectStructField(string name, GoReflectType type, string tag, int index, bool anonymous)
        {
            Name = name;
            Type = type;
            Tag = tag;
            Index = index;
            Anonymous = anonymous;
        }

        public override string ToString() => $"{Name} {Type}";
    }

    /// <summary>
    /// Go reflect.Value — wraps a .NET object with type information.
    /// </summary>
    public sealed class GoReflectValue
    {
        private object? _value;
        private readonly GoReflectType _type;
        private readonly bool _canSet;

        internal GoReflectValue(object? value, GoReflectType type, bool canSet = false)
        {
            _value = value;
            _type = type;
            _canSet = canSet;
        }

        // Zero value
        internal static readonly GoReflectValue InvalidValue = new(null, new GoReflectType(typeof(void), "invalid"), false);

        public long Kind() => _type.Kind();

        public GoReflectType Type() => _type;

        public bool IsValid() => _value != null || _type.Kind() != GoReflectKinds.Invalid;

        public bool IsNil()
        {
            return _value == null;
        }

        public bool IsZero()
        {
            if (_value == null) return true;
            var k = Kind();
            if (k == GoReflectKinds.Bool) return !(bool)_value;
            if (k == GoReflectKinds.Int || k == GoReflectKinds.Int64) return (long)_value == 0;
            if (k == GoReflectKinds.Int32) return (int)_value == 0;
            if (k == GoReflectKinds.Float64) return (double)_value == 0.0;
            if (k == GoReflectKinds.Float32) return (float)_value == 0.0f;
            if (k == GoReflectKinds.String) return (string)_value == "";
            return false;
        }

        public object? Interface() => _value;

        public long Int()
        {
            if (_value is long l) return l;
            if (_value is int i) return i;
            if (_value is short s) return s;
            if (_value is sbyte sb) return sb;
            return Convert.ToInt64(_value);
        }

        public double Float()
        {
            if (_value is double d) return d;
            if (_value is float f) return f;
            return Convert.ToDouble(_value);
        }

        public bool Bool()
        {
            if (_value is bool b) return b;
            throw new GoPanicException("reflect: call of Value.Bool on " + GoReflectKinds.KindToString(Kind()) + " Value");
        }

        public string String()
        {
            if (_value is string s) return s;
            if (Kind() == GoReflectKinds.Invalid) return "<invalid reflect.Value>";
            return "<" + _type.String() + " Value>";
        }

        public long Len()
        {
            if (_value is string s) return s.Length;
            if (_value is ICollection c) return c.Count;
            // Try Slice<T>.Len property
            var lenProp = _value?.GetType().GetProperty("Len");
            if (lenProp != null) return Convert.ToInt64(lenProp.GetValue(_value));
            throw new GoPanicException("reflect: call of Value.Len on " + GoReflectKinds.KindToString(Kind()) + " Value");
        }

        public GoReflectValue Index(long i)
        {
            // Slice<T> — use indexer
            if (_value != null)
            {
                var indexer = _value.GetType().GetProperty("Item");
                if (indexer != null)
                {
                    var elem = indexer.GetValue(_value, new object[] { (int)i });
                    return new GoReflectValue(elem, new GoReflectType(elem?.GetType() ?? typeof(object)));
                }
            }
            throw new GoPanicException("reflect: call of Value.Index on " + GoReflectKinds.KindToString(Kind()) + " Value");
        }

        public Slice<GoReflectValue> MapKeys()
        {
            if (Kind() != GoReflectKinds.Map || _value == null)
                throw new GoPanicException("reflect: call of Value.MapKeys on " + GoReflectKinds.KindToString(Kind()) + " Value");
            // Map<K,V> — use Keys() or iterate
            var keysMethod = _value.GetType().GetMethod("Keys");
            if (keysMethod != null)
            {
                var keys = (IEnumerable)keysMethod.Invoke(_value, null)!;
                var result = new List<GoReflectValue>();
                foreach (var k in keys)
                    result.Add(new GoReflectValue(k, new GoReflectType(k?.GetType() ?? typeof(object))));
                return new Slice<GoReflectValue>(result.ToArray());
            }
            return new Slice<GoReflectValue>();
        }

        public GoReflectValue MapIndex(GoReflectValue key)
        {
            if (_value == null)
                return InvalidValue;
            // Map<K,V> — use Get method
            var getMethod = _value.GetType().GetMethod("Get");
            if (getMethod != null)
            {
                var val = getMethod.Invoke(_value, new[] { key._value });
                if (val == null) return InvalidValue;
                return new GoReflectValue(val, new GoReflectType(val.GetType()));
            }
            return InvalidValue;
        }

        public long NumField()
        {
            return _type.NumField();
        }

        public GoReflectValue Field(long i)
        {
            if (Kind() != GoReflectKinds.Struct || _value == null)
                throw new GoPanicException("reflect: call of Value.Field on " + GoReflectKinds.KindToString(Kind()) + " Value");
            var fields = _value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            if (i < 0 || i >= fields.Length)
                throw new GoPanicException("reflect: Field index out of range");
            var f = fields[(int)i];
            var val = f.GetValue(_value);
            return new GoReflectValue(val, new GoReflectType(f.FieldType), _canSet);
        }

        public GoReflectValue FieldByName(string name)
        {
            if (Kind() != GoReflectKinds.Struct || _value == null)
                throw new GoPanicException("reflect: call of Value.FieldByName on " + GoReflectKinds.KindToString(Kind()) + " Value");
            var f = _value.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return InvalidValue;
            var val = f.GetValue(_value);
            return new GoReflectValue(val, new GoReflectType(f.FieldType), _canSet);
        }

        public GoReflectValue MethodByName(string name)
        {
            if (_value == null) return InvalidValue;
            var m = _value.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            if (m == null) return InvalidValue;
            // Return a Value wrapping the method as a delegate-like
            return new GoReflectValue(m, new GoReflectType(typeof(Delegate), "func"), false);
        }

        public Slice<GoReflectValue> Call(Slice<GoReflectValue> args)
        {
            if (_value is MethodInfo m)
            {
                // We need the target — this is only for method values
                throw new GoPanicException("reflect: call of Value.Call requires method value");
            }
            if (_value is Delegate d)
            {
                var parameters = new object?[args.Len];
                for (int i = 0; i < args.Len; i++)
                    parameters[i] = args[i].Interface();
                var result = d.DynamicInvoke(parameters);
                if (result == null)
                    return new Slice<GoReflectValue>();
                return new Slice<GoReflectValue>(new[] { GoReflect.ValueOf(result) });
            }
            throw new GoPanicException("reflect: call of Value.Call on " + GoReflectKinds.KindToString(Kind()) + " Value");
        }

        public GoReflectValue Elem()
        {
            var k = Kind();
            if (k == GoReflectKinds.Pointer)
            {
                if (_value == null) throw new GoPanicException("reflect: call of Value.Elem on nil Ptr Value");
                // Ptr<T> — get Value
                var valProp = _value.GetType().GetProperty("Value");
                if (valProp != null)
                {
                    var inner = valProp.GetValue(_value);
                    return new GoReflectValue(inner, new GoReflectType(inner?.GetType() ?? typeof(object)), true);
                }
                return new GoReflectValue(_value, new GoReflectType(_value.GetType()), true);
            }
            if (k == GoReflectKinds.Interface)
            {
                if (_value == null) return InvalidValue;
                return new GoReflectValue(_value, new GoReflectType(_value.GetType()), false);
            }
            throw new GoPanicException("reflect: call of Value.Elem on " + GoReflectKinds.KindToString(k) + " Value");
        }

        public bool CanSet() => _canSet;
        public bool CanInterface() => true;

        public void Set(GoReflectValue x)
        {
            if (!_canSet)
                throw new GoPanicException("reflect: call of Value.Set on unaddressable Value");
            _value = x._value;
        }

        public void SetInt(long x)
        {
            if (!_canSet)
                throw new GoPanicException("reflect: call of Value.SetInt on unaddressable Value");
            _value = x;
        }

        public void SetFloat(double x)
        {
            if (!_canSet)
                throw new GoPanicException("reflect: call of Value.SetFloat on unaddressable Value");
            _value = x;
        }

        public void SetString(string x)
        {
            if (!_canSet)
                throw new GoPanicException("reflect: call of Value.SetString on unaddressable Value");
            _value = x;
        }

        public void SetBool(bool x)
        {
            if (!_canSet)
                throw new GoPanicException("reflect: call of Value.SetBool on unaddressable Value");
            _value = x;
        }

        public override string ToString()
        {
            if (_value == null) return "<nil>";
            return _value.ToString() ?? "";
        }
    }

    /// <summary>
    /// Go reflect package top-level functions.
    /// </summary>
    public static class GoReflect
    {
        public static GoReflectType TypeOf(object? v)
        {
            if (v == null)
                return new GoReflectType(typeof(object), "interface {}");
            return new GoReflectType(v.GetType());
        }

        public static GoReflectValue ValueOf(object? v)
        {
            if (v == null)
                return GoReflectValue.InvalidValue;
            return new GoReflectValue(v, new GoReflectType(v.GetType()));
        }

        public static bool DeepEqual(object? a, object? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.GetType() != b.GetType()) return false;

            // String comparison
            if (a is string sa && b is string sb)
                return sa == sb;

            // Value type comparison
            if (a.GetType().IsValueType)
                return a.Equals(b);

            // Slice comparison
            var aType = a.GetType();
            if (aType.IsGenericType && aType.GetGenericTypeDefinition() == typeof(Slice<>))
            {
                var lenProp = aType.GetProperty("Len")!;
                var aLen = (int)lenProp.GetValue(a)!;
                var bLen = (int)lenProp.GetValue(b)!;
                if (aLen != bLen) return false;
                var indexer = aType.GetProperty("Item")!;
                for (int i = 0; i < aLen; i++)
                {
                    var aElem = indexer.GetValue(a, new object[] { i });
                    var bElem = indexer.GetValue(b, new object[] { i });
                    if (!DeepEqual(aElem, bElem)) return false;
                }
                return true;
            }

            // Map comparison
            if (aType.IsGenericType && aType.GetGenericTypeDefinition() == typeof(Map<,>))
            {
                var lenProp = aType.GetProperty("Len")!;
                var aLen = (int)lenProp.GetValue(a)!;
                var bLen = (int)lenProp.GetValue(b)!;
                if (aLen != bLen) return false;
                // Use Keys() and Get()
                var keysMethod = aType.GetMethod("Keys")!;
                var getMethod = aType.GetMethod("Get")!;
                var keys = (IEnumerable)keysMethod.Invoke(a, null)!;
                foreach (var key in keys)
                {
                    var aVal = getMethod.Invoke(a, new[] { key });
                    var bVal = getMethod.Invoke(b, new[] { key });
                    if (!DeepEqual(aVal, bVal)) return false;
                }
                return true;
            }

            return Equals(a, b);
        }

        public static GoReflectValue Zero(GoReflectType typ)
        {
            var clrType = typ.ClrType;
            object? zero = null;
            if (clrType.IsValueType)
                zero = Activator.CreateInstance(clrType);
            return new GoReflectValue(zero, typ);
        }

        public static GoReflectValue New(GoReflectType typ)
        {
            var clrType = typ.ClrType;
            var instance = Activator.CreateInstance(clrType);
            // Return a Ptr-like value
            return new GoReflectValue(instance, new GoReflectType(clrType, "*" + typ.Name()), true);
        }

        public static GoReflectValue MakeSlice(GoReflectType typ, long len, long cap)
        {
            // typ is the slice type — get element type
            var elemType = typ.ClrType.GetGenericArguments()[0];
            var sliceType = typeof(Slice<>).MakeGenericType(elemType);
            var instance = Activator.CreateInstance(sliceType);
            return new GoReflectValue(instance, typ);
        }

        public static GoReflectValue MakeMap(GoReflectType typ)
        {
            var args = typ.ClrType.GetGenericArguments();
            var mapType = typeof(Map<,>).MakeGenericType(args);
            var instance = Activator.CreateInstance(mapType);
            return new GoReflectValue(instance!, typ);
        }

        public static GoReflectValue Append(GoReflectValue s, params GoReflectValue[] elems)
        {
            // Use the runtime Slice<T>.Append pattern
            if (s.Interface() == null)
                throw new GoPanicException("reflect: Append of nil slice");
            var sliceObj = s.Interface()!;
            var sliceType = sliceObj.GetType();
            var appendMethod = sliceType.GetMethod("Append");
            if (appendMethod != null)
            {
                foreach (var e in elems)
                    sliceObj = appendMethod.Invoke(sliceObj, new[] { e.Interface() })!;
                return new GoReflectValue(sliceObj, s.Type());
            }
            return s;
        }
    }
}

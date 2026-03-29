// -----------------------------------------------------------------------
// <copyright file="GoReflectValue.cs" company="Ziad">
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
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Reflect
{
    /// <summary>
    /// Go reflect.Value — wraps a .NET object with type information.
    /// </summary>
    [GoType("struct", Name = "Value", Package = "reflect")]
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

        [GoMethod]
        [return: GoReturn("Kind")]
        public long Kind() => _type.Kind();

        [GoMethod]
        [return: GoReturn("Type")]
        public GoReflectType Type() => _type;

        [GoMethod]
        public bool IsValid() => _value != null || _type.Kind() != GoReflectKinds.Invalid;

        [GoMethod]
        public bool IsNil()
        {
            return _value == null;
        }

        [GoMethod]
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

        [GoMethod]
        public object? Interface() => _value;

        public long Int()
        {
            if (_value is long l) return l;
            if (_value is int i) return i;
            if (_value is short s) return s;
            if (_value is sbyte sb) return sb;
            return System.Convert.ToInt64(_value);
        }

        public double Float()
        {
            if (_value is double d) return d;
            if (_value is float f) return f;
            return System.Convert.ToDouble(_value);
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
            if (lenProp != null) return System.Convert.ToInt64(lenProp.GetValue(_value));
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

        [GoMethod]
        [return: GoReturn("Value")]
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

        [GoMethod]
        public void SetUint(long x)
        {
            if (!_canSet)
                throw new GoPanicException("reflect: call of Value.SetUint on unaddressable Value");
            _value = x;
        }

        [GoMethod]
        public void SetComplex(object x)
        {
            if (!_canSet)
                throw new GoPanicException("reflect: call of Value.SetComplex on unaddressable Value");
            _value = x;
        }

        [GoMethod]
        public void SetMapIndex(GoReflectValue key, GoReflectValue val)
        {
            if (_value == null)
                throw new GoPanicException("reflect: call of Value.SetMapIndex on nil Value");
            var setMethod = _value.GetType().GetMethod("Set");
            if (setMethod != null)
            {
                setMethod.Invoke(_value, new[] { key._value, val._value });
            }
        }

        [GoMethod]
        public void SetZero()
        {
            if (!_canSet)
                throw new GoPanicException("reflect: call of Value.SetZero on unaddressable Value");
            var k = Kind();
            if (k == GoReflectKinds.Bool) _value = false;
            else if (k == GoReflectKinds.Int || k == GoReflectKinds.Int64) _value = 0L;
            else if (k == GoReflectKinds.Float64) _value = 0.0;
            else if (k == GoReflectKinds.String) _value = "";
            else _value = null;
        }

        [GoMethod]
        public long Uint()
        {
            if (_value is long l) return l;
            if (_value is ulong ul) return (long)ul;
            if (_value is uint ui) return (long)ui;
            if (_value is ushort us) return (long)us;
            if (_value is byte b) return (long)b;
            return System.Convert.ToInt64(_value);
        }

        [GoMethod]
        public long Pointer()
        {
            if (_value == null)
            {
                return 0;
            }
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_value);
        }

        [GoMethod]
        public long UnsafePointer()
        {
            return Pointer();
        }

        [GoMethod]
        public GoReflectValue Addr()
        {
            return new GoReflectValue(_value, new GoReflectType(typeof(Ptr<>), "*" + _type.String()), false);
        }

        [GoMethod]
        public long Cap()
        {
            if (_value != null)
            {
                var capProp = _value.GetType().GetProperty("Cap");
                if (capProp != null) return System.Convert.ToInt64(capProp.GetValue(_value));
            }
            throw new GoPanicException("reflect: call of Value.Cap on " + GoReflectKinds.KindToString(Kind()) + " Value");
        }

        [GoMethod]
        public GoReflectValue Slice(long i, long j)
        {
            if (_value == null)
            {
                return InvalidValue;
            }
            var type = _value.GetType();
            var resliceMethod = type.GetMethod("Reslice", new[] { typeof(int), typeof(int) });
            if (resliceMethod != null)
            {
                var result = resliceMethod.Invoke(_value, new object[] { (int)i, (int)j });
                if (result != null)
                {
                    return new GoReflectValue(result, _type, _canSet);
                }
            }
            return this;
        }

        [GoMethod]
        public GoReflectValue Slice3(long i, long j, long k)
        {
            if (_value == null)
            {
                return InvalidValue;
            }
            var type = _value.GetType();
            var resliceMethod = type.GetMethod("Reslice", new[] { typeof(int), typeof(int), typeof(int) });
            if (resliceMethod != null)
            {
                var result = resliceMethod.Invoke(_value, new object[] { (int)i, (int)j, (int)k });
                if (result != null)
                {
                    return new GoReflectValue(result, _type, _canSet);
                }
            }
            return this;
        }

        [GoMethod]
        public (GoReflectValue, bool) Recv()
        {
            if (_value == null)
            {
                return (GoReflectValue.InvalidValue, false);
            }
            var receiveMethod = _value.GetType().GetMethod("Receive");
            if (receiveMethod != null)
            {
                var result = receiveMethod.Invoke(_value, null);
                if (result != null)
                {
                    var resultType = result.GetType();
                    var item1 = resultType.GetField("Item1")?.GetValue(result);
                    var item2 = resultType.GetField("Item2")?.GetValue(result);
                    bool ok = item2 is bool b && b;
                    return (item1 != null ? Reflect.GoReflect.ValueOf(item1) : GoReflectValue.InvalidValue, ok);
                }
            }
            return (GoReflectValue.InvalidValue, false);
        }

        [GoMethod]
        public void Send(GoReflectValue x)
        {
            if (_value == null)
            {
                return;
            }
            var sendMethod = _value.GetType().GetMethod("Send");
            if (sendMethod != null)
            {
                sendMethod.Invoke(_value, new[] { x.Interface() });
            }
        }

        [GoMethod]
        public long NumMethod()
        {
            return _type.NumMethod();
        }

        [GoMethod]
        public GoReflectValue Convert(GoReflectType t)
        {
            return new GoReflectValue(_value, t, _canSet);
        }

        [GoMethod]
        public bool CanAddr() => _canSet;

        [GoMethod]
        public bool CanConvert(GoReflectType t)
        {
            if (_value == null)
            {
                return false;
            }
            // Basic numeric conversions are always possible
            var srcKind = Kind();
            var dstKind = t.Kind();
            bool srcNumeric = srcKind >= GoReflectKinds.Int && srcKind <= GoReflectKinds.Float64;
            bool dstNumeric = dstKind >= GoReflectKinds.Int && dstKind <= GoReflectKinds.Float64;
            if (srcNumeric && dstNumeric)
            {
                return true;
            }
            // String <-> []byte
            if (srcKind == GoReflectKinds.String && dstKind == GoReflectKinds.Slice)
            {
                return true;
            }
            if (srcKind == GoReflectKinds.Slice && dstKind == GoReflectKinds.String)
            {
                return true;
            }
            // Same kind is always convertible
            if (srcKind == dstKind)
            {
                return true;
            }
            return false;
        }

        [GoMethod]
        public bool OverflowInt(long x)
        {
            long kind = Kind();
            return kind switch
            {
                2 => x < sbyte.MinValue || x > sbyte.MaxValue,   // Int8
                3 => x < short.MinValue || x > short.MaxValue,   // Int16
                4 => x < int.MinValue || x > int.MaxValue,       // Int32
                _ => false, // Int64/Int — no overflow possible in long
            };
        }

        [GoMethod]
        public bool OverflowUint(long x)
        {
            long kind = Kind();
            return kind switch
            {
                8 => x < 0 || x > byte.MaxValue,     // Uint8
                9 => x < 0 || x > ushort.MaxValue,   // Uint16
                10 => x < 0 || x > uint.MaxValue,    // Uint32
                _ => x < 0, // Uint64/Uint/Uintptr — negative overflows
            };
        }

        [GoMethod]
        public bool OverflowFloat(double x)
        {
            long kind = Kind();
            if (kind == 13) // Float32
            {
                return x > float.MaxValue || x < -float.MaxValue;
            }
            return false; // Float64 — no overflow possible in double
        }

        [GoMethod]
        public bool OverflowComplex(object x) => false; // Complex128 — no overflow possible

        [GoMethod]
        [return: GoReturn("complex128")]
        public long Complex()
        {
            if (_value is System.Numerics.Complex complexValue)
            {
                return BitConverter.DoubleToInt64Bits(complexValue.Real);
            }
            if (_value is double doubleValue)
            {
                return BitConverter.DoubleToInt64Bits(doubleValue);
            }
            return 0;
        }

        [GoMethod]
        public void SetLen(long n)
        {
            if (_value == null)
            {
                return;
            }
            // Try to call Reslice(0, n) on slice types
            var type = _value.GetType();
            var reslice = type.GetMethod("Reslice", new[] { typeof(int), typeof(int) });
            if (reslice != null)
            {
                _value = reslice.Invoke(_value, new object[] { 0, (int)n });
            }
        }

        [GoMethod]
        public void SetCap(long n)
        {
            // SetCap is not directly supported — Slice<T> manages capacity internally
        }

        [GoMethod]
        public Slice<byte> Bytes()
        {
            if (_value is Slice<byte> sb) return sb;
            if (_value is byte[] ba) return new Slice<byte>(ba);
            throw new GoPanicException("reflect: call of Value.Bytes on " + GoReflectKinds.KindToString(Kind()) + " Value");
        }

        [GoMethod]
        public void SetBytes(Slice<byte> x)
        {
            _value = x;
        }

        [GoMethod]
        public GoReflectValue FieldByIndex(Slice<long> index)
        {
            var v = this;
            for (int i = 0; i < index.Len; i++)
                v = v.Field(index[i]);
            return v;
        }

        [GoMethod]
        public (GoReflectValue, object?) FieldByIndexErr(Slice<long> index)
        {
            try
            {
                return (FieldByIndex(index), null);
            }
            catch (Exception ex)
            {
                return (GoReflectValue.InvalidValue, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("reflect.Value")]
        public GoReflectValue FieldByNameFunc(Func<string, bool> match)
        {
            return GoReflectValue.InvalidValue;
        }

        [GoMethod]
        public GoReflectValue Method(long i)
        {
            if (_value == null)
            {
                return GoReflectValue.InvalidValue;
            }
            var methods = _value.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (i < 0 || i >= methods.Length)
            {
                return GoReflectValue.InvalidValue;
            }
            var method = methods[(int)i];
            Func<Slice<GoReflectValue>, Slice<GoReflectValue>> boundMethod = (args) =>
            {
                var paramInfos = method.GetParameters();
                var callArgs = new object?[paramInfos.Length];
                for (int idx = 0; idx < paramInfos.Length && idx < args.Len; idx++)
                {
                    callArgs[idx] = args[idx].Interface();
                }
                var result = method.Invoke(_value, callArgs);
                if (result == null)
                {
                    return new Slice<GoReflectValue>(System.Array.Empty<GoReflectValue>());
                }
                return new Slice<GoReflectValue>(new[] { GoReflect.ValueOf(result) });
            };
            return new GoReflectValue(boundMethod, new GoReflectType(method.GetType()), false);
        }

        [GoMethod]
        public GoReflectMapIter MapRange()
        {
            return new GoReflectMapIter();
        }

        [GoMethod]
        public void Grow(long n)
        {
            // Grow ensures the slice has capacity for n more elements
            // In .NET, Slice<T> grows automatically on Append, so this is a no-op
        }

        [GoMethod]
        public bool CanFloat() => Kind() == GoReflectKinds.Float32 || Kind() == GoReflectKinds.Float64;
        [GoMethod]
        public bool CanInt() => Kind() >= GoReflectKinds.Int && Kind() <= GoReflectKinds.Int64;
        [GoMethod]
        public bool CanUint() => Kind() >= GoReflectKinds.Uint && Kind() <= GoReflectKinds.Uint64;
        [GoMethod]
        public bool CanComplex() => Kind() == GoReflectKinds.Complex64 || Kind() == GoReflectKinds.Complex128;

        [GoMethod]
        public bool TrySend(GoReflectValue x)
        {
            if (_value == null)
            {
                return false;
            }
            var sendMethod = _value.GetType().GetMethod("TrySend");
            if (sendMethod != null)
            {
                var result = sendMethod.Invoke(_value, new[] { x.Interface() });
                return result is bool b && b;
            }
            return false;
        }

        [GoMethod]
        public (GoReflectValue, bool) TryRecv()
        {
            if (_value == null)
            {
                return (GoReflectValue.InvalidValue, false);
            }
            var tryReceiveMethod = _value.GetType().GetMethod("TryReceive");
            if (tryReceiveMethod != null)
            {
                var result = tryReceiveMethod.Invoke(_value, null);
                if (result != null)
                {
                    var resultType = result.GetType();
                    var item1 = resultType.GetField("Item1")?.GetValue(result);
                    var item2 = resultType.GetField("Item2")?.GetValue(result);
                    bool ok = item2 is bool okVal && okVal;
                    return (item1 != null ? Reflect.GoReflect.ValueOf(item1) : GoReflectValue.InvalidValue, ok);
                }
            }
            return (GoReflectValue.InvalidValue, false);
        }

        [GoMethod]
        public void SetPointer(long x)
        {
            if (!_canSet)
            {
                throw new GoPanicException("reflect: call of Value.SetPointer on unaddressable Value");
            }
            _value = x;
        }

        [GoMethod]
        [return: GoReturn("[2]uintptr")]
        public Slice<long> InterfaceData()
        {
            return new Slice<long>(new long[] { 0, 0 });
        }

        [GoMethod]
        public void SetIterKey([GoParam("*MapIter")] GoReflectMapIter iter)
        {
            if (!_canSet)
            {
                throw new GoPanicException("reflect: call of Value.SetIterKey on unaddressable Value");
            }
        }

        [GoMethod]
        public void SetIterValue([GoParam("*MapIter")] GoReflectMapIter iter)
        {
            if (!_canSet)
            {
                throw new GoPanicException("reflect: call of Value.SetIterValue on unaddressable Value");
            }
        }

        [GoMethod]
        public void Close()
        {
            if (_value == null)
            {
                throw new GoPanicException("reflect: call of Value.Close on zero Value");
            }
            var closeMethod = _value.GetType().GetMethod("Close");
            if (closeMethod != null)
            {
                closeMethod.Invoke(_value, null);
            }
        }

        [GoMethod]
        public long UnsafeAddr()
        {
            return Pointer();
        }

        [GoMethod]
        public Slice<GoReflectValue> CallSlice(Slice<GoReflectValue> args)
        {
            // CallSlice calls a variadic function with the last arg as an already-packed slice.
            // In ngo's runtime model, Call already handles slices directly.
            return Call(args);
        }

        [GoMethod]
        public GoReflectValue Resolve()
        {
            // In ngo, interface values are already unwrapped to concrete types.
            return this;
        }

        public override string ToString()
        {
            if (_value == null) return "<nil>";
            return _value.ToString() ?? "";
        }
    }
}

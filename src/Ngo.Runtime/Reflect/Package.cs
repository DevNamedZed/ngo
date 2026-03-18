// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
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
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Reflect
{
    /// <summary>
    /// Go reflect package top-level functions.
    /// </summary>
    [GoPackage("reflect")]
    public static class GoReflect
    {
        // Kind constants (mirror GoReflectKinds for package-level access)
        [GoConst] public static readonly long Invalid = GoReflectKinds.Invalid;
        [GoConst] public static readonly long Bool = GoReflectKinds.Bool;
        [GoConst] public static readonly long Int = GoReflectKinds.Int;
        [GoConst] public static readonly long Int8 = GoReflectKinds.Int8;
        [GoConst] public static readonly long Int16 = GoReflectKinds.Int16;
        [GoConst] public static readonly long Int32 = GoReflectKinds.Int32;
        [GoConst] public static readonly long Int64 = GoReflectKinds.Int64;
        [GoConst] public static readonly long Uint = GoReflectKinds.Uint;
        [GoConst] public static readonly long Uint8 = GoReflectKinds.Uint8;
        [GoConst] public static readonly long Uint16 = GoReflectKinds.Uint16;
        [GoConst] public static readonly long Uint32 = GoReflectKinds.Uint32;
        [GoConst] public static readonly long Uint64 = GoReflectKinds.Uint64;
        [GoConst] public static readonly long Uintptr = GoReflectKinds.Uintptr;
        [GoConst] public static readonly long Float32 = GoReflectKinds.Float32;
        [GoConst] public static readonly long Float64 = GoReflectKinds.Float64;
        [GoConst] public static readonly long Complex64 = GoReflectKinds.Complex64;
        [GoConst] public static readonly long Complex128 = GoReflectKinds.Complex128;
        [GoConst] public static readonly long Array = GoReflectKinds.Array;
        [GoConst] public static readonly long Chan = GoReflectKinds.Chan;
        [GoConst] public static readonly long Func = GoReflectKinds.Func;
        [GoConst] public static readonly long Interface = GoReflectKinds.Interface;
        [GoConst] public static readonly long Map = GoReflectKinds.Map;
        [GoConst] public static readonly long Pointer = GoReflectKinds.Pointer;
        [GoConst] public static readonly long Slice = GoReflectKinds.Slice;
        [GoConst] public static readonly long String = GoReflectKinds.String;
        [GoConst] public static readonly long Struct = GoReflectKinds.Struct;
        [GoConst] public static readonly long UnsafePointer = GoReflectKinds.UnsafePointer;
        [GoConst] public static readonly long Ptr = GoReflectKinds.Ptr;

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

        public static GoReflectValue MakeMapWithSize(GoReflectType typ, long n)
        {
            return MakeMap(typ);
        }

        public static GoReflectValue MakeMap(GoReflectType typ)
        {
            var args = typ.ClrType.GetGenericArguments();
            var mapType = typeof(Map<,>).MakeGenericType(args);
            var instance = Activator.CreateInstance(mapType);
            return new GoReflectValue(instance!, typ);
        }

        public static (long, GoReflectValue, bool) Select(Slice<GoReflectSelectCase> cases)
        {
            // Try each case: look for a default case, or try to receive from channels
            long defaultIdx = -1;
            for (int i = 0; i < cases.Len; i++)
            {
                var c = cases[i];
                if (c.Dir == SelectDefault)
                {
                    defaultIdx = i;
                    continue;
                }
                var chanObj = c.Chan.Interface();
                if (c.Dir == SelectRecv && chanObj != null)
                {
                    var chanType = chanObj.GetType();
                    var tryRecvMethod = chanType.GetMethod("TryReceive");
                    if (tryRecvMethod != null)
                    {
                        var result = tryRecvMethod.Invoke(chanObj, null);
                        if (result is ValueTuple<object?, bool> tuple && tuple.Item2)
                        {
                            return (i, ValueOf(tuple.Item1), true);
                        }
                    }
                }
            }
            // If no channel was ready, return default case
            if (defaultIdx >= 0)
            {
                return (defaultIdx, GoReflectValue.InvalidValue, false);
            }
            // No default — block on first recv channel (simplified)
            return (0, GoReflectValue.InvalidValue, false);
        }

        public static GoReflectValue MakeFunc(GoReflectType typ, Func<Slice<GoReflectValue>, Slice<GoReflectValue>> fn)
        {
            return new GoReflectValue(fn, typ);
        }

        public static GoReflectValue MakeChan(GoReflectType typ, long buffer)
        {
            var elemType = typ.ClrType.GetGenericArguments()[0];
            var chanType = typeof(Channel<>).MakeGenericType(elemType);
            var instance = Activator.CreateInstance(chanType, new object[] { (int)buffer });
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

        // reflect.Indirect(v Value) Value
        public static GoReflectValue Indirect(GoReflectValue v)
        {
            if (v.Kind() == GoReflectKinds.Pointer)
                return v.Elem();
            return v;
        }

        // reflect.PointerTo(t Type) Type
        public static GoReflectType PointerTo(GoReflectType t)
        {
            return new GoReflectType(typeof(Ptr<>), "*" + t.Name());
        }

        // reflect.TypeFor[T]() Type — generic function (stub as non-generic)
        public static GoReflectType TypeFor()
        {
            return new GoReflectType(typeof(object), "interface {}");
        }

        // reflect.SliceOf(t Type) Type
        public static GoReflectType SliceOf(GoReflectType t)
        {
            return new GoReflectType(typeof(Slice<>), "[]" + t.Name());
        }

        // reflect.MapOf(key, elem Type) Type
        public static GoReflectType MapOf(GoReflectType key, GoReflectType elem)
        {
            return new GoReflectType(typeof(Map<,>), "map[" + key.Name() + "]" + elem.Name());
        }

        // reflect.FuncOf(in, out []Type, variadic bool) Type
        public static GoReflectType FuncOf(Slice<GoReflectType> inTypes, Slice<GoReflectType> outTypes, bool variadic)
        {
            return new GoReflectType(typeof(Delegate), "func");
        }

        // reflect.ChanOf(dir ChanDir, t Type) Type
        public static GoReflectType ChanOf(long dir, GoReflectType t)
        {
            return new GoReflectType(typeof(Channel<>), "chan " + t.Name());
        }

        // reflect.Copy(dst, src Value) int
        public static long Copy(GoReflectValue dst, GoReflectValue src)
        {
            long dstLen = dst.Len();
            long srcLen = src.Len();
            long count = global::System.Math.Min(dstLen, srcLen);
            for (long i = 0; i < count; i++)
            {
                var elem = src.Index(i);
                dst.Index(i).Set(elem);
            }
            return count;
        }

        // reflect.Swapper(slice interface{}) func(i, j int)
        public static Action<long, long> Swapper(object? slice)
        {
            if (slice == null)
            {
                throw new GoPanicException("reflect: Swapper of nil");
            }

            var sliceType = slice.GetType();
            // Look for indexer and Len property
            var lenProp = sliceType.GetProperty("Len");
            var indexer = sliceType.GetProperty("Item");
            if (lenProp != null && indexer != null)
            {
                return (i, j) =>
                {
                    var vi = indexer.GetValue(slice, new object[] { (int)i });
                    var vj = indexer.GetValue(slice, new object[] { (int)j });
                    indexer.SetValue(slice, vi, new object[] { (int)j });
                    indexer.SetValue(slice, vj, new object[] { (int)i });
                };
            }

            return (i, j) => { };
        }

        // Channel direction constants
        [GoConst] public static readonly long RecvDir = 1;
        [GoConst] public static readonly long SendDir = 2;
        [GoConst] public static readonly long BothDir = 3;

        // SelectDir constants
        [GoConst] public static readonly long SelectSend = 1;
        [GoConst] public static readonly long SelectRecv = 2;
        [GoConst] public static readonly long SelectDefault = 3;

        // Kind constants are already defined above (Invalid, Bool, Int, etc.)
        // Just need [GoConst] on the existing declarations

        // reflect.AppendSlice(s, t Value) Value
        [GoFunc]
        public static GoReflectValue AppendSlice(GoReflectValue s, GoReflectValue t)
        {
            var sVal = s.Interface();
            var tVal = t.Interface();
            if (sVal == null || tVal == null)
            {
                return s;
            }
            // Use Slice<T>.Append(s, t) via reflection
            var sType = sVal.GetType();
            var appendMethod = sType.GetMethod("Append", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, new[] { sType, sType }, null);
            if (appendMethod != null)
            {
                var result = appendMethod.Invoke(null, new[] { sVal, tVal });
                if (result != null)
                {
                    return ValueOf(result);
                }
            }
            return s;
        }

        // reflect.ArrayOf(count int, elem Type) Type
        [GoFunc]
        public static GoReflectType ArrayOf(long count, GoReflectType elem)
        {
            return new GoReflectType(elem.ClrType, "[" + count + "]" + elem.Name());
        }

        // reflect.NewAt(typ Type, p unsafe.Pointer) Value
        [GoFunc]
        public static GoReflectValue NewAt(GoReflectType typ, object? p)
        {
            var instance = Activator.CreateInstance(typ.ClrType);
            return new GoReflectValue(instance, new GoReflectType(typ.ClrType, "*" + typ.Name()), true);
        }

        // reflect.PtrTo(t Type) Type — deprecated alias for PointerTo
        [GoFunc]
        public static GoReflectType PtrTo(GoReflectType t)
        {
            return PointerTo(t);
        }

        // reflect.StructOf(fields []StructField) Type
        [GoFunc]
        public static GoReflectType StructOf(Slice<GoReflectStructField> fields)
        {
            return new GoReflectType(typeof(object), "struct");
        }
    }
}

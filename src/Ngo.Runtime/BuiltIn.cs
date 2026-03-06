// -----------------------------------------------------------------------
// <copyright file="BuiltIn.cs" company="Ziad">
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
using System.Text;

namespace Ngo.Runtime
{
    /// <summary>
    /// Go built-in functions that codegen emits calls to.
    /// </summary>
    public static class BuiltIn
    {
        // --- len ---

        public static int Len<T>(Slice<T> s) => s.Len;
        public static int Len(string s) => GoString.Len(s);
        public static int Len<K, V>(Map<K, V> m) where K : notnull => m.Len;
        public static int Len<T>(Channel<T> ch) => ch.Length;

        // --- cap ---

        public static int Cap<T>(Slice<T> s) => s.Cap;
        public static int Cap<T>(Channel<T> ch) => ch.Capacity;

        // --- make ---

        public static Slice<T> MakeSlice<T>(int length, int capacity = -1)
            => Slice<T>.Make(length, capacity);

        public static Map<K, V> MakeMap<K, V>(int capacity = 0) where K : notnull
            => new Map<K, V>(capacity);

        public static Channel<T> MakeChan<T>(int bufferSize = 0)
            => new Channel<T>(bufferSize);

        // --- append ---

        public static Slice<T> Append<T>(Slice<T> s, params T[] elems)
            => Slice<T>.Append(s, elems);

        public static Slice<T> Append<T>(Slice<T> s, Slice<T> other)
            => Slice<T>.Append(s, other);

        // --- copy ---

        public static int Copy<T>(Slice<T> dst, Slice<T> src)
            => Slice<T>.Copy(dst, src);

        // --- delete ---

        public static void Delete<K, V>(Map<K, V> m, K key) where K : notnull
            => m.Delete(key);

        // --- close ---

        public static void Close<T>(Channel<T> ch)
            => ch.Close();

        // --- new ---

        public static Ptr<T> New<T>() where T : struct
            => new Ptr<T>();

        // --- panic ---

        public static void Panic(object? value)
            => throw new GoPanicException(value);

        // --- recover ---

        public static object? Recover()
            => GoRecover.Recover();

        // --- print / println ---

        public static void Print(params object?[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) Console.Write(" ");
                Console.Write(FormatArg(args[i]));
            }
        }

        public static void Println(params object?[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) Console.Write(" ");
                Console.Write(FormatArg(args[i]));
            }
            Console.WriteLine();
        }

        internal static string FormatArg(object? arg)
        {
            if (arg == null) return "<nil>";
            if (arg is bool b) return b ? "true" : "false";
            return arg.ToString() ?? "";
        }

        public static object? UnwrapInterface(object? value)
        {
            if (value == null) return null;
            var type = value.GetType();
            var field = type.GetField("_value");
            if (field != null)
                return field.GetValue(value);
            return value;
        }
    }
}

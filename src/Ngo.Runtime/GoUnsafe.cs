// -----------------------------------------------------------------------
// <copyright file="GoUnsafe.cs" company="Ziad">
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
using System.Runtime.InteropServices;

namespace Ngo.Runtime
{
    /// <summary>
    /// Go unsafe package — stub implementations for .NET.
    /// </summary>
    public static class GoUnsafe
    {
        public static nuint Sizeof(object? x)
        {
            if (x == null) return 0;
            return (nuint)Marshal.SizeOf(x.GetType());
        }

        public static nuint Offsetof(object? x)
        {
            // Go's unsafe.Offsetof takes a struct field selector;
            // in .NET we can't replicate this exactly, return 0 as stub.
            return 0;
        }

        public static nuint Alignof(object? x)
        {
            if (x == null) return 1;
            var type = x.GetType();
            if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(bool)) return 1;
            if (type == typeof(short) || type == typeof(ushort)) return 2;
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float)) return 4;
            return 8;
        }
    }
}

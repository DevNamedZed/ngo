// -----------------------------------------------------------------------
// <copyright file="GoReflectKinds.cs" company="Ziad">
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

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Reflect
{
    /// <summary>
    /// Go reflect.Kind named type.
    /// </summary>
    [GoType("named", Name = "Kind", Package = "reflect", Underlying = "uint")]
    public class GoReflectKind
    {
        [GoMethod]
        public string String() => GoReflectKinds.KindToString(0);
    }

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
}

// -----------------------------------------------------------------------
// <copyright file="BuiltinTypes.cs" company="Ziad">
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

using System.Collections.Generic;

namespace Ngo.Compiler.Symbols
{
    public static class BuiltinTypes
    {
        public static readonly TypeSymbol Bool = new TypeSymbol("bool", TypeKind.Bool, null);
        public static readonly TypeSymbol Int = new TypeSymbol("int", TypeKind.Int, null);
        public static readonly TypeSymbol Int8 = new TypeSymbol("int8", TypeKind.Int8, null);
        public static readonly TypeSymbol Int16 = new TypeSymbol("int16", TypeKind.Int16, null);
        public static readonly TypeSymbol Int32 = new TypeSymbol("int32", TypeKind.Int32, null);
        public static readonly TypeSymbol Int64 = new TypeSymbol("int64", TypeKind.Int64, null);
        public static readonly TypeSymbol Uint = new TypeSymbol("uint", TypeKind.Uint, null);
        public static readonly TypeSymbol Uint8 = new TypeSymbol("uint8", TypeKind.Uint8, null);
        public static readonly TypeSymbol Uint16 = new TypeSymbol("uint16", TypeKind.Uint16, null);
        public static readonly TypeSymbol Uint32 = new TypeSymbol("uint32", TypeKind.Uint32, null);
        public static readonly TypeSymbol Uint64 = new TypeSymbol("uint64", TypeKind.Uint64, null);
        public static readonly TypeSymbol Uintptr = new TypeSymbol("uintptr", TypeKind.Uintptr, null);
        public static readonly TypeSymbol Float32 = new TypeSymbol("float32", TypeKind.Float32, null);
        public static readonly TypeSymbol Float64 = new TypeSymbol("float64", TypeKind.Float64, null);
        public static readonly TypeSymbol Complex64 = new TypeSymbol("complex64", TypeKind.Complex64, null);
        public static readonly TypeSymbol Complex128 = new TypeSymbol("complex128", TypeKind.Complex128, null);
        public static readonly TypeSymbol String = new TypeSymbol("string", TypeKind.String, null);

        // Aliases
        public static readonly TypeSymbol Byte = new TypeSymbol("byte", TypeKind.Uint8, Uint8);
        public static readonly TypeSymbol Rune = new TypeSymbol("rune", TypeKind.Int32, Int32);

        // Untyped constants
        public static readonly TypeSymbol UntypedBool = new TypeSymbol("untyped bool", TypeKind.UntypedBool, null);
        public static readonly TypeSymbol UntypedInt = new TypeSymbol("untyped int", TypeKind.UntypedInt, null);
        public static readonly TypeSymbol UntypedFloat = new TypeSymbol("untyped float", TypeKind.UntypedFloat, null);
        public static readonly TypeSymbol UntypedComplex = new TypeSymbol("untyped complex", TypeKind.UntypedComplex, null);
        public static readonly TypeSymbol UntypedString = new TypeSymbol("untyped string", TypeKind.UntypedString, null);
        public static readonly TypeSymbol UntypedNil = new TypeSymbol("untyped nil", TypeKind.UntypedNil, null);

        // Void (for functions with no return value)
        public static readonly TypeSymbol Void = new TypeSymbol("void", TypeKind.Void, null);

        // Empty interface: interface{} / any — maps to System.Object
        public static readonly InterfaceTypeSymbol EmptyInterface =
            new InterfaceTypeSymbol("interface{}", System.Array.Empty<MethodSymbol>());

        // Built-in interface: error { Error() string }
        public static readonly InterfaceTypeSymbol Error = CreateErrorInterface();

        private static InterfaceTypeSymbol CreateErrorInterface()
        {
            var iface = new InterfaceTypeSymbol("error", System.Array.Empty<MethodSymbol>());
            var errorMethod = new MethodSymbol(
                "Error", iface, false,
                System.Array.Empty<ParameterSymbol>(),
                String);
            iface.SetMethods(new[] { errorMethod });
            return iface;
        }

        private static readonly Dictionary<string, TypeSymbol> _typesByName = new Dictionary<string, TypeSymbol>
        {
            ["bool"] = Bool,
            ["int"] = Int,
            ["int8"] = Int8,
            ["int16"] = Int16,
            ["int32"] = Int32,
            ["int64"] = Int64,
            ["uint"] = Uint,
            ["uint8"] = Uint8,
            ["uint16"] = Uint16,
            ["uint32"] = Uint32,
            ["uint64"] = Uint64,
            ["uintptr"] = Uintptr,
            ["float32"] = Float32,
            ["float64"] = Float64,
            ["complex64"] = Complex64,
            ["complex128"] = Complex128,
            ["string"] = String,
            ["byte"] = Byte,
            ["rune"] = Rune,
            ["error"] = Error,
            ["interface{}"] = EmptyInterface,
            ["any"] = EmptyInterface,
        };

        public static TypeSymbol? Resolve(string name)
        {
            return _typesByName.TryGetValue(name, out var type) ? type : null;
        }
    }
}

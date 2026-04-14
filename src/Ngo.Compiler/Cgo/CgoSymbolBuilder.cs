// -----------------------------------------------------------------------
// <copyright file="CgoSymbolBuilder.cs" company="Ziad">
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
using Ngo.Compiler.Semantics;
using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Builds the Go-visible <c>C</c> pseudo-package from a
    /// <see cref="CgoSymbolCatalog"/> (produced by an
    /// <see cref="ICgoSymbolSource"/> reading DWARF or PDB) and the
    /// numeric <see cref="CgoProbeResult"/> returned by the executable
    /// probe. The catalog provides the authoritative view of every C
    /// function, struct, union, and enum the Go code references; the
    /// probe result covers platform-specific primitive sizes that the
    /// <c>C.sizeof_*</c> constants need.
    /// </summary>
    public class CgoSymbolBuilder
    {
        private readonly CgoSymbolCatalog _catalog;
        private readonly CgoProbeResult _probeResult;
        private readonly MarshallingStubGenerator _marshaller;
        private PackageSymbol? _activePackage;
        private TypeSymbol? _unsafePointerType;

        public CgoSymbolBuilder(CgoSymbolCatalog catalog, CgoProbeResult probeResult)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            if (probeResult == null)
            {
                throw new ArgumentNullException(nameof(probeResult));
            }
            _catalog = catalog;
            _probeResult = probeResult;
            _marshaller = new MarshallingStubGenerator(probeResult);
        }

        /// <summary>
        /// Build the C pseudo-package with all exported symbols.
        /// Type exports run before <see cref="AddFunctions"/> so the
        /// function-parameter mapping in <see cref="MapCToGoType"/> can
        /// reuse the canonical Go-visible <see cref="TypeSymbol"/> for
        /// each user type. Without that ordering the parameter side
        /// would build a fresh ad-hoc TypeSymbol while the resolver
        /// resolves the Go-side <c>C.&lt;type&gt;</c> reference to the
        /// exported one — two distinct instances that fail structural
        /// assignability for typedef-pointer aliases like
        /// <c>CK_ATTRIBUTE_PTR</c>.
        /// </summary>
        public PackageSymbol BuildCPackage(string libraryName)
        {
            if (libraryName == null)
            {
                throw new ArgumentNullException(nameof(libraryName));
            }

            PackageSymbol package = new("C", "C");
            _activePackage = package;
            try
            {
                AddPrimitiveTypeAliases(package);
                AddStructTypes(package);
                AddOpaqueHandleTypes(package);
                AddTypedefTypeAliases(package);
                AddTagNamespaceAliases(package);
                AddFunctions(package);
                AddHelperFunctions(package);
                AddSizeofConstants(package);
                AddEnumConstants(package);
            }
            finally
            {
                _activePackage = null;
            }

            return package;
        }

        private void AddPrimitiveTypeAliases(PackageSymbol package)
        {
            Dictionary<string, TypeKind> primitives = new()
            {
                { "char", TypeKind.Int8 },
                { "schar", TypeKind.Int8 },
                { "uchar", TypeKind.Uint8 },
                { "short", TypeKind.Int16 },
                { "ushort", TypeKind.Uint16 },
                { "int", TypeKind.Int32 },
                { "uint", TypeKind.Uint32 },
                { "long", GetGoLongTypeKind() },
                { "ulong", GetGoULongTypeKind() },
                { "longlong", TypeKind.Int64 },
                { "ulonglong", TypeKind.Uint64 },
                { "float", TypeKind.Float32 },
                { "double", TypeKind.Float64 },
                { "size_t", TypeKind.Uintptr },
            };

            foreach (KeyValuePair<string, TypeKind> primitive in primitives)
            {
                TypeSymbol typeSymbol = new(primitive.Key, primitive.Value, null);
                package.AddExport(typeSymbol);
            }
        }

        private void AddFunctions(PackageSymbol package)
        {
            foreach (CgoFunctionInfo functionInfo in _catalog.Functions.Values)
            {
                FunctionSymbol functionSymbol = BuildFunctionSymbol(functionInfo);
                package.AddExport(functionSymbol);
            }
        }

        private void AddStructTypes(PackageSymbol package)
        {
            foreach (CgoStructInfo structInfo in _catalog.StructsAndUnions.Values)
            {
                TypeSymbol typeSymbol = BuildStructTypeSymbol(structInfo);
                package.AddExport(typeSymbol);
            }
        }

        /// <summary>
        /// Export every opaque-handle type the catalog carries (e.g.
        /// <c>ZSTD_CCtx</c>, <c>sqlite3</c>, <c>CK_SESSION_HANDLE</c>)
        /// as a <see cref="TypeSymbol"/> of <see cref="TypeKind.Uintptr"/>.
        /// Opaque handles are pointer-sized in C and the Go side only
        /// ever holds pointers to them, so a uintptr-shaped symbol is
        /// the right surface form for the resolver to bind against
        /// when it sees <c>*C.ZSTD_CCtx</c> or
        /// <c>C.ZSTD_freeCCtx(ctx)</c>. Skipped when an export of the
        /// same name already exists (e.g. a populated typedef alias
        /// took precedence) so a less-informative opaque shape never
        /// overwrites a real layout.
        /// </summary>
        private void AddOpaqueHandleTypes(PackageSymbol package)
        {
            foreach (CgoOpaqueTypeInfo opaque in _catalog.OpaqueTypes.Values)
            {
                if (package.LookupExport(opaque.Name) != null)
                {
                    continue;
                }
                TypeSymbol opaqueSymbol = new(opaque.Name, TypeKind.Uintptr, null);
                package.AddExport(opaqueSymbol);
            }
        }

        /// <summary>
        /// Export each catalog typedef whose alias target is neither a
        /// struct/union (already exported through
        /// <see cref="AddStructTypes"/>) nor an opaque handle (already
        /// exported through <see cref="AddOpaqueHandleTypes"/>) as a
        /// <see cref="TypeSymbol"/> of the alias target's
        /// <see cref="TypeKind"/>. This covers typedefs over primitive
        /// integers (<c>typedef int my_int;</c>), enum types, function
        /// pointers represented as opaque-int aliases, and any other
        /// non-struct C type. The lookup-then-skip guard preserves the
        /// richer struct or opaque shape registered earlier in the
        /// build pipeline.
        /// </summary>
        private void AddTypedefTypeAliases(PackageSymbol package)
        {
            foreach (CgoTypedefInfo typedef in _catalog.Typedefs.Values)
            {
                if (package.LookupExport(typedef.Name) != null)
                {
                    continue;
                }
                TypeSymbol mapped = MapCToGoType(typedef.AliasCType);
                // Mark the alias so the type checker unwraps it to the
                // mapped type. Without UnderlyingType + IsAlias the
                // typedef would lose any structural shape (e.g. a
                // PointerTypeSymbol from 'CK_ATTRIBUTE *') and degrade
                // to a flat scalar with the typedef's TypeKind, breaking
                // assignability against '*C.CK_ATTRIBUTE' arguments.
                TypeSymbol typedefSymbol = new(typedef.Name, mapped.TypeKind, mapped)
                {
                    IsAlias = true,
                };
                package.AddExport(typedefSymbol);
            }
        }

        /// <summary>
        /// Export every struct, union, and named enum a second time
        /// under its Go cgo tag-namespace alias —
        /// <c>struct_&lt;tag&gt;</c>, <c>union_&lt;tag&gt;</c>, or
        /// <c>enum_&lt;tag&gt;</c>. Go syntax cannot spell the
        /// C tag namespaces directly, so cgo reserves these prefixes
        /// per the language spec; without the alias entries
        /// <c>C.struct_ZSTD_CDict_s</c> and <c>C.enum_FooBar</c> would
        /// fail to resolve even when the underlying type is in the
        /// catalog. Aliases share the same field layout (or
        /// underlying integer kind for enums) as the primary export,
        /// so callers see the same shape regardless of which spelling
        /// they used. Anonymous enums whose synthetic catalog key
        /// begins with <c>__anonymous_enum_at_</c> are skipped because
        /// they are unreachable through the C tag namespace.
        /// </summary>
        private void AddTagNamespaceAliases(PackageSymbol package)
        {
            foreach (CgoStructInfo structInfo in _catalog.StructsAndUnions.Values)
            {
                string aliasPrefix = structInfo.IsUnion ? "union_" : "struct_";
                string aliasName = aliasPrefix + structInfo.GoName;
                if (package.LookupExport(aliasName) != null)
                {
                    continue;
                }
                TypeSymbol aliasSymbol = BuildTagAliasSymbol(package, structInfo, aliasName);
                package.AddExport(aliasSymbol);
            }

            foreach (CgoEnumInfo enumInfo in _catalog.Enums.Values)
            {
                if (enumInfo.Name.StartsWith(AnonymousEnumKeyPrefix, StringComparison.Ordinal))
                {
                    continue;
                }
                string aliasName = "enum_" + enumInfo.Name;
                if (package.LookupExport(aliasName) != null)
                {
                    continue;
                }
                TypeSymbol enumAlias = BuildEnumTagAliasSymbol(package, enumInfo.Name, aliasName);
                package.AddExport(enumAlias);
            }
        }

        /// <summary>
        /// Build the <c>struct_&lt;tag&gt;</c> / <c>union_&lt;tag&gt;</c>
        /// alias as an <c>IsAlias</c> <see cref="TypeSymbol"/> wrapping
        /// the existing bare-tag export when one is present in
        /// <paramref name="package"/>. Anchoring the alias on the
        /// already-exported instance is what lets the type checker
        /// unwrap an argument typed <c>C.struct_X</c> back to
        /// <c>C.X</c> through <see cref="TypeSymbol.UnderlyingType"/>
        /// and recognise the two as the same struct. The legacy fresh
        /// <see cref="StructTypeSymbol"/> (kept as a fallback for cases
        /// where no bare-tag export exists, e.g. a struct that lives
        /// only under its tag) produced two distinct StructTypeSymbol
        /// instances with identical fields, which fail
        /// <c>IsAssignable</c> by reference identity even though the
        /// names line up.
        /// </summary>
        private TypeSymbol BuildTagAliasSymbol(
            PackageSymbol package, CgoStructInfo structInfo, string aliasName)
        {
            Symbol? existing = package.LookupExport(structInfo.GoName);
            if (existing is TypeSymbol existingType)
            {
                return new TypeSymbol(aliasName, existingType.TypeKind, existingType)
                {
                    IsAlias = true,
                };
            }
            return BuildTagAliasStructSymbol(structInfo, aliasName);
        }

        /// <summary>
        /// Build the <c>enum_&lt;tag&gt;</c> alias as an
        /// <c>IsAlias</c> <see cref="TypeSymbol"/> wrapping the
        /// already-exported bare-name enum type when one exists (a
        /// typedef <c>typedef enum X X;</c> would surface as such).
        /// Falls back to a freestanding <see cref="TypeKind.Int32"/>
        /// symbol when no bare-name export is present, which matches
        /// the historical behaviour for plain
        /// <c>enum X { ... };</c> declarations whose Go-visible form
        /// is only the tag-prefixed spelling.
        /// </summary>
        private static TypeSymbol BuildEnumTagAliasSymbol(
            PackageSymbol package, string enumName, string aliasName)
        {
            Symbol? existing = package.LookupExport(enumName);
            if (existing is TypeSymbol existingType)
            {
                return new TypeSymbol(aliasName, existingType.TypeKind, existingType)
                {
                    IsAlias = true,
                };
            }
            return new TypeSymbol(aliasName, TypeKind.Int32, null);
        }

        private TypeSymbol BuildTagAliasStructSymbol(CgoStructInfo structInfo, string aliasName)
        {
            return new StructTypeSymbol(aliasName, BuildFieldSymbols(structInfo));
        }

        /// <summary>
        /// Catalog-key prefix used by
        /// <c>CgoDwarfSymbolSource.SyntheticAnonymousEnumName</c> for
        /// enums that lack a C tag. Mirrored here so the tag-alias
        /// pass can recognise and skip them — they have no
        /// user-visible <c>enum_&lt;tag&gt;</c> form.
        /// </summary>
        private const string AnonymousEnumKeyPrefix = "__anonymous_enum_at_";

        private FunctionSymbol BuildFunctionSymbol(CgoFunctionInfo function)
        {
            List<ParameterSymbol> parameters = new(function.Parameters.Count);
            for (int parameterIndex = 0; parameterIndex < function.Parameters.Count; parameterIndex++)
            {
                CgoParameterInfo parameter = function.Parameters[parameterIndex];
                TypeSymbol parameterType = MapCToGoType(parameter.CType);
                parameters.Add(new ParameterSymbol(parameter.Name, parameterType, parameterIndex));
            }

            IReadOnlyList<TypeSymbol> returnTypes;
            if (IsVoidCType(function.ReturnType))
            {
                returnTypes = Array.Empty<TypeSymbol>();
            }
            else
            {
                returnTypes = new TypeSymbol[] { MapCToGoType(function.ReturnType) };
            }

            return new FunctionSymbol(
                function.Name, parameters, returnTypes, function.IsVariadic, packageName: null);
        }

        private static bool IsVoidCType(string cType)
        {
            if (cType == null)
            {
                return true;
            }
            return cType.Trim() == "void";
        }

        private TypeSymbol BuildStructTypeSymbol(CgoStructInfo structInfo)
        {
            return new StructTypeSymbol(structInfo.GoName, BuildFieldSymbols(structInfo));
        }

        /// <summary>
        /// Materialise a <see cref="CgoStructInfo"/>'s field list into
        /// the <see cref="FieldSymbol"/> list a
        /// <see cref="StructTypeSymbol"/> exposes. Centralised so that
        /// every Go-visible cgo struct (the bare struct exported by
        /// <see cref="AddStructTypes"/>, the typedef-aliased struct
        /// rebuilt as a tag-namespace fallback, and any future variant)
        /// applies the same Go-keyword escape on field names. Without
        /// the escape a C field named <c>type</c>, <c>func</c>,
        /// <c>chan</c>, etc. would be unspellable in Go source —
        /// matching real cgo's behaviour of prefixing <c>_</c> in front
        /// of any field whose C name collides with a Go reserved
        /// keyword (e.g. <c>CK_ATTRIBUTE.type</c> surfaces as
        /// <c>_type</c> on the Go side).
        /// </summary>
        private List<FieldSymbol> BuildFieldSymbols(CgoStructInfo structInfo)
        {
            List<FieldSymbol> fields = new(structInfo.Fields.Count);
            for (int fieldIndex = 0; fieldIndex < structInfo.Fields.Count; fieldIndex++)
            {
                CgoFieldInfo field = structInfo.Fields[fieldIndex];
                TypeSymbol fieldType = MapCToGoType(field.CType);
                string goFieldName = EscapeGoKeyword(field.Name);
                fields.Add(new FieldSymbol(goFieldName, fieldType, fieldIndex));
            }
            return fields;
        }

        /// <summary>
        /// Go-side field name for a C struct field. Matches Go cgo's
        /// behaviour: when the C field name is one of Go's 25 reserved
        /// keywords, prefix it with an underscore so the field is
        /// spellable in Go source. The 25 keywords come from the Go
        /// language spec and are mirrored here rather than reaching
        /// across into the lexer to keep <c>Cgo</c> independent of
        /// <c>Language</c>.
        /// </summary>
        private static string EscapeGoKeyword(string fieldName)
        {
            switch (fieldName)
            {
                case "break":
                case "case":
                case "chan":
                case "const":
                case "continue":
                case "default":
                case "defer":
                case "else":
                case "fallthrough":
                case "for":
                case "func":
                case "go":
                case "goto":
                case "if":
                case "import":
                case "interface":
                case "map":
                case "package":
                case "range":
                case "return":
                case "select":
                case "struct":
                case "switch":
                case "type":
                case "var":
                    return "_" + fieldName;
                default:
                    return fieldName;
            }
        }

        private void AddHelperFunctions(PackageSymbol package)
        {
            TypeSymbol stringType = new("string", TypeKind.String, null);
            TypeSymbol ptrType = new("unsafe.Pointer", TypeKind.Uintptr, null);
            TypeSymbol intType = new("int32", TypeKind.Int32, null);
            // Construct as a real SliceTypeSymbol over byte rather than a
            // flat TypeSymbol so the type checker's slice-element queries
            // (e.g. GetSliceElementType used by '[]byte' ↔ string
            // conversions and by len/cap calls) see the underlying
            // element. A TypeSymbol("[]byte", Slice, null) has the right
            // name and TypeKind but carries no element type, so
            // 'string(C.GoBytes(...))' degrades to "Cannot convert
            // '[]byte' to 'string'" even though both sides are correct.
            TypeSymbol byteSliceType = new SliceTypeSymbol(BuiltinTypes.Byte);
            TypeSymbol voidType = new("void", TypeKind.Void, null);

            package.AddExport(new FunctionSymbol(
                "CString",
                new List<ParameterSymbol> { new("s", stringType, 0) },
                ptrType));

            package.AddExport(new FunctionSymbol(
                "GoString",
                new List<ParameterSymbol> { new("p", ptrType, 0) },
                stringType));

            package.AddExport(new FunctionSymbol(
                "GoStringN",
                new List<ParameterSymbol>
                {
                    new("p", ptrType, 0),
                    new("n", intType, 1),
                },
                stringType));

            package.AddExport(new FunctionSymbol(
                "GoBytes",
                new List<ParameterSymbol>
                {
                    new("p", ptrType, 0),
                    new("n", intType, 1),
                },
                byteSliceType));

            package.AddExport(new FunctionSymbol(
                "CBytes",
                new List<ParameterSymbol> { new("b", byteSliceType, 0) },
                ptrType));

            package.AddExport(new FunctionSymbol(
                "free",
                new List<ParameterSymbol> { new("p", ptrType, 0) },
                voidType));
        }

        private TypeSymbol MapCToGoType(string cType)
        {
            string trimmed = (cType ?? string.Empty).Trim();

            // Cgo's Go surface uses underscore-prefixed tag spellings
            // ('struct_ctx', 'union_foo', 'enum_bar'); the C-source form
            // 'struct ctx' would never match a Go-side reference like
            // '*C.struct_ctx'. Normalising here keeps both sides on the
            // same identifier before any further mapping runs.
            string canonical = NormalizeTagSpelling(trimmed);

            // C 'T *' must surface as a Go-side PointerTypeSymbol so an
            // argument written as '*C.T' (which the resolver builds as
            // PointerTypeSymbol over the exported C type) type-checks
            // against the parameter. A flat Uintptr-named symbol breaks
            // assignability because the type checker compares pointer
            // shapes, not C-source spellings — '*compressStream2_result'
            // and 'compressStream2_result *' would otherwise differ.
            // 'void *' is the cgo special case: Go has no 'void' type, so
            // C void pointers surface as unsafe.Pointer, which the type
            // checker recognises as freely convertible to and from any
            // other pointer type. Without this, a typedef like
            // 'CK_VOID_PTR' (alias of void *) would carry a
            // PointerTypeSymbol over a void TypeSymbol and the conversion
            // 'CK_BYTE_PTR(p)' on a CK_VOID_PTR-typed argument would fail
            // because no PointerTypeSymbol-to-PointerTypeSymbol path
            // exists between unrelated element types.
            if (canonical.EndsWith("*"))
            {
                string innerSpelling = canonical.Substring(0, canonical.Length - 1).Trim();
                if (IsVoidSpelling(innerSpelling))
                {
                    return GetUnsafePointerType();
                }
                if (IsFunctionSignatureSpelling(innerSpelling))
                {
                    return new CFunctionPointerTypeSymbol(canonical);
                }
                TypeSymbol innerSymbol = MapCToGoType(innerSpelling);
                return new PointerTypeSymbol(innerSymbol);
            }

            // C 'T[N]' → Go '[N]T' so the type checker permits indexing
            // and the runtime carries the correct element count. In C
            // multi-dim arrays the LEFTMOST dimension is the outermost
            // (e.g. 'int[3][5]' is "array of 3 arrays of 5 int"), so we
            // peel from the left here and recurse on the remainder. A
            // flat scalar TypeSymbol from the marshaller fallback would
            // produce "Cannot index type 'CK_UTF8CHAR[32]'" because the
            // type checker has no way to recover the element shape from
            // a name.
            if (TryParseLeadingArrayDimension(
                    canonical, out int arrayLength, out string? arrayElementSpelling))
            {
                TypeSymbol elementType = MapCToGoType(arrayElementSpelling!);
                return new ArrayTypeSymbol(elementType, arrayLength);
            }

            // Reuse a previously exported user type so the Go-side
            // C.<type> reference and the C-side function-parameter
            // mapping converge on the same TypeSymbol instance.
            // Otherwise a typedef-pointer alias like C.CK_ATTRIBUTE_PTR
            // would be one TypeSymbol on the resolver side and a
            // separately constructed one on the parameter side, and the
            // type checker would reject the call even though both names
            // and structures match.
            if (_activePackage != null)
            {
                Symbol? existing = _activePackage.LookupExport(canonical);
                if (existing is TypeSymbol existingType)
                {
                    return existingType;
                }

                // A field whose DWARF type DIE points directly at a
                // structure (rather than at the typedef wrapping it)
                // arrives here as 'struct X' / 'union X' / 'enum X' and
                // normalises to 'struct_X'. The tag-namespace alias for
                // 'struct_X' is exported by AddTagNamespaceAliases —
                // which runs AFTER the struct fields are mapped, so the
                // alias is not yet present at this point. Fall back to
                // the bare-tag export ('X') so the field reuses the
                // canonical StructTypeSymbol everyone else binds to.
                // Without this an inner struct field built during
                // AddStructTypes would carry a fresh ad-hoc TypeSymbol
                // and fail assignability against the canonical struct
                // when the user pulls the field out and passes it to a
                // function expecting C.X.
                string? bareTag = TryStripTagPrefix(canonical);
                if (bareTag != null)
                {
                    Symbol? bareExisting = _activePackage.LookupExport(bareTag);
                    if (bareExisting is TypeSymbol bareType)
                    {
                        return bareType;
                    }
                }
            }

            NetTypeMapping mapping = _marshaller.MapCTypeToNet(canonical);
            TypeKind kind = mapping.CSharpType switch
            {
                "void" => TypeKind.Void,
                "sbyte" => TypeKind.Int8,
                "byte" => TypeKind.Uint8,
                "short" => TypeKind.Int16,
                "ushort" => TypeKind.Uint16,
                "int" => TypeKind.Int32,
                "uint" => TypeKind.Uint32,
                "long" => TypeKind.Int64,
                "ulong" => TypeKind.Uint64,
                "nint" => TypeKind.Uintptr,
                "nuint" => TypeKind.Uintptr,
                "float" => TypeKind.Float32,
                "double" => TypeKind.Float64,
                _ => TypeKind.Uintptr,
            };
            return new TypeSymbol(canonical, kind, null);
        }

        private static string NormalizeTagSpelling(string cType)
        {
            const string structPrefix = "struct ";
            const string unionPrefix = "union ";
            const string enumPrefix = "enum ";
            if (cType.StartsWith(structPrefix, StringComparison.Ordinal))
            {
                return "struct_" + cType.Substring(structPrefix.Length);
            }
            if (cType.StartsWith(unionPrefix, StringComparison.Ordinal))
            {
                return "union_" + cType.Substring(unionPrefix.Length);
            }
            if (cType.StartsWith(enumPrefix, StringComparison.Ordinal))
            {
                return "enum_" + cType.Substring(enumPrefix.Length);
            }
            return cType;
        }

        /// <summary>
        /// Try to interpret <paramref name="canonical"/> as a C array
        /// type. The C-source form is
        /// <c>BASE[OUTER][NEXT]...</c>; this method peels the leftmost
        /// (outermost) <c>[N]</c> off and returns the element spelling
        /// (the base name plus any remaining inner dimensions) along
        /// with the outermost length. Callers recurse on
        /// <paramref name="elementSpelling"/> to handle multi-dim
        /// arrays. Returns <c>false</c> when <paramref name="canonical"/>
        /// is not an array (no <c>[N]</c>) or the dimension isn't a
        /// non-negative decimal integer (e.g. an incomplete <c>T[]</c>
        /// emitted by <see cref="DwarfCTypeFormatter.FormatArray"/>
        /// when the array DIE lacked subrange info — those are passed
        /// through to the marshaller fallback so the original spelling
        /// is preserved in diagnostics).
        /// </summary>
        private static bool TryParseLeadingArrayDimension(
            string canonical, out int length, out string? elementSpelling)
        {
            length = 0;
            elementSpelling = null;
            if (!canonical.EndsWith("]", StringComparison.Ordinal))
            {
                return false;
            }
            int firstOpen = canonical.IndexOf('[');
            if (firstOpen <= 0)
            {
                return false;
            }
            int firstClose = canonical.IndexOf(']', firstOpen + 1);
            if (firstClose <= firstOpen + 1)
            {
                return false;
            }
            string lengthText = canonical.Substring(
                firstOpen + 1, firstClose - firstOpen - 1).Trim();
            if (!int.TryParse(
                    lengthText,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int parsedLength)
                || parsedLength < 0)
            {
                return false;
            }
            string baseName = canonical.Substring(0, firstOpen).TrimEnd();
            string innerDimensions = canonical.Substring(firstClose + 1);
            length = parsedLength;
            elementSpelling = baseName + innerDimensions;
            return true;
        }

        /// <summary>
        /// Strip the cgo tag-namespace prefix (<c>struct_</c>,
        /// <c>union_</c>, or <c>enum_</c>) from <paramref name="canonical"/>
        /// and return the bare tag, or <c>null</c> if no prefix matched.
        /// Used by <see cref="MapCToGoType"/> to fall back to the bare
        /// export when the prefixed alias is not yet present in the
        /// active package.
        /// </summary>
        private static string? TryStripTagPrefix(string canonical)
        {
            const string structPrefix = "struct_";
            const string unionPrefix = "union_";
            const string enumPrefix = "enum_";
            if (canonical.StartsWith(structPrefix, StringComparison.Ordinal))
            {
                return canonical.Substring(structPrefix.Length);
            }
            if (canonical.StartsWith(unionPrefix, StringComparison.Ordinal))
            {
                return canonical.Substring(unionPrefix.Length);
            }
            if (canonical.StartsWith(enumPrefix, StringComparison.Ordinal))
            {
                return canonical.Substring(enumPrefix.Length);
            }
            return null;
        }

        /// <summary>
        /// Detect a C 'void' spelling, including the cv-qualified forms
        /// (<c>const void</c>, <c>volatile void</c>) that DWARF formatters
        /// can emit on the inner type of a pointer DIE. Used by
        /// <see cref="MapCToGoType"/> to decide when 'T *' should surface
        /// as <c>unsafe.Pointer</c> rather than as a
        /// <see cref="PointerTypeSymbol"/> over a void TypeSymbol.
        /// </summary>
        private static bool IsVoidSpelling(string spelling)
        {
            string trimmed = spelling.Trim();
            if (trimmed == "void")
            {
                return true;
            }
            if (trimmed == "const void"
                || trimmed == "volatile void"
                || trimmed == "const volatile void"
                || trimmed == "volatile const void")
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Detect a C subroutine-type spelling of the form
        /// <c>returnType (paramType1, paramType2, ...)</c> that
        /// <see cref="Symbols.DwarfCTypeFormatter"/> emits for a
        /// <c>DW_TAG_subroutine_type</c>. C type strings never contain a
        /// parenthesis in any other form — struct/union/enum tag names, array
        /// dimensions, and cv-qualifiers all use bare identifiers or
        /// brackets — so the presence of '(' is sufficient to distinguish a
        /// function signature from a typedef or primitive name. Used by
        /// <see cref="MapCToGoType"/> to route pointer-to-subroutine
        /// inputs to <see cref="CFunctionPointerTypeSymbol"/> instead of
        /// folding the signature into a fallback uintptr scalar.
        /// </summary>
        private static bool IsFunctionSignatureSpelling(string spelling)
        {
            return spelling.IndexOf('(') >= 0;
        }

        /// <summary>
        /// Resolve the canonical <c>unsafe.Pointer</c> TypeSymbol so the
        /// type checker's structural <c>IsUnsafePointer</c> probe (a
        /// <see cref="StructTypeSymbol"/> named "Pointer" or
        /// "UnsafePointer" with zero fields) recognises C void pointers
        /// and lets the existing unsafe.Pointer ↔ pointer conversion path
        /// fire. Prefers the instance the runtime resolver already
        /// exports — sharing it preserves identity for any downstream
        /// check that compares by reference. Falls back to constructing a
        /// fresh stand-in (still passes the structural probe) when the
        /// runtime package is unreachable, e.g. in unit tests that build
        /// the cgo symbol layer in isolation.
        /// </summary>
        private TypeSymbol GetUnsafePointerType()
        {
            if (_unsafePointerType != null)
            {
                return _unsafePointerType;
            }
            PackageSymbol? unsafePackage = RuntimePackageResolver.Instance.Resolve("unsafe");
            if (unsafePackage != null
                && unsafePackage.LookupExport("Pointer") is TypeSymbol exported)
            {
                _unsafePointerType = exported;
                return _unsafePointerType;
            }
            _unsafePointerType = new StructTypeSymbol("Pointer", new List<FieldSymbol>());
            return _unsafePointerType;
        }

        private TypeKind GetGoLongTypeKind()
        {
            long size = _probeResult.GetTypeSize("long");
            if (size == 8)
            {
                return TypeKind.Int64;
            }
            return TypeKind.Int32;
        }

        private TypeKind GetGoULongTypeKind()
        {
            long size = _probeResult.GetTypeSize("unsigned_long");
            if (size == 8)
            {
                return TypeKind.Uint64;
            }
            return TypeKind.Uint32;
        }

        private void AddSizeofConstants(PackageSymbol package)
        {
            TypeSymbol uintptrType = new("uintptr", TypeKind.Uintptr, null);

            Dictionary<string, string> standardTypes = new()
            {
                { "sizeof_char", "char" },
                { "sizeof_short", "short" },
                { "sizeof_int", "int" },
                { "sizeof_long", "long" },
                { "sizeof_longlong", "long_long" },
                { "sizeof_float", "float" },
                { "sizeof_double", "double" },
                { "sizeof_void_ptr", "void_ptr" },
            };

            foreach (KeyValuePair<string, string> pair in standardTypes)
            {
                long size = _probeResult.GetTypeSize(pair.Value);
                if (size < 0)
                {
                    size = pair.Value switch
                    {
                        "char" => 1,
                        "short" => 2,
                        "int" => 4,
                        "long" => IntPtr.Size,
                        "long_long" => 8,
                        "float" => 4,
                        "double" => 8,
                        "void_ptr" => IntPtr.Size,
                        _ => IntPtr.Size,
                    };
                }
                package.AddExport(new ConstantSymbol(pair.Key, uintptrType, size));
            }

            foreach (KeyValuePair<string, long> probedSize in _probeResult.TypeSizes)
            {
                string constantName = "sizeof_" + probedSize.Key;
                if (package.LookupExport(constantName) == null)
                {
                    package.AddExport(new ConstantSymbol(constantName, uintptrType, probedSize.Value));
                }
            }

            foreach (CgoStructInfo structInfo in _catalog.StructsAndUnions.Values)
            {
                string constantName = "sizeof_" + structInfo.GoName;
                if (package.LookupExport(constantName) == null)
                {
                    package.AddExport(new ConstantSymbol(constantName, uintptrType, structInfo.SizeBytes));
                }
            }
        }

        private void AddEnumConstants(PackageSymbol package)
        {
            TypeSymbol intType = new("int", TypeKind.Int32, null);

            foreach (CgoEnumInfo enumInfo in _catalog.Enums.Values)
            {
                foreach (CgoEnumValue enumerator in enumInfo.Values)
                {
                    if (package.LookupExport(enumerator.Name) == null)
                    {
                        package.AddExport(new ConstantSymbol(enumerator.Name, intType, enumerator.Value));
                    }
                }
            }

            foreach (CgoMacroConstantInfo macro in _catalog.MacroConstants.Values)
            {
                if (package.LookupExport(macro.Name) == null)
                {
                    package.AddExport(new ConstantSymbol(macro.Name, intType, macro.Value));
                }
            }
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="CgoDwarfSymbolSource.cs" company="Ziad">
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
using System.IO;
using Ngo.Compiler.Cgo.Binary;
using Ngo.Compiler.Cgo.Dwarf;
using Ngo.Compiler.Cgo.ObjectFile;

namespace Ngo.Compiler.Cgo.Symbols
{
    /// <summary>
    /// DWARF-backed <see cref="ICgoSymbolSource"/>: takes an anchor
    /// probe build result (gcc / clang compiled with <c>-g</c>), reads
    /// the object file's debug sections, parses them with
    /// <see cref="DwarfReader"/>, and hands the resulting DIE tree to
    /// <see cref="DwarfTypeResolver"/> and
    /// <see cref="DwarfCTypeFormatter"/> to populate a
    /// <see cref="CgoSymbolCatalog"/> with every user-requested C
    /// symbol.
    ///
    /// The reader walks the top-level DIEs of every compilation unit
    /// in the object file and classifies each named type, function, or
    /// constant variable per the mapping documented in
    /// <c>spec/CGO-DWARF-READER.md</c>. Anchor variables emitted by
    /// <see cref="CgoProbeGenerator.GenerateAnchorProbe"/> — named
    /// <c>__ngo_anchor_&lt;go_name&gt;</c> — are skipped by the
    /// top-level classifier so they never appear as C symbols, but a
    /// second pass revisits them: when an anchor variable's pointer
    /// type resolves to a <see cref="DwarfTag.SubroutineType"/>, the
    /// reader reconstructs a <see cref="CgoFunctionInfo"/> using the
    /// Go-side name embedded in the variable. This is how library
    /// functions such as <c>malloc</c> — which never emit a
    /// <see cref="DwarfTag.Subprogram"/> into the probe's own
    /// translation unit — still surface in the catalog.
    ///
    /// Structural failures throw <see cref="CgoDebugInfoException"/>.
    /// Lower-layer exceptions (<see cref="DwarfParseException"/>,
    /// <see cref="BinaryReadException"/>,
    /// <see cref="ObjectFileException"/>) are wrapped with the object
    /// file path so a build driver catching the one semantic-layer
    /// type still gets the full diagnostic chain through
    /// <see cref="Exception.InnerException"/>.
    /// </summary>
    public sealed class CgoDwarfSymbolSource : ICgoSymbolSource
    {
        private const string AnchorVariableNamePrefix = "__ngo_anchor_";

        private const string DebugInfoSectionName = ".debug_info";
        private const string DebugAbbrevSectionName = ".debug_abbrev";
        private const string DebugStrSectionName = ".debug_str";
        private const string DebugLineStrSectionName = ".debug_line_str";

        public CgoSymbolCatalog Extract(CgoAnchorProbeBuildResult probeResult)
        {
            if (probeResult == null)
            {
                throw new ArgumentNullException(nameof(probeResult));
            }
            if (probeResult.Compiler.Kind == CCompilerKind.MSVC)
            {
                throw new CgoDebugInfoException(
                    "CgoDwarfSymbolSource only supports DWARF debug info (gcc/clang). " +
                    "The anchor probe was built by " + probeResult.Compiler.Kind +
                    "; route MSVC builds through a PDB symbol source.");
            }
            if (string.IsNullOrEmpty(probeResult.ObjectFilePath))
            {
                throw new CgoDebugInfoException(
                    "CgoAnchorProbeBuildResult has no object file path; " +
                    "the anchor probe did not produce an artifact to read.");
            }
            if (!File.Exists(probeResult.ObjectFilePath))
            {
                throw new CgoDebugInfoException(
                    "Anchor probe object file does not exist at \"" +
                    probeResult.ObjectFilePath + "\".");
            }

            DwarfSections sections = ReadDwarfSections(probeResult.ObjectFilePath);
            DwarfDebugInfo debugInfo;
            try
            {
                debugInfo = DwarfReader.Read(sections);
            }
            catch (DwarfParseException dwarfException)
            {
                throw new CgoDebugInfoException(
                    "Failed to parse DWARF from \"" + probeResult.ObjectFilePath + "\": " +
                    dwarfException.Message,
                    dwarfException);
            }
            catch (BinaryReadException binaryException)
            {
                throw new CgoDebugInfoException(
                    "Binary read failure while parsing DWARF from \"" +
                    probeResult.ObjectFilePath + "\": " + binaryException.Message,
                    binaryException);
            }

            CgoSymbolCatalog catalog = new();
            foreach (DwarfCompilationUnit compilationUnit in debugInfo.CompilationUnits)
            {
                PopulateFromCompilationUnit(compilationUnit, catalog);
            }
            return catalog;
        }

        private static DwarfSections ReadDwarfSections(string objectFilePath)
        {
            IObjectFileReader reader;
            ObjectFileContents contents;
            try
            {
                reader = ObjectFileReaderFactory.Open(objectFilePath);
                contents = reader.Read(objectFilePath);
            }
            catch (ObjectFileException objectFileException)
            {
                throw new CgoDebugInfoException(
                    "Failed to open anchor probe object file \"" + objectFilePath +
                    "\": " + objectFileException.Message,
                    objectFileException);
            }

            byte[]? debugInfo = null;
            byte[]? debugAbbrev = null;
            byte[]? debugStr = null;
            byte[]? debugLineStr = null;

            foreach (DebugSection section in contents.DebugSections)
            {
                switch (section.Name)
                {
                    case DebugInfoSectionName:
                        debugInfo = section.Data;
                        break;
                    case DebugAbbrevSectionName:
                        debugAbbrev = section.Data;
                        break;
                    case DebugStrSectionName:
                        debugStr = section.Data;
                        break;
                    case DebugLineStrSectionName:
                        debugLineStr = section.Data;
                        break;
                }
            }

            if (debugInfo == null)
            {
                throw new CgoDebugInfoException(
                    "Object file \"" + objectFilePath +
                    "\" has no .debug_info section; the anchor probe must be " +
                    "compiled with -g so DWARF debug info is emitted.");
            }
            if (debugAbbrev == null)
            {
                throw new CgoDebugInfoException(
                    "Object file \"" + objectFilePath +
                    "\" has no .debug_abbrev section; DWARF cannot be decoded " +
                    "without the abbreviation table.");
            }

            return new DwarfSections(debugInfo, debugAbbrev, debugStr, debugLineStr);
        }

        internal static void PopulateFromCompilationUnit(
            DwarfCompilationUnit compilationUnit, CgoSymbolCatalog catalog)
        {
            DwarfTypeResolver resolver = new(compilationUnit);
            DwarfCTypeFormatter formatter = new(compilationUnit);

            foreach (DwarfDie candidate in EnumerateCandidateDies(compilationUnit))
            {
                ClassifyCandidateDie(candidate, catalog, resolver, formatter);
            }

            foreach (DwarfDie candidate in EnumerateCandidateDies(compilationUnit))
            {
                TryRegisterFunctionFromAnchorVariable(candidate, catalog, resolver, formatter);
            }
        }

        private static IEnumerable<DwarfDie> EnumerateCandidateDies(
            DwarfCompilationUnit compilationUnit)
        {
            foreach (DwarfDie topLevelDie in compilationUnit.TopLevelDies)
            {
                if (topLevelDie.Tag == DwarfTag.CompileUnit)
                {
                    foreach (DwarfDie child in topLevelDie.Children)
                    {
                        yield return child;
                    }
                    continue;
                }
                yield return topLevelDie;
            }
        }

        private static void ClassifyCandidateDie(
            DwarfDie die,
            CgoSymbolCatalog catalog,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter)
        {
            string? name = TryReadName(die);
            if (name != null && name.StartsWith(AnchorVariableNamePrefix, StringComparison.Ordinal))
            {
                return;
            }

            switch (die.Tag)
            {
                case DwarfTag.Typedef:
                    if (name == null)
                    {
                        return;
                    }
                    RegisterTypedefOrFunctionPointer(die, name, catalog, resolver, formatter);
                    break;
                case DwarfTag.StructureType:
                case DwarfTag.UnionType:
                    if (name == null)
                    {
                        return;
                    }
                    RegisterStructOrUnion(die, name, catalog, resolver, formatter);
                    break;
                case DwarfTag.EnumerationType:
                    RegisterEnum(die, name ?? SyntheticAnonymousEnumName(die), catalog, resolver);
                    break;
                case DwarfTag.Subprogram:
                    if (name == null)
                    {
                        return;
                    }
                    RegisterFunction(die, name, catalog, resolver, formatter);
                    break;
                case DwarfTag.Variable:
                    if (name == null)
                    {
                        return;
                    }
                    RegisterConstantVariable(die, name, catalog, resolver, formatter);
                    break;
            }
        }

        /// <summary>
        /// Synthetic catalog key for an anonymous enumeration type.
        /// The user-visible payload of an anonymous enum is its
        /// enumerators (e.g. <c>typedef enum { ZSTD_c_compressionLevel,
        /// ZSTD_c_windowLog, ... } ZSTD_cParameter;</c>); the enum
        /// itself never receives a tag in the C source. The catalog
        /// keys enums by name, so we synthesise a unique key from the
        /// DIE offset to keep the entry distinct from other anonymous
        /// enums that may co-exist in the same compilation unit. The
        /// synthetic name is never user-visible: <c>CgoSymbolBuilder</c>
        /// iterates the enumerators directly.
        /// </summary>
        private static string SyntheticAnonymousEnumName(DwarfDie enumerationDie)
        {
            return "__anonymous_enum_at_" + enumerationDie.OffsetInDebugInfo.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void RegisterTypedefOrFunctionPointer(
            DwarfDie typedefDie,
            string name,
            CgoSymbolCatalog catalog,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter)
        {
            DwarfDie referencedTarget;
            try
            {
                referencedTarget = resolver.ResolveTypeReference(typedefDie);
            }
            catch (CgoDebugInfoException inner)
            {
                throw new CgoDebugInfoException(
                    "Typedef '" + name + "' at DIE @" + typedefDie.OffsetInDebugInfo +
                    " could not be resolved: " + inner.Message,
                    name,
                    inner);
            }

            DwarfDie unwrappedTarget = resolver.UnwrapTypeAliases(referencedTarget);
            if (TryBuildFunctionPointerInfo(
                    name, unwrappedTarget, resolver, formatter, out CgoFunctionPointerInfo? functionPointer))
            {
                catalog.AddFunctionPointer(functionPointer!);
                return;
            }

            if (IsOpaqueStructOrUnion(unwrappedTarget, resolver, name))
            {
                catalog.AddOpaqueType(new CgoOpaqueTypeInfo(name));
            }
            else if (unwrappedTarget.Tag == DwarfTag.StructureType
                || unwrappedTarget.Tag == DwarfTag.UnionType)
            {
                RegisterPopulatedStructAliasForTypedef(
                    name, unwrappedTarget, catalog, resolver, formatter);
            }

            string aliasCType = formatter.Format(referencedTarget);
            catalog.AddTypedef(new CgoTypedefInfo(name, aliasCType));
        }

        /// <summary>
        /// Register a struct/union under a typedef name so that Go
        /// source written as <c>C.&lt;typedef&gt;.&lt;field&gt;</c>
        /// resolves through the typedef alone, without the user
        /// having to spell the underlying tag namespace (e.g.
        /// <c>C.compressStream2_result.return_code</c> works without
        /// the user reaching for <c>C.struct_compressStream2_result_s</c>).
        /// The same field set is registered under both names; the
        /// catalog stores them as independent <see cref="CgoStructInfo"/>
        /// entries so neither is sensitive to the other being deleted
        /// or renamed in the future.
        /// </summary>
        private static void RegisterPopulatedStructAliasForTypedef(
            string typedefName,
            DwarfDie unwrappedStructOrUnionDie,
            CgoSymbolCatalog catalog,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter)
        {
            DwarfResolvedStructLayout layout;
            try
            {
                layout = resolver.ResolveStructLayout(unwrappedStructOrUnionDie);
            }
            catch (CgoDebugInfoException inner)
            {
                throw new CgoDebugInfoException(
                    "Typedef '" + typedefName + "' target struct/union at DIE @" +
                    unwrappedStructOrUnionDie.OffsetInDebugInfo +
                    " could not be resolved while registering it as a typedef alias: " +
                    inner.Message,
                    typedefName,
                    inner);
            }
            if (layout.IsOpaque)
            {
                return;
            }

            AddStructOrUnionFromLayout(
                catalog, formatter, layout, cName: typedefName, goName: typedefName);
        }

        /// <summary>
        /// Whether a typedef's unwrapped target is a forward-declared
        /// struct or union. These aliases are the Go-visible names of
        /// opaque handle types like <c>sqlite3</c>, <c>ZSTD_CCtx</c>,
        /// or <c>CK_SESSION</c> — C users hold pointers to them
        /// without ever inspecting fields. Registering the typedef
        /// name in <see cref="CgoSymbolCatalog.OpaqueTypes"/>
        /// alongside the struct-tag entry lets the P/Invoke emitter
        /// resolve <c>C.ZSTD_CCtx</c> to an <c>IntPtr</c> without
        /// following the typedef chain itself.
        /// </summary>
        private static bool IsOpaqueStructOrUnion(
            DwarfDie unwrappedTarget, DwarfTypeResolver resolver, string typedefName)
        {
            if (unwrappedTarget.Tag != DwarfTag.StructureType
                && unwrappedTarget.Tag != DwarfTag.UnionType)
            {
                return false;
            }

            DwarfResolvedStructLayout layout;
            try
            {
                layout = resolver.ResolveStructLayout(unwrappedTarget);
            }
            catch (CgoDebugInfoException inner)
            {
                throw new CgoDebugInfoException(
                    "Typedef '" + typedefName + "' target struct at DIE @" +
                    unwrappedTarget.OffsetInDebugInfo +
                    " could not be resolved while classifying opacity: " + inner.Message,
                    typedefName,
                    inner);
            }
            return layout.IsOpaque;
        }

        private static bool TryBuildFunctionPointerInfo(
            string name,
            DwarfDie unwrappedTarget,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter,
            out CgoFunctionPointerInfo? functionPointer)
        {
            functionPointer = null;
            if (unwrappedTarget.Tag != DwarfTag.PointerType)
            {
                return false;
            }
            if (unwrappedTarget.TryGetAttribute(DwarfAttribute.Type) == null)
            {
                return false;
            }

            DwarfDie pointee = resolver.UnwrapTypeAliases(resolver.ResolveTypeReference(unwrappedTarget));
            if (pointee.Tag != DwarfTag.SubroutineType)
            {
                return false;
            }

            string returnCType;
            if (pointee.TryGetAttribute(DwarfAttribute.Type) == null)
            {
                returnCType = "void";
            }
            else
            {
                returnCType = formatter.Format(resolver.ResolveTypeReference(pointee));
            }

            List<string> parameterCTypes = new();
            bool isVariadic = false;
            foreach (DwarfDie child in pointee.Children)
            {
                if (child.Tag == DwarfTag.UnspecifiedParameters)
                {
                    isVariadic = true;
                    continue;
                }
                if (child.Tag != DwarfTag.FormalParameter)
                {
                    continue;
                }

                if (child.TryGetAttribute(DwarfAttribute.Type) == null)
                {
                    throw new CgoDebugInfoException(
                        "Function pointer '" + name + "' parameter DIE @" +
                        child.OffsetInDebugInfo + " is missing DW_AT_type.",
                        name);
                }
                parameterCTypes.Add(formatter.Format(resolver.ResolveTypeReference(child)));
            }

            functionPointer = new CgoFunctionPointerInfo(name, returnCType, parameterCTypes, isVariadic);
            return true;
        }

        private static void RegisterStructOrUnion(
            DwarfDie die,
            string name,
            CgoSymbolCatalog catalog,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter)
        {
            DwarfResolvedStructLayout layout;
            try
            {
                layout = resolver.ResolveStructLayout(die);
            }
            catch (CgoDebugInfoException inner)
            {
                throw new CgoDebugInfoException(
                    "Struct/union '" + name + "' at DIE @" + die.OffsetInDebugInfo +
                    " could not be resolved: " + inner.Message,
                    name,
                    inner);
            }

            if (layout.IsOpaque)
            {
                catalog.AddOpaqueType(new CgoOpaqueTypeInfo(name));
                // Register the C tag-namespace spelling (e.g. struct_ZSTD_CDict_s)
                // alongside the bare tag so AddOpaqueHandleTypes — which iterates
                // OpaqueTypes — surfaces both names. Otherwise a Go reference like
                // C.struct_ZSTD_CDict_s would fail to resolve because populated-only
                // tag aliases live in StructsAndUnions, never OpaqueTypes.
                string opaqueTagPrefix = layout.IsUnion ? "union_" : "struct_";
                catalog.AddOpaqueType(new CgoOpaqueTypeInfo(opaqueTagPrefix + name));
                return;
            }

            string tagKeyword = layout.IsUnion ? "union" : "struct";
            AddStructOrUnionFromLayout(
                catalog, formatter, layout,
                cName: tagKeyword + " " + name,
                goName: name);
        }

        /// <summary>
        /// Materialise <paramref name="layout"/> into a
        /// <see cref="CgoStructInfo"/> and add it to
        /// <paramref name="catalog"/>. Field DIEs are formatted into
        /// their C source spelling here so the catalog never carries
        /// raw DIE references. The <paramref name="cName"/> /
        /// <paramref name="goName"/> split lets the same layout be
        /// registered under both its tag form (e.g.
        /// <c>"struct ZSTD_inBuffer_s"</c> / <c>"ZSTD_inBuffer_s"</c>)
        /// and a typedef alias form (e.g.
        /// <c>"ZSTD_inBuffer"</c> / <c>"ZSTD_inBuffer"</c>) without
        /// duplicating the field-mapping loop.
        /// </summary>
        private static void AddStructOrUnionFromLayout(
            CgoSymbolCatalog catalog,
            DwarfCTypeFormatter formatter,
            DwarfResolvedStructLayout layout,
            string cName,
            string goName)
        {
            List<CgoFieldInfo> fields = new(layout.Fields.Count);
            foreach (DwarfResolvedField resolvedField in layout.Fields)
            {
                string fieldCType = formatter.Format(resolvedField.TypeDie);
                fields.Add(new CgoFieldInfo(
                    resolvedField.Name,
                    fieldCType,
                    resolvedField.OffsetBytes,
                    resolvedField.SizeBytes,
                    resolvedField.BitOffset,
                    resolvedField.BitSize));
            }

            catalog.AddStructOrUnion(new CgoStructInfo(
                cName: cName,
                goName: goName,
                fields: fields,
                isUnion: layout.IsUnion,
                sizeBytes: layout.SizeBytes,
                alignmentBytes: layout.AlignmentBytes));
        }

        private static void RegisterEnum(
            DwarfDie die,
            string name,
            CgoSymbolCatalog catalog,
            DwarfTypeResolver resolver)
        {
            DwarfResolvedEnum resolvedEnum;
            try
            {
                resolvedEnum = resolver.ResolveEnum(die);
            }
            catch (CgoDebugInfoException inner)
            {
                throw new CgoDebugInfoException(
                    "Enum '" + name + "' at DIE @" + die.OffsetInDebugInfo +
                    " could not be resolved: " + inner.Message,
                    name,
                    inner);
            }

            string underlyingCType = resolvedEnum.IsSigned ? "int" : "unsigned int";

            List<CgoEnumValue> values = new(resolvedEnum.Enumerators.Count);
            foreach (DwarfResolvedEnumerator enumerator in resolvedEnum.Enumerators)
            {
                values.Add(new CgoEnumValue(enumerator.Name, enumerator.Value));
            }

            catalog.AddEnum(new CgoEnumInfo(name, underlyingCType, values));
        }

        private static void RegisterFunction(
            DwarfDie subprogramDie,
            string name,
            CgoSymbolCatalog catalog,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter)
        {
            CgoFunctionInfo functionInfo = BuildFunctionInfoFromSignatureDie(
                name, subprogramDie, resolver, formatter);
            catalog.AddFunction(functionInfo);
        }

        /// <summary>
        /// Build a <see cref="CgoFunctionInfo"/> from a DIE whose shape
        /// carries a function signature — either a
        /// <see cref="DwarfTag.Subprogram"/> (named function definition)
        /// or a <see cref="DwarfTag.SubroutineType"/> (unnamed function
        /// type reached via an anchor variable). Both tags share the
        /// same attribute layout: <c>DW_AT_type</c> for the return
        /// type and <see cref="DwarfTag.FormalParameter"/> children for
        /// the parameters. The caller supplies the function name
        /// because a subroutine_type does not carry one.
        /// </summary>
        private static CgoFunctionInfo BuildFunctionInfoFromSignatureDie(
            string functionName,
            DwarfDie signatureDie,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter)
        {
            CgoFunctionInfo functionInfo = new()
            {
                Name = functionName,
                ReturnType = ReadSignatureReturnType(signatureDie, resolver, formatter),
                IsVariadic = HasVariadicChild(signatureDie),
            };

            int parameterIndex = 0;
            foreach (DwarfDie child in signatureDie.Children)
            {
                if (child.Tag != DwarfTag.FormalParameter)
                {
                    continue;
                }

                string parameterName = TryReadName(child) ?? ("p" + parameterIndex);
                if (child.TryGetAttribute(DwarfAttribute.Type) == null)
                {
                    throw new CgoDebugInfoException(
                        "Function '" + functionName + "' parameter '" + parameterName +
                        "' at DIE @" + child.OffsetInDebugInfo + " is missing DW_AT_type.",
                        functionName);
                }

                string parameterCType = formatter.Format(resolver.ResolveTypeReference(child));
                functionInfo.Parameters.Add(new CgoParameterInfo
                {
                    Name = parameterName,
                    CType = parameterCType,
                });
                parameterIndex++;
            }

            return functionInfo;
        }

        /// <summary>
        /// Post-pass classifier for anchor variables. When a Go source
        /// references <c>C.malloc</c>, the anchor probe emits
        /// <c>static __typeof__(malloc) *__ngo_anchor_malloc;</c>.
        /// GCC records that as a <see cref="DwarfTag.Variable"/> whose
        /// type chain is <see cref="DwarfTag.PointerType"/> →
        /// <see cref="DwarfTag.SubroutineType"/>, without ever emitting
        /// a <see cref="DwarfTag.Subprogram"/> with that name because
        /// the function body lives in libc rather than the probe's own
        /// translation unit. The top-level classifier therefore never
        /// sees the function; this method recovers it by walking every
        /// anchor variable and materialising a
        /// <see cref="CgoFunctionInfo"/> when the pointee is a
        /// subroutine type. Anchors whose pointee is anything else (a
        /// base type, struct, typedef, etc.) are left to the top-level
        /// classifier — those types are already carried as named DIEs
        /// and do not need the anchor-driven path.
        /// </summary>
        private static void TryRegisterFunctionFromAnchorVariable(
            DwarfDie die,
            CgoSymbolCatalog catalog,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter)
        {
            if (die.Tag != DwarfTag.Variable)
            {
                return;
            }

            string? anchorVariableName = TryReadName(die);
            if (anchorVariableName == null)
            {
                return;
            }
            if (!anchorVariableName.StartsWith(AnchorVariableNamePrefix, StringComparison.Ordinal))
            {
                return;
            }

            string goName = anchorVariableName.Substring(AnchorVariableNamePrefix.Length);
            if (goName.Length == 0)
            {
                return;
            }

            if (die.TryGetAttribute(DwarfAttribute.Type) == null)
            {
                return;
            }

            DwarfDie anchorType = resolver.UnwrapTypeAliases(
                resolver.ResolveTypeReference(die));
            if (anchorType.Tag != DwarfTag.PointerType)
            {
                return;
            }
            if (anchorType.TryGetAttribute(DwarfAttribute.Type) == null)
            {
                return;
            }

            DwarfDie pointee = resolver.UnwrapTypeAliases(
                resolver.ResolveTypeReference(anchorType));
            if (pointee.Tag != DwarfTag.SubroutineType)
            {
                return;
            }

            if (catalog.Functions.ContainsKey(goName))
            {
                return;
            }

            CgoFunctionInfo functionInfo = BuildFunctionInfoFromSignatureDie(
                goName, pointee, resolver, formatter);
            catalog.AddFunction(functionInfo);
        }

        private static void RegisterConstantVariable(
            DwarfDie variableDie,
            string name,
            CgoSymbolCatalog catalog,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter)
        {
            DwarfAttributeValue? constValue = variableDie.TryGetAttribute(DwarfAttribute.ConstValue);
            if (constValue == null)
            {
                return;
            }

            long value = constValue.AsInteger();
            if (variableDie.TryGetAttribute(DwarfAttribute.Type) == null)
            {
                throw new CgoDebugInfoException(
                    "Constant variable '" + name + "' at DIE @" +
                    variableDie.OffsetInDebugInfo +
                    " carries DW_AT_const_value but no DW_AT_type; " +
                    "the underlying C type cannot be determined.",
                    name);
            }

            string underlyingCType = formatter.Format(resolver.ResolveTypeReference(variableDie));
            catalog.AddMacroConstant(new CgoMacroConstantInfo(name, value, underlyingCType));
        }

        private static string ReadSignatureReturnType(
            DwarfDie signatureDie,
            DwarfTypeResolver resolver,
            DwarfCTypeFormatter formatter)
        {
            if (signatureDie.TryGetAttribute(DwarfAttribute.Type) == null)
            {
                return "void";
            }
            return formatter.Format(resolver.ResolveTypeReference(signatureDie));
        }

        private static bool HasVariadicChild(DwarfDie signatureDie)
        {
            foreach (DwarfDie child in signatureDie.Children)
            {
                if (child.Tag == DwarfTag.UnspecifiedParameters)
                {
                    return true;
                }
            }
            return false;
        }

        private static string? TryReadName(DwarfDie die)
        {
            DwarfAttributeValue? nameAttribute = die.TryGetAttribute(DwarfAttribute.Name);
            if (nameAttribute == null)
            {
                return null;
            }
            return nameAttribute.AsString();
        }
    }
}

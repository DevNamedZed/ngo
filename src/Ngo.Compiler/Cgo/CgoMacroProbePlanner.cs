using System;
using System.Collections.Generic;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Planner and harvester for the cgo macro probe (pass 2). The
    /// typeof anchor probe run in pass 1 surfaces every C identifier
    /// that refers to a typedef, struct, function, variable, enum, or
    /// opaque handle — anything whose identity DWARF can carry.
    /// Preprocessor <c>#define</c> macros evaporate before the compiler
    /// emits debug info, so their names appear in the Go usage set yet
    /// land in none of the catalog dictionaries after pass 1. The
    /// planner identifies those leftover names so the caller can build
    /// a targeted pass-2 probe, then harvests the resulting enumerator
    /// DIEs and registers them as <see cref="CgoMacroConstantInfo"/>.
    /// </summary>
    public static class CgoMacroProbePlanner
    {
        /// <summary>
        /// C base-type keywords and <c>&lt;stddef.h&gt;</c> primitives
        /// that the Go-visible <c>C</c> package exports directly without
        /// surfacing them through the DWARF catalog. They appear in a
        /// usage set whenever Go code writes something like
        /// <c>C.int(x)</c> or <c>C.size_t(n)</c>, but they are not
        /// <c>#define</c> macros — wrapping them in
        /// <c>enum { _ngo_macro_int = (int) };</c> would be a syntax
        /// error because <c>(int)</c> is a cast with no operand. The
        /// planner drops these from the leftover set so the pass-2
        /// macro probe never sees them. Keep in sync with
        /// <c>CgoSymbolBuilder.AddPrimitiveTypeAliases</c>.
        /// </summary>
        private static readonly HashSet<string> CgoBaseTypeNames =
            new(StringComparer.Ordinal)
            {
                "char",
                "short",
                "int",
                "long",
                "float",
                "double",
                "size_t",
            };

        /// <summary>
        /// Return the subset of <paramref name="usageSet"/> names that
        /// are still unknown after pass 1. A name counts as known when
        /// it is recorded in any dictionary on <paramref name="catalog"/>
        /// — typedef, struct or union (including the <c>struct_</c> /
        /// <c>union_</c> tag spellings), enum (including the
        /// <c>enum_</c> tag spelling), enum value, function, function
        /// pointer, opaque type, or an already-harvested macro constant
        /// — or when it names one of the C base-type keywords the
        /// <c>C</c> package exports directly.
        /// </summary>
        public static IReadOnlyList<string> CollectLeftoverNames(
            CgoUsageSet usageSet, CgoSymbolCatalog catalog)
        {
            if (usageSet == null)
            {
                throw new ArgumentNullException(nameof(usageSet));
            }
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            HashSet<string> known = BuildKnownNameSet(catalog);
            List<string> leftovers = new();
            foreach (string name in usageSet.Names)
            {
                if (known.Contains(name))
                {
                    continue;
                }
                if (CgoBaseTypeNames.Contains(name))
                {
                    continue;
                }
                leftovers.Add(name);
            }
            return leftovers;
        }

        /// <summary>
        /// Copy every <c>__ngo_macro_&lt;name&gt;</c> enumerator that the
        /// pass-2 probe produced into <paramref name="primary"/> as a
        /// <see cref="CgoMacroConstantInfo"/>. Enumerators without the
        /// configured prefix belong to genuine anonymous enums carried
        /// in by the preamble (the probe source itself contains no other
        /// enums) and are therefore ignored; they will already have been
        /// registered by the pass-1 reader on the regular catalog.
        /// </summary>
        public static void RegisterMacroConstants(
            CgoSymbolCatalog primary, CgoSymbolCatalog macroProbe)
        {
            if (primary == null)
            {
                throw new ArgumentNullException(nameof(primary));
            }
            if (macroProbe == null)
            {
                throw new ArgumentNullException(nameof(macroProbe));
            }

            string prefix = CgoProbeGenerator.MacroEnumeratorPrefix;
            foreach (CgoEnumInfo enumInfo in macroProbe.Enums.Values)
            {
                foreach (CgoEnumValue value in enumInfo.Values)
                {
                    if (!value.Name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string macroName = value.Name.Substring(prefix.Length);
                    if (macroName.Length == 0)
                    {
                        continue;
                    }
                    primary.AddMacroConstant(new CgoMacroConstantInfo(
                        macroName, value.Value, enumInfo.UnderlyingCType));
                }
            }
        }

        private static HashSet<string> BuildKnownNameSet(CgoSymbolCatalog catalog)
        {
            HashSet<string> known = new(StringComparer.Ordinal);

            foreach (string name in catalog.Typedefs.Keys)
            {
                known.Add(name);
            }
            foreach (CgoStructInfo info in catalog.StructsAndUnions.Values)
            {
                known.Add(info.GoName);
                string tagPrefix = info.IsUnion ? "union_" : "struct_";
                known.Add(tagPrefix + info.GoName);
            }
            foreach (CgoEnumInfo info in catalog.Enums.Values)
            {
                known.Add(info.Name);
                known.Add("enum_" + info.Name);
                foreach (CgoEnumValue value in info.Values)
                {
                    known.Add(value.Name);
                }
            }
            foreach (string name in catalog.Functions.Keys)
            {
                known.Add(name);
            }
            foreach (string name in catalog.OpaqueTypes.Keys)
            {
                known.Add(name);
            }
            foreach (string name in catalog.FunctionPointers.Keys)
            {
                known.Add(name);
            }
            foreach (string name in catalog.MacroConstants.Keys)
            {
                known.Add(name);
            }

            return known;
        }
    }
}

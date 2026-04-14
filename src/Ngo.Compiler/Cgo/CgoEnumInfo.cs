using System.Collections.Generic;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Catalog entry for a C enumeration type — the name of the
    /// enum, the integer type the compiler chose for its storage,
    /// and every enumerator it declares. Anonymous enums are still
    /// represented: they are registered under the synthetic name
    /// that the DWARF or PDB reader assigns (usually the first
    /// enumerator), so downstream code never has to reason about
    /// the absence of a name.
    /// </summary>
    public sealed class CgoEnumInfo
    {
        public CgoEnumInfo(string name, string underlyingCType, IReadOnlyList<CgoEnumValue> values)
        {
            Name = name;
            UnderlyingCType = underlyingCType;
            Values = values;
        }

        public string Name { get; }

        /// <summary>
        /// C type the compiler chose for the enum's storage — for
        /// example <c>int</c>, <c>unsigned int</c>, or on some
        /// platforms <c>long</c>. Propagated verbatim from debug
        /// info so the P/Invoke layer can pick the right .NET
        /// integer type and width.
        /// </summary>
        public string UnderlyingCType { get; }

        public IReadOnlyList<CgoEnumValue> Values { get; }
    }
}

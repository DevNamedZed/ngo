namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// A C <c>typedef</c> — the alias name the user writes in
    /// source and the C type it resolves to. The alias target is
    /// kept verbatim (the compiler's fully-qualified form,
    /// e.g. <c>struct ZSTD_CCtx *</c>) so the marshalling layer
    /// can chase the chain itself rather than inheriting a
    /// partially-flattened representation.
    /// </summary>
    public sealed class CgoTypedefInfo
    {
        public CgoTypedefInfo(string name, string aliasCType)
        {
            Name = name;
            AliasCType = aliasCType;
        }

        public string Name { get; }

        public string AliasCType { get; }
    }
}

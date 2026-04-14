namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Reads C symbols out of a compiled anchor probe and produces
    /// a normalised <see cref="CgoSymbolCatalog"/>. There are two
    /// implementations, picked by the resolved compiler family:
    /// a DWARF reader for gcc / clang / clang-cl-in-DWARF-mode and
    /// a PDB reader for MSVC / clang-cl.
    ///
    /// Contract:
    /// <list type="bullet">
    ///   <item>The reader walks the debug-info container in the
    ///         probe's object file (and, for MSVC, its program
    ///         database) and registers every type, function,
    ///         function pointer, typedef, enum, opaque handle, and
    ///         macro-derived constant it finds.</item>
    ///   <item>On a structural failure — corrupt DWARF, unexpected
    ///         CodeView record — the implementation throws. It
    ///         never returns a partial catalog with silent gaps.</item>
    ///   <item>The reader does not attempt to map C types to .NET
    ///         types. That is the P/Invoke emitter's responsibility.</item>
    /// </list>
    /// </summary>
    public interface ICgoSymbolSource
    {
        CgoSymbolCatalog Extract(CgoAnchorProbeBuildResult probeResult);
    }
}

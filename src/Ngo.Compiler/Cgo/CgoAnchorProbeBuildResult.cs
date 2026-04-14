namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Output of compiling the anchor probe: paths to the artifacts
    /// that downstream DWARF / PDB readers will consume, plus the
    /// compiler identity so readers can pick the right format.
    /// </summary>
    public sealed class CgoAnchorProbeBuildResult
    {
        public CgoAnchorProbeBuildResult(
            string objectFilePath,
            CCompilerInfo compiler,
            string? programDatabasePath)
        {
            ObjectFilePath = objectFilePath;
            Compiler = compiler;
            ProgramDatabasePath = programDatabasePath;
        }

        /// <summary>
        /// Absolute path of the compiled object file
        /// (<c>.o</c> on Unix toolchains, <c>.obj</c> on MSVC).
        /// Contains the DWARF debug info on gcc/clang.
        /// </summary>
        public string ObjectFilePath { get; }

        public CCompilerInfo Compiler { get; }

        /// <summary>
        /// MSVC / clang-cl only — path to the <c>.pdb</c> emitted by
        /// the linker when the probe is compiled with <c>/Z7</c> or
        /// <c>/Zi</c>. Null for toolchains that embed debug info
        /// directly in the object file.
        /// </summary>
        public string? ProgramDatabasePath { get; }
    }
}

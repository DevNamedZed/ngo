namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// A successful compiler-resolution result. Carries the chosen
    /// <see cref="CCompilerInfo"/> and which input (CLI flag, environment
    /// variable, or auto-detect) ultimately selected it.
    /// </summary>
    public sealed class CgoCompilerResolution
    {
        public CgoCompilerResolution(CCompilerInfo compiler, CgoCompilerSource source)
        {
            Compiler = compiler;
            Source = source;
        }

        public CCompilerInfo Compiler { get; }

        public CgoCompilerSource Source { get; }
    }
}

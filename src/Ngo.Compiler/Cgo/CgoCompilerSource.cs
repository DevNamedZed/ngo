namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Which input resolved the C compiler. Ordered by precedence from
    /// highest to lowest.
    /// </summary>
    public enum CgoCompilerSource
    {
        /// <summary>Resolved from the <c>--cc</c> CLI flag.</summary>
        CliFlag,

        /// <summary>Resolved from the <c>CC</c> environment variable.</summary>
        Environment,

        /// <summary>Resolved by platform-default auto-detection.</summary>
        AutoDetect,
    }
}

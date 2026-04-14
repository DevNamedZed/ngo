namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// A free-standing integer constant the compiler recorded in
    /// debug info — typically a preprocessor macro such as
    /// <c>#define CKA_CLASS 0UL</c> that the anchor probe forces
    /// into a sizeof or initialiser context, causing the compiler
    /// to emit a <c>DW_TAG_constant</c> or <c>S_CONSTANT</c>
    /// record the reader can pick up. Distinct from
    /// <see cref="CgoEnumValue"/> because it has no enclosing
    /// enum: the P/Invoke emitter exposes each macro constant as
    /// a top-level <c>const</c> on the generated C pseudo-package.
    /// </summary>
    public sealed class CgoMacroConstantInfo
    {
        public CgoMacroConstantInfo(string name, long value, string underlyingCType)
        {
            Name = name;
            Value = value;
            UnderlyingCType = underlyingCType;
        }

        public string Name { get; }

        public long Value { get; }

        /// <summary>
        /// C type the compiler assigned to the constant. Captured
        /// verbatim (<c>int</c>, <c>unsigned long</c>, …) so the
        /// emitter can pick the .NET integer width that matches
        /// the original declaration rather than truncating to
        /// <c>int</c>.
        /// </summary>
        public string UnderlyingCType { get; }
    }
}

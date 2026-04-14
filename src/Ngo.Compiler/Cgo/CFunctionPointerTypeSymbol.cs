using Ngo.Compiler.Symbols;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// A C function pointer value. Produced in two places:
    /// a pointer-to-subroutine parameter type recovered from DWARF by
    /// <see cref="CgoSymbolBuilder.MapCToGoType"/>, and the value yielded when a
    /// Go expression references a C function by name without calling it
    /// (e.g. <c>C.callbackTrampoline</c>). Cgo treats every such value as an
    /// opaque function pointer interchangeable with <c>unsafe.Pointer</c> and
    /// with the Go idiom <c>*[0]byte</c>; the type checker mirrors that rule
    /// and never matches on the carried signature, which exists only for
    /// diagnostics.
    /// </summary>
    public sealed class CFunctionPointerTypeSymbol : TypeSymbol
    {
        public CFunctionPointerTypeSymbol(string displayName)
            : base(displayName, TypeKind.Pointer, null)
        {
        }
    }
}

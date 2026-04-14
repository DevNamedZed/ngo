using System;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Thrown when the source uses <c>import "C"</c> but the environment
    /// has <c>CGO_ENABLED=0</c>, which matches Go's behaviour: an explicit
    /// opt-out disables cgo even if a compiler is resolvable.
    /// </summary>
    public sealed class CgoDisabledException : Exception
    {
        public CgoDisabledException(string message) : base(message)
        {
        }
    }
}

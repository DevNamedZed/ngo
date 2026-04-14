using System.Collections.Generic;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// A named C function-pointer typedef — for example
    /// <c>typedef int (*sqlite3_callback)(void *, int, char **, char **);</c>.
    /// Kept as a separate catalog entry from <see cref="CgoFunctionInfo"/>
    /// because the P/Invoke emitter generates a delegate type for
    /// pointers and a direct entry point for functions: conflating
    /// them would force the emitter to re-classify every entry.
    /// </summary>
    public sealed class CgoFunctionPointerInfo
    {
        public CgoFunctionPointerInfo(
            string name,
            string returnCType,
            IReadOnlyList<string> parameterCTypes,
            bool isVariadic)
        {
            Name = name;
            ReturnCType = returnCType;
            ParameterCTypes = parameterCTypes;
            IsVariadic = isVariadic;
        }

        public string Name { get; }

        public string ReturnCType { get; }

        public IReadOnlyList<string> ParameterCTypes { get; }

        public bool IsVariadic { get; }
    }
}

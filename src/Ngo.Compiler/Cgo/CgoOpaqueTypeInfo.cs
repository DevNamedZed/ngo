namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// A C type whose name is known but whose layout is not —
    /// forward-declared structs, opaque-by-design handle types
    /// like <c>sqlite3</c>, <c>ZSTD_CCtx</c>, or
    /// <c>CK_SESSION_HANDLE</c>. Registered as a dedicated catalog
    /// entry so the P/Invoke emitter lowers uses of the type to an
    /// <c>IntPtr</c> without ever having to inspect struct fields
    /// that do not exist, and so a bug in a reader can never
    /// confuse an opaque handle with an empty struct.
    /// </summary>
    public sealed class CgoOpaqueTypeInfo
    {
        public CgoOpaqueTypeInfo(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}

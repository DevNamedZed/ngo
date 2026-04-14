namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// A single enumerator in a C enum declaration — the name of
    /// the enumerator and the integer value the compiler assigned
    /// to it. Values are stored as <c>long</c> so the full signed
    /// <c>int64_t</c> range is representable; enums with
    /// <c>unsigned long long</c> underlying types that exceed
    /// <c>long.MaxValue</c> are rejected at read time rather than
    /// silently truncated.
    /// </summary>
    public sealed class CgoEnumValue
    {
        public CgoEnumValue(string name, long value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public long Value { get; }
    }
}

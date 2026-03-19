using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Fuzz
{
    /// <summary>
    /// internal/fuzz — fuzzing runtime support. Stubs for .NET.
    /// </summary>
    [GoPackage("internal/fuzz")]
    public static class Package
    {
        [GoType("struct", Name = "CorpusEntry", Package = "internal/fuzz")]
        public class GoCorpusEntry
        {
            [GoField(Name = "Parent")] public string Parent = "";
            [GoField(Name = "Path")] public string Path = "";
            [GoField(Name = "Data")] public Slice<byte> Data;
            [GoField(Name = "Values")] public Slice<object> Values;
            [GoField(Name = "Generation")] public long Generation;
            [GoField(Name = "IsSeed")] public bool IsSeed;
        }

        [GoFunc]
        [return: GoReturn("[]CorpusEntry", "error")]
        public static (Slice<GoCorpusEntry>, object?) ReadCorpus(string dir, Slice<object> types)
            => (default, null);

        [GoFunc]
        public static void ResetCoverage() { }

        [GoFunc]
        [return: GoReturn("int")]
        public static long SnapshotCoverage() => 0;
    }
}

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Xcoff
{
    [GoPackage("internal/xcoff")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*File", "error")]
        public static (GoFile?, object?) NewFile([GoParam("io.ReaderAt")] object? r)
            => (null, "xcoff: not supported");

        [GoFunc]
        [return: GoReturn("*File", "error")]
        public static (GoFile?, object?) Open(string name)
            => (null, "xcoff: not supported");

        [GoConst] public const long STYP_DATA = 0x40;
        [GoConst] public const long STYP_BSS = 0x80;
        [GoConst] public const long STYP_TEXT = 0x20;
    }

    [GoType("struct", Name = "File", Package = "internal/xcoff")]
    public class GoFile
    {
        [GoField(Name = "Sections")] public Slice<GoSection> Sections;
        [GoMethod] public void Close() { }
        [GoMethod] [return: GoReturn("*Section")] public GoSection? SectionByType([GoParam("uint32")] long typ) => null;
    }

    [GoType("struct", Name = "Section", Package = "internal/xcoff")]
    public class GoSection
    {
        [GoField(Name = "Type")] public long Type;
        [GoField(Name = "Size")] public long Size;
        [GoField(Name = "VirtualAddress")] public long VirtualAddress;
        [GoMethod] [return: GoReturn("[]byte", "error")] public (Slice<byte>, object?) Data() => (default, null);
        [GoMethod] [return: GoReturn("io.ReadSeeker")] public object? Open() => null;
    }
}

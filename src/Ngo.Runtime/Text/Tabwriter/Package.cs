using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Text.Tabwriter
{
    [GoPackage("text/tabwriter")]
    public static class Package
    {
        // tabwriter.NewWriter(output io.Writer, minwidth, tabwidth, padding int, padchar byte, flags uint) *Writer
        [GoFunc]
        [return: GoReturn("*tabwriter.Writer")]
        public static GoWriter NewWriter(object? output, [GoParam("int")] long minwidth,
            [GoParam("int")] long tabwidth, [GoParam("int")] long padding, byte padchar,
            [GoParam("uint")] ulong flags) => new GoWriter();

        // Constants
        [GoConst(Type = "uint")]
        public const long FilterHTML = 1;

        [GoConst(Type = "uint")]
        public const long StripEscape = 2;

        [GoConst(Type = "uint")]
        public const long AlignRight = 4;

        [GoConst(Type = "uint")]
        public const long DiscardEmptyColumns = 8;

        [GoConst(Type = "uint")]
        public const long TabIndent = 16;

        [GoConst(Type = "uint")]
        public const long Debug = 32;

        [GoConst(Type = "byte")]
        public const long Escape = 0xFF;
    }

    [GoType("struct", Name = "Writer", Package = "text/tabwriter")]
    public class GoWriter
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> buf) => (0, null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? Flush() => null;

        [GoMethod]
        [return: GoReturn("*tabwriter.Writer")]
        public GoWriter Init(object? output, [GoParam("int")] long minwidth,
            [GoParam("int")] long tabwidth, [GoParam("int")] long padding, byte padchar,
            [GoParam("uint")] ulong flags) => this;
    }
}

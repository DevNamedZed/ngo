using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Bufio
{
    [GoType("struct", Name = "ReadWriter", Package = "bufio")]
    public sealed class ReadWriter
    {
        [GoField(Name = "Reader", Type = "*bufio.Reader")] public Reader Reader { get; }
        [GoField(Name = "Writer", Type = "*bufio.Writer")] public Writer Writer { get; }

        public ReadWriter(Reader reader, Writer writer)
        {
            Reader = reader;
            Writer = writer;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Flush() => Writer.Flush();
    }
}

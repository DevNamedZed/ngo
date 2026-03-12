using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net.Textproto
{
    [GoPackage("net/textproto")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*textproto.Reader")]
        public static GoReader NewReader([GoParam("*bufio.Reader")] object? r)
        {
            return new GoReader();
        }

        [GoFunc]
        public static string CanonicalMIMEHeaderKey(string s)
        {
            return s; // stub
        }

        [GoFunc]
        public static string TrimString(string s)
        {
            return s?.Trim() ?? "";
        }

        [GoFunc]
        [return: GoReturn("*textproto.Conn")]
        public static GoConn NewConn([GoParam("io.ReadWriteCloser")] object? conn)
        {
            return new GoConn();
        }

        [GoFunc]
        [return: GoReturn("*textproto.Conn", "error")]
        public static (GoConn, object?) Dial(string network, string addr)
        {
            return (new GoConn(), null);
        }
    }

    // MIMEHeader is map[string][]string with methods
    [GoType("named", Name = "MIMEHeader", Package = "net/textproto", Underlying = "map[string][]string")]
    public struct GoMIMEHeader
    {
        public Map<string, Slice<string>> Value;

        public GoMIMEHeader(Map<string, Slice<string>> v) { Value = v; }

        [GoMethod]
        public void Add(string key, string value)
        {
            // stub
        }

        [GoMethod]
        public void Set(string key, string value)
        {
            // stub
        }

        [GoMethod]
        public string Get(string key)
        {
            return "";
        }

        [GoMethod]
        public Slice<string> Values(string key)
        {
            return new Slice<string>();
        }

        [GoMethod]
        public void Del(string key)
        {
            // stub
        }
    }

    [GoType("struct", Name = "Reader", Package = "net/textproto")]
    public class GoReader
    {
        [GoField(Type = "*bufio.Reader")] public object? R;

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, object?) ReadLine() => ("", null);

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, object?) ReadContinuedLine() => ("", null);

        [GoMethod]
        [return: GoReturn("textproto.MIMEHeader", "error")]
        public (GoMIMEHeader, object?) ReadMIMEHeader() => (new GoMIMEHeader(), null);

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, object?) ReadDotBytes() => (new Slice<byte>(), null);

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) ReadCodeLine(long expectCode) => (0, null);

        [GoMethod]
        [return: GoReturn("int", "string", "error")]
        public (long, string, object?) ReadResponse(long expectCode) => (0, "", null);

        [GoMethod]
        [return: GoReturn("[]string", "error")]
        public (Slice<string>, object?) ReadDotLines() => (new Slice<string>(), null);
    }

    [GoType("struct", Name = "Writer", Package = "net/textproto")]
    public class GoWriter
    {
        [GoField(Type = "*bufio.Writer")] public object? W;

        [GoMethod]
        [return: GoReturn("io.WriteCloser")]
        public object DotWriter() => new object();

        [GoMethod]
        [return: GoReturn("int64", "error")]
        public (long, object?) PrintfLine(string format, params object[] args) => (0, null);
    }

    [GoType("struct", Name = "Error", Package = "net/textproto")]
    public class GoError
    {
        [GoField] public long Code;
        [GoField] public string Msg;

        [GoMethod]
        public string Error() => $"{Code} {Msg}";
    }

    [GoType("struct", Name = "Pipeline", Package = "net/textproto")]
    public class GoPipeline
    {
        [GoMethod]
        public ulong Next() => 0;

        [GoMethod]
        public void StartRequest(ulong id) { }

        [GoMethod]
        public void EndRequest(ulong id) { }

        [GoMethod]
        public void StartResponse(ulong id) { }

        [GoMethod]
        public void EndResponse(ulong id) { }
    }

    [GoType("struct", Name = "Conn", Package = "net/textproto")]
    public class GoConn
    {
        [GoField(Name = "Reader", Type = "textproto.Reader", Embedded = true)] public GoReader Reader;
        [GoField(Name = "Writer", Type = "textproto.Writer", Embedded = true)] public GoWriter Writer;
        [GoField(Name = "Pipeline", Type = "textproto.Pipeline", Embedded = true)] public GoPipeline Pipeline;

        public GoConn()
        {
            Reader = new GoReader();
            Writer = new GoWriter();
            Pipeline = new GoPipeline();
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        [GoMethod]
        [return: GoReturn("uint", "error")]
        public (ulong, object?) Cmd(string format, params object[] args) => (Pipeline.Next(), null);
    }
}

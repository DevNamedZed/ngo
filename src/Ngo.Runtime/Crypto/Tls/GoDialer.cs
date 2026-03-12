using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Crypto.Tls
{
    [GoType("struct", Name = "Dialer", Package = "crypto/tls")]
    public class GoDialer
    {
        [GoField(Name = "Config", Type = "*tls.Config")] public GoConfig? Config;
        [GoField(Name = "NetDialer", Type = "*net.Dialer")] public object? NetDialer;

        [GoMethod]
        [return: GoReturn("net.Conn", "error")]
        public (object?, object?) DialContext([GoParam("context.Context")] object? ctx, string network, string addr)
            => (null, null);
    }

    [GoType("struct", Name = "RecordHeaderError", Package = "crypto/tls")]
    public class GoRecordHeaderError
    {
        [GoField(Name = "Msg")] public string Msg = "";
        [GoField(Name = "RecordHeader", Type = "[5]byte")] public Slice<byte> RecordHeader;
        [GoField(Name = "Conn", Type = "net.Conn")] public object? Conn;

        [GoMethod]
        public string Error() => Msg;
    }
}

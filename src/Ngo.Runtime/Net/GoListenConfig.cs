using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "ListenConfig", Package = "net")]
    public class GoListenConfig
    {
        [GoField(Name = "Control", Type = "func(network string, address string, c syscall.RawConn) error")]
        public object? Control;

        [GoField(Name = "KeepAlive")] public long KeepAlive;

        [GoField(Name = "KeepAliveConfig")] public object? KeepAliveConfig;

        [GoMethod]
        [return: GoReturn("net.Listener", "error")]
        public (object?, object?) Listen([GoParam("context.Context")] object? ctx, string network, string address)
        {
            return GoNet.Listen(network, address);
        }

        [GoMethod]
        [return: GoReturn("net.PacketConn", "error")]
        public (object?, object?) ListenPacket([GoParam("context.Context")] object? ctx, string network, string address)
        {
            return (null, "net: ListenPacket not supported");
        }
    }
}

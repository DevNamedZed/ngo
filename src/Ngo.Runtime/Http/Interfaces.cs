using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("interface", Name = "Flusher", Package = "net/http")]
    public interface IFlusher
    {
        [GoMethod]
        void Flush();
    }

    [GoType("interface", Name = "Hijacker", Package = "net/http")]
    public interface IHijacker
    {
        [GoMethod]
        [return: GoReturn("net.Conn", "*bufio.ReadWriter", "error")]
        (object, object, object?) Hijack();
    }

    [GoType("interface", Name = "CloseNotifier", Package = "net/http")]
    public interface ICloseNotifier { }

    [GoType("interface", Name = "Pusher", Package = "net/http")]
    public interface IPusher { }

    [GoType("interface", Name = "RoundTripper", Package = "net/http")]
    public interface IRoundTripper
    {
        [GoMethod]
        [return: GoReturn("*Response", "error")]
        (object?, object?) RoundTrip(Request req);
    }

    [GoType("interface", Name = "CookieJar", Package = "net/http")]
    public interface ICookieJar { }

    [GoType("interface", Name = "FileSystem", Package = "net/http")]
    public interface IFileSystem { }

    [GoType("interface", Name = "File", Package = "net/http")]
    public interface IFile { }
}

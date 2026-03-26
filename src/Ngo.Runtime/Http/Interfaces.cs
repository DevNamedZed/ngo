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
    public interface ICloseNotifier
    {
        [GoMethod]
        [return: GoReturn("<-chan bool")]
        Ngo.Runtime.Channel<bool> CloseNotify();
    }

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
    public interface IFileSystem
    {
        [GoMethod]
        [return: GoReturn("http.File", "error")]
        (IFile, object?) Open(string name);
    }

    [GoType("interface", Name = "File", Package = "net/http")]
    public interface IFile
    {
        [GoMethod]
        [return: GoReturn("error")]
        object? Close();

        [GoMethod]
        [return: GoReturn("int", "error")]
        (long, object?) Read(Slice<byte> p);

        [GoMethod]
        [return: GoReturn("int64", "error")]
        (long, object?) Seek(long offset, long whence);

        [GoMethod]
        [return: GoReturn("[]fs.FileInfo", "error")]
        (Slice<object?>, object?) Readdir(long count);

        [GoMethod]
        [return: GoReturn("fs.FileInfo", "error")]
        (object?, object?) Stat();
    }
}

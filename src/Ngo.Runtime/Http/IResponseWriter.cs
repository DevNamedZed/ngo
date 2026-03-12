using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("interface", Name = "ResponseWriter", Package = "net/http")]
    public interface IResponseWriter
    {
        [GoMethod]
        Header Header();

        [GoMethod]
        [return: GoReturn("int", "error")]
        (int, string) Write(Slice<byte> p);

        [GoMethod]
        void WriteHeader(long statusCode);
    }
}

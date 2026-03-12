using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    public class ResponseWriter : IResponseWriter
    {
        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Write(Slice<byte> p) => (p.Len, null!);

        [GoMethod]
        public void WriteHeader(long statusCode) { }

        [GoMethod]
        public Header Header() => new Header();
    }
}

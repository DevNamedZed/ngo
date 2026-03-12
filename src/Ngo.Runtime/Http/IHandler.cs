using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("interface", Name = "Handler", Package = "net/http")]
    public interface IHandler
    {
        [GoMethod]
        void ServeHTTP(ResponseWriter w, Request r);
    }
}

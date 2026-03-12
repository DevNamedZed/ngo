using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("named", Name = "HandlerFunc", Package = "net/http", Underlying = "func(ResponseWriter, *Request)")]
    public class HandlerFunc
    {
        [GoMethod]
        public void ServeHTTP(ResponseWriter w, Request r) { }
    }
}

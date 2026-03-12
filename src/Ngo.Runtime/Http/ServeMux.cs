using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "ServeMux", Package = "net/http")]
    public class ServeMux
    {
        [GoMethod]
        public void Handle(string pattern, object handler) { }

        [GoMethod]
        public void HandleFunc(string pattern, Action<ResponseWriter, Request> handler) { }

        [GoMethod]
        public void ServeHTTP(ResponseWriter w, Request r) { }

        [GoMethod]
        public (object?, string) Handler(Request r) => (null, "");
    }
}

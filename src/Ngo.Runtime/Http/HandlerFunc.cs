using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("named", Name = "HandlerFunc", Package = "net/http", Underlying = "func(ResponseWriter, *Request)")]
    public class HandlerFunc : IHandler
    {
        private readonly Action<ResponseWriter, Request>? _func;

        public HandlerFunc() { }

        public HandlerFunc(Action<ResponseWriter, Request> func)
        {
            _func = func;
        }

        [GoMethod]
        public void ServeHTTP(ResponseWriter w, Request r)
        {
            _func?.Invoke(w, r);
        }
    }
}

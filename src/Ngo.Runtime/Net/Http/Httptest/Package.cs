using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net.Http.Httptest
{
    /// <summary>
    /// Runtime support for Go's net/http/httptest package.
    /// </summary>
    [GoPackage("net/http/httptest")]
    public static class Package
    {
        [GoConst]
        public const string DefaultRemoteAddr = "1.2.3.4";

        [GoFunc]
        public static GoServer NewServer(object handler)
        {
            // Stub: create a test server
            return new GoServer
            {
                URL = "http://127.0.0.1:0",
            };
        }

        [GoFunc]
        public static GoResponseRecorder NewRecorder()
        {
            return new GoResponseRecorder
            {
                Code = 200,
                Flushed = false,
            };
        }
    }

    [GoType("struct", Name = "Server", Package = "net/http/httptest")]
    public class GoServer
    {
        [GoField]
        public string URL;

        [GoMethod]
        public void Close()
        {
            // Stub
        }

        [GoMethod]
        public void CloseClientConnections()
        {
            // Stub
        }
    }

    [GoType("struct", Name = "ResponseRecorder", Package = "net/http/httptest")]
    public class GoResponseRecorder
    {
        [GoField]
        public long Code;

        [GoField]
        public object Body;

        [GoField]
        public bool Flushed;

        [GoMethod]
        public object Result()
        {
            throw new NotImplementedException("httptest.ResponseRecorder.Result not yet implemented");
        }

        [GoMethod]
        public void Flush()
        {
            Flushed = true;
        }
    }
}

using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "Server", Package = "net/http")]
    public class Server
    {
        [GoField(Name = "Addr")] public string Addr { get; set; } = "";
        [GoField(Name = "Handler")] public object? Handler { get; set; }
        [GoField(Name = "ReadTimeout", Type = "time.Duration")] public long ReadTimeout { get; set; }
        [GoField(Name = "ReadHeaderTimeout", Type = "time.Duration")] public long ReadHeaderTimeout { get; set; }
        [GoField(Name = "WriteTimeout", Type = "time.Duration")] public long WriteTimeout { get; set; }
        [GoField(Name = "IdleTimeout", Type = "time.Duration")] public long IdleTimeout { get; set; }
        [GoField(Name = "MaxHeaderBytes")] public long MaxHeaderBytes { get; set; }
        [GoField(Name = "TLSConfig")] public object? TLSConfig { get; set; }
        [GoField(Name = "ErrorLog")] public object? ErrorLog { get; set; }
        [GoField(Name = "TLSNextProto")] public object? TLSNextProto { get; set; }
        [GoField(Name = "ConnState")] public object? ConnState { get; set; }

        [GoMethod]
        [return: GoReturn("error")]
        public object? ListenAndServe() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? ListenAndServeTLS(string certFile, string keyFile) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Serve(object? l) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? ServeTLS(object? l, string certFile, string keyFile) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close() => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Shutdown(object? ctx) => null;

        [GoMethod]
        public void SetKeepAlivesEnabled(bool v) { }

        [GoMethod]
        public void RegisterOnShutdown(Action f) { }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
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
        [GoField(Name = "BaseContext")] public object? BaseContext { get; set; }
        [GoField(Name = "ConnContext")] public object? ConnContext { get; set; }

        private HttpListener? _listener;
        private readonly List<Action> _shutdownCallbacks = new List<Action>();
        private volatile bool _closed;

        [GoMethod]
        [return: GoReturn("error")]
        public object? ListenAndServe()
        {
            string addr = Addr;
            if (string.IsNullOrEmpty(addr))
            {
                addr = ":http";
            }

            string prefix = BuildListenerPrefix(addr);

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                _listener.Start();

                while (!_closed)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = _listener.GetContext();
                    }
                    catch (HttpListenerException)
                    {
                        if (_closed)
                        {
                            break;
                        }
                        throw;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
            }
            catch (Exception ex)
            {
                if (_closed)
                {
                    return Package.ErrServerClosed;
                }
                return ex.Message;
            }

            return Package.ErrServerClosed;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? ListenAndServeTLS(string certFile, string keyFile)
        {
            // TLS via HttpListener requires OS-level certificate binding
            // For now, delegate to non-TLS (most Go programs use reverse proxies)
            return ListenAndServe();
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Serve(object? l) => ListenAndServe();

        [GoMethod]
        [return: GoReturn("error")]
        public object? ServeTLS(object? l, string certFile, string keyFile) => ListenAndServe();

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close()
        {
            _closed = true;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
                // Ignore close errors
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Shutdown(object? ctx)
        {
            _closed = true;

            foreach (var callback in _shutdownCallbacks)
            {
                callback();
            }

            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
                // Ignore close errors
            }
            return null;
        }

        [GoMethod]
        public void SetKeepAlivesEnabled(bool v) { }

        [GoMethod]
        public void RegisterOnShutdown(Action f)
        {
            _shutdownCallbacks.Add(f);
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var request = BuildRequest(context.Request);
                var responseWriter = new ResponseWriter
                {
                    ListenerResponse = context.Response,
                };

                var handler = Handler;
                if (handler == null)
                {
                    handler = Package.DefaultServeMux;
                }

                if (handler is IHandler h)
                {
                    h.ServeHTTP(responseWriter, request);
                }
                else if (handler is ServeMux mux)
                {
                    mux.ServeHTTP(responseWriter, request);
                }

                responseWriter.Flush();
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.StatusCode = 500;
                    var bytes = System.Text.Encoding.UTF8.GetBytes(ex.Message);
                    context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    context.Response.OutputStream.Close();
                }
                catch
                {
                    // Ignore write errors
                }
            }
        }

        private static Request BuildRequest(HttpListenerRequest listenerRequest)
        {
            var request = new Request
            {
                Method = listenerRequest.HttpMethod,
                URLPath = listenerRequest.Url?.AbsolutePath ?? "/",
                Host = listenerRequest.UserHostName ?? "",
                RemoteAddr = listenerRequest.RemoteEndPoint?.ToString() ?? "",
                RequestURI = listenerRequest.RawUrl ?? "/",
                Proto = $"HTTP/{listenerRequest.ProtocolVersion.Major}.{listenerRequest.ProtocolVersion.Minor}",
                ProtoMajor = listenerRequest.ProtocolVersion.Major,
                ProtoMinor = listenerRequest.ProtocolVersion.Minor,
                ContentLength = listenerRequest.ContentLength64,
            };

            // Build URL
            if (listenerRequest.Url != null)
            {
                var goUrl = new Url.GoURL
                {
                    Scheme = listenerRequest.Url.Scheme,
                    Host = listenerRequest.Url.Authority,
                    Path = listenerRequest.Url.AbsolutePath,
                    RawQuery = listenerRequest.Url.Query.TrimStart('?'),
                    Fragment = listenerRequest.Url.Fragment.TrimStart('#'),
                };
                request.URL = goUrl;
            }

            // Copy headers
            var header = new Header();
            foreach (string key in listenerRequest.Headers.AllKeys)
            {
                if (key != null)
                {
                    var values = listenerRequest.Headers.GetValues(key);
                    if (values != null)
                    {
                        foreach (var val in values)
                        {
                            header.Add(key, val);
                        }
                    }
                }
            }
            request.Header = header;

            // Body
            if (listenerRequest.HasEntityBody)
            {
                request.Body = new Io.StreamReaderAdapter(listenerRequest.InputStream);
            }

            return request;
        }

        private static string BuildListenerPrefix(string addr)
        {
            // Parse Go-style address ":8080" or "localhost:8080"
            string host = "+";
            string port = "80";

            if (addr.Contains(":"))
            {
                int colonIdx = addr.LastIndexOf(':');
                string hostPart = addr.Substring(0, colonIdx);
                string portPart = addr.Substring(colonIdx + 1);

                if (!string.IsNullOrEmpty(hostPart))
                {
                    host = hostPart;
                }
                if (!string.IsNullOrEmpty(portPart))
                {
                    if (portPart == "http")
                    {
                        port = "80";
                    }
                    else if (portPart == "https")
                    {
                        port = "443";
                    }
                    else
                    {
                        port = portPart;
                    }
                }
            }

            return $"http://{host}:{port}/";
        }
    }
}

using System;
using System.Net;
using System.Text;
using System.Threading;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Http;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Net.Http.Httptest
{
    [GoPackage("net/http/httptest")]
    public static class Package
    {
        [GoConst]
        public const string DefaultRemoteAddr = "1.2.3.4";

        [GoFunc]
        public static GoServer NewServer(object handler)
        {
            var server = new GoServer(handler);
            server.Start();
            return server;
        }

        [GoFunc]
        public static GoServer NewTLSServer(object handler)
        {
            // TLS test servers are complex in .NET — start plain HTTP
            return NewServer(handler);
        }

        [GoFunc]
        public static GoServer NewUnstartedServer(object handler)
        {
            return new GoServer(handler);
        }

        [GoFunc]
        public static GoResponseRecorder NewRecorder()
        {
            return new GoResponseRecorder();
        }

        [GoFunc]
        [return: GoReturn("*http.Request")]
        public static Request NewRequest(string method, string target, object? body)
        {
            var request = new Request
            {
                Method = method,
                URLPath = target,
                Proto = "HTTP/1.1",
                ProtoMajor = 1,
                ProtoMinor = 1,
                Header = new Header(),
                RemoteAddr = DefaultRemoteAddr + ":1234",
                RequestURI = target,
            };

            // Parse target into URL
            var (url, _) = Url.Package.Parse(target);
            if (url != null)
            {
                request.URL = url;
                request.Host = url.Host;
            }

            if (body is IGoReader)
            {
                request.Body = body;
            }

            return request;
        }
    }

    [GoType("struct", Name = "Server", Package = "net/http/httptest")]
    public class GoServer
    {
        private readonly object _handler;
        private HttpListener? _listener;
        private volatile bool _closed;

        [GoField(Name = "URL")]
        public string URL = "";

        [GoField(Name = "Listener")]
        public object? Listener;

        [GoField(Name = "Config")]
        public object? Config;

        public GoServer(object handler)
        {
            _handler = handler;
        }

        internal void Start()
        {
            // Find a free port
            var tempListener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            tempListener.Start();
            int port = ((IPEndPoint)tempListener.LocalEndpoint).Port;
            tempListener.Stop();

            URL = $"http://127.0.0.1:{port}";

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (!_closed)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = _listener.GetContext();
                    }
                    catch
                    {
                        break;
                    }

                    ThreadPool.QueueUserWorkItem(__ =>
                    {
                        try
                        {
                            HandleRequest(context);
                        }
                        catch
                        {
                            // Ignore errors in test server
                        }
                    });
                }
            });
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = new Request
            {
                Method = context.Request.HttpMethod,
                URLPath = context.Request.Url?.AbsolutePath ?? "/",
                Host = context.Request.UserHostName ?? "",
                RemoteAddr = context.Request.RemoteEndPoint?.ToString() ?? "",
                RequestURI = context.Request.RawUrl ?? "/",
                Header = new Header(),
            };

            if (context.Request.Url != null)
            {
                var (url, _) = Url.Package.Parse(context.Request.Url.ToString());
                request.URL = url;
            }

            // Copy request headers
            foreach (string key in context.Request.Headers.AllKeys)
            {
                if (key != null)
                {
                    var values = context.Request.Headers.GetValues(key);
                    if (values != null)
                    {
                        foreach (var val in values)
                        {
                            request.Header.Add(key, val);
                        }
                    }
                }
            }

            if (context.Request.HasEntityBody)
            {
                request.Body = new StreamReaderAdapter(context.Request.InputStream);
            }

            var responseWriter = new ResponseWriter
            {
                ListenerResponse = context.Response,
            };

            if (_handler is IHandler h)
            {
                h.ServeHTTP(responseWriter, request);
            }
            else if (_handler is ServeMux mux)
            {
                mux.ServeHTTP(responseWriter, request);
            }

            responseWriter.Flush();
        }

        [GoMethod]
        public void Close()
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
        }

        [GoMethod]
        public void CloseClientConnections()
        {
            // No-op: HttpListener doesn't track individual connections
        }

        [GoMethod]
        [return: GoReturn("*http.Client")]
        public Client Client()
        {
            return new Client();
        }
    }

    [GoType("struct", Name = "ResponseRecorder", Package = "net/http/httptest")]
    public class GoResponseRecorder : IResponseWriter
    {
        private readonly Header _header = new Header();
        private readonly Bytes.Buffer _body = new Bytes.Buffer();
        private bool _headerWritten;

        [GoField(Name = "Code")]
        public long Code = 200;

        [GoField(Name = "HeaderMap")]
        public Header HeaderMap;

        [GoField(Name = "Body")]
        public object Body;

        [GoField(Name = "Flushed")]
        public bool Flushed;

        public GoResponseRecorder()
        {
            HeaderMap = _header;
            Body = _body;
        }

        [GoMethod]
        public Header Header() => _header;

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Write(Slice<byte> p)
        {
            if (!_headerWritten)
            {
                WriteHeader(200);
            }
            return _body.Write(p);
        }

        [GoMethod]
        public void WriteHeader(long statusCode)
        {
            if (_headerWritten)
            {
                return;
            }
            Code = statusCode;
            _headerWritten = true;
        }

        [GoMethod]
        public void Flush()
        {
            Flushed = true;
        }

        [GoMethod]
        [return: GoReturn("*http.Response")]
        public Response Result()
        {
            var response = new Response
            {
                StatusCode = Code,
                Header = _header,
            };
            return response;
        }
    }
}

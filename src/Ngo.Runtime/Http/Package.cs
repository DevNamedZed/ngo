using System;
using System.IO;
using System.Net.Http;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Http
{
    [GoPackage("net/http")]
    public static class Package
    {
        private static readonly HttpClient _client = new HttpClient();

        [GoFunc]
        [return: GoReturn("*Response", "error")]
        public static (Response, object?) Get(string url)
        {
            try
            {
                var response = _client.GetAsync(url).GetAwaiter().GetResult();
                return (new Response(response), null);
            }
            catch (Exception ex)
            {
                return (null!, ex.Message);
            }
        }

        [GoFunc]
        [return: GoReturn("*Response", "error")]
        public static (Response, object?) Post(string url, string contentType, object? body)
        {
            try
            {
                HttpContent content;
                if (body is IGoReader reader)
                {
                    var ms = new MemoryStream();
                    var buf = new byte[4096];
                    while (true)
                    {
                        var slice = new Slice<byte>(buf);
                        var (n, err) = reader.Read(slice);
                        if (n > 0)
                            ms.Write(buf, 0, (int)n);
                        if (err != null)
                            break;
                    }
                    ms.Position = 0;
                    content = new StreamContent(ms);
                }
                else
                {
                    content = new StringContent("");
                }
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

                var response = _client.PostAsync(url, content).GetAwaiter().GetResult();
                return (new Response(response), null);
            }
            catch (Exception ex)
            {
                return (null!, ex.Message);
            }
        }

        [GoFunc]
        public static string DetectContentType(Slice<byte> data)
        {
            if (data.Len >= 4)
            {
                if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                    return "image/png";
                if (data[0] == 0xFF && data[1] == 0xD8)
                    return "image/jpeg";
                if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
                    return "image/gif";
                if (data[0] == '{' || data[0] == '[')
                    return "application/json";
                if (data[0] == '<')
                    return "text/html; charset=utf-8";
                if (data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46)
                    return "application/pdf";
            }
            for (int i = 0; i < data.Len && i < 512; i++)
            {
                if (data[i] < 0x20 && data[i] != 0x09 && data[i] != 0x0A && data[i] != 0x0D)
                    return "application/octet-stream";
            }
            return "text/plain; charset=utf-8";
        }

        // Status code constants
        [GoConst] public static readonly long StatusContinue = 100;
        [GoConst] public static readonly long StatusSwitchingProtocols = 101;
        [GoConst] public static readonly long StatusOK = 200;
        [GoConst] public static readonly long StatusCreated = 201;
        [GoConst] public static readonly long StatusAccepted = 202;
        [GoConst] public static readonly long StatusNoContent = 204;
        [GoConst] public static readonly long StatusMovedPermanently = 301;
        [GoConst] public static readonly long StatusFound = 302;
        [GoConst] public static readonly long StatusSeeOther = 303;
        [GoConst] public static readonly long StatusNotModified = 304;
        [GoConst] public static readonly long StatusTemporaryRedirect = 307;
        [GoConst] public static readonly long StatusPermanentRedirect = 308;
        [GoConst] public static readonly long StatusBadRequest = 400;
        [GoConst] public static readonly long StatusUnauthorized = 401;
        [GoConst] public static readonly long StatusPaymentRequired = 402;
        [GoConst] public static readonly long StatusForbidden = 403;
        [GoConst] public static readonly long StatusNotFound = 404;
        [GoConst] public static readonly long StatusMethodNotAllowed = 405;
        [GoConst] public static readonly long StatusRequestTimeout = 408;
        [GoConst] public static readonly long StatusConflict = 409;
        [GoConst] public static readonly long StatusGone = 410;
        [GoConst] public static readonly long StatusTeapot = 418;
        [GoConst] public static readonly long StatusTooManyRequests = 429;
        [GoConst] public static readonly long StatusInternalServerError = 500;
        [GoConst] public static readonly long StatusNotImplemented = 501;
        [GoConst] public static readonly long StatusBadGateway = 502;
        [GoConst] public static readonly long StatusServiceUnavailable = 503;
        [GoConst] public static readonly long StatusGatewayTimeout = 504;

        [GoFunc]
        public static string StatusText(long code)
        {
            return code switch
            {
                200 => "OK", 201 => "Created", 204 => "No Content",
                301 => "Moved Permanently", 302 => "Found", 304 => "Not Modified",
                400 => "Bad Request", 401 => "Unauthorized", 403 => "Forbidden",
                404 => "Not Found", 405 => "Method Not Allowed", 500 => "Internal Server Error",
                _ => ""
            };
        }

        [GoFunc]
        public static void Handle(string pattern, object handler) { }

        [GoFunc]
        public static void HandleFunc(string pattern, Action<ResponseWriter, Request> handler) { }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? ListenAndServe(string addr, object? handler) => null;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? ListenAndServeTLS(string addr, string certFile, string keyFile, object? handler) => null;

        [GoFunc]
        [return: GoReturn("*Request", "error")]
        public static (Request, object?) NewRequest(string method, string url, object? body)
        {
            return (new Request { Method = method, URLPath = url }, null);
        }

        [GoFunc]
        public static void Error(ResponseWriter w, string error, long code) { }

        [GoFunc]
        public static void Redirect(ResponseWriter w, Request r, string url, long code) { }

        [GoFunc]
        public static void NotFound(ResponseWriter w, Request r) { }

        [GoFunc]
        [return: GoReturn("Handler")]
        public static object NotFoundHandler() => new object();

        [GoFunc]
        public static object MaxBytesReader(object w, object r, long n) => r;

        [GoFunc]
        public static ServeMux NewServeMux() => new ServeMux();

        [GoFunc]
        [return: GoReturn("*url.URL", "error")]
        public static (object?, object?) ProxyFromEnvironment(Request req) => (null, null);

        [GoFunc]
        public static string CanonicalHeaderKey(string s) => s;

        [GoFunc]
        public static void SetCookie(ResponseWriter w, Cookie cookie) { }

        [GoVar] public static ServeMux DefaultServeMux = new ServeMux();
        [GoConst] public static readonly string TimeFormat = "Mon, 02 Jan 2006 15:04:05 GMT";
        [GoConst] public static readonly string TrailerPrefix = "Trailer:";

        [GoConst] public static readonly string MethodGet = "GET";
        [GoConst] public static readonly string MethodHead = "HEAD";
        [GoConst] public static readonly string MethodPost = "POST";
        [GoConst] public static readonly string MethodPut = "PUT";
        [GoConst] public static readonly string MethodPatch = "PATCH";
        [GoConst] public static readonly string MethodDelete = "DELETE";
        [GoConst] public static readonly string MethodConnect = "CONNECT";
        [GoConst] public static readonly string MethodOptions = "OPTIONS";
        [GoConst] public static readonly string MethodTrace = "TRACE";

        [GoVar(Type = "RoundTripper")] public static Transport DefaultTransport = new Transport();
        [GoVar] public static Client DefaultClient = new Client();

        [GoVar] public static readonly object? ErrBodyNotAllowed = "http: request method or response status code does not allow body";
        [GoVar] public static readonly object? ErrHijacked = "http: connection has been hijacked";
        [GoVar] public static readonly object? ErrContentLength = "http: wrote more than the declared Content-Length";
        [GoVar] public static readonly object? ErrAbortHandler = "net/http: abort Handler";
        [GoVar] public static readonly object? ErrServerClosed = "http: Server closed";
        [GoVar] public static readonly object? ErrHandlerTimeout = "http: Handler timeout";
        [GoVar] public static readonly object? ErrLineTooLong = "header line too long";
        [GoVar] public static readonly object? ErrMissingFile = "http: no such file";
        [GoVar] public static readonly object? ErrNoCookie = "http: named cookie not present";
        [GoVar] public static readonly object? ErrNoLocation = "http: no Location header in response";
        [GoVar] public static readonly object? ErrNotSupported = "feature not supported";
        [GoVar] public static readonly long DefaultMaxHeaderBytes = 1 << 20;
        [GoVar] public static readonly long DefaultMaxIdleConnsPerHost = 2;

        [GoConst] public static readonly long StateNew = 0;
        [GoConst] public static readonly long StateActive = 1;
        [GoConst] public static readonly long StateIdle = 2;
        [GoConst] public static readonly long StateHijacked = 3;
        [GoConst] public static readonly long StateClosed = 4;

        [GoFunc]
        public static object FileServer(object root) => new object();

        [GoFunc]
        public static object StripPrefix(string prefix, object h) => h;

        [GoFunc]
        public static object TimeoutHandler(object h, long dt, string msg) => h;

        [GoFunc]
        public static object AllowQuerySemicolons(object h) => h;

        [GoVar] public static readonly object? NoBody = null;

        [GoFunc]
        [return: GoReturn("*Request", "error")]
        public static (Request, object?) ReadRequest(object? b) => (new Request(), null);

        [GoFunc]
        [return: GoReturn("*Response", "error")]
        public static (Response, object?) ReadResponse(object? r, Request req) => (new Response(), null);

        [GoFunc]
        [return: GoReturn("*ResponseController")]
        public static ResponseController NewResponseController(ResponseWriter rw) => new ResponseController(rw);

        [GoFunc]
        [return: GoReturn("int", "int", "bool")]
        public static (long, long, bool) ParseHTTPVersion(string vers)
        {
            if (vers == "HTTP/1.0") return (1, 0, true);
            if (vers == "HTTP/1.1") return (1, 1, true);
            if (vers == "HTTP/2.0") return (2, 0, true);
            return (0, 0, false);
        }

        [GoVar] public static readonly object? ServerContextKey = new object();
        [GoVar] public static readonly object? LocalAddrContextKey = new object();

        [GoConst] public static readonly long SameSiteDefaultMode = 1;
        [GoConst] public static readonly long SameSiteStrictMode = 2;
        [GoConst] public static readonly long SameSiteLaxMode = 3;
        [GoConst] public static readonly long SameSiteNoneMode = 4;

        // Missing status codes
        [GoConst] public static readonly long StatusMultipleChoices = 300;
        [GoConst] public static readonly long StatusPartialContent = 206;
        [GoConst] public static readonly long StatusUnsupportedMediaType = 415;

        // NewRequestWithContext(ctx context.Context, method, url string, body io.Reader) (*Request, error)
        [GoFunc]
        [return: GoReturn("*Request", "error")]
        public static (Request, object?) NewRequestWithContext(object? ctx, string method, string url, object? body)
        {
            return (new Request { Method = method, URLPath = url }, null);
        }

        // RedirectHandler(url string, code int) Handler
        [GoFunc]
        [return: GoReturn("Handler")]
        public static object RedirectHandler(string url, long code) => new object();

        // ErrUseLastResponse
        [GoVar] public static readonly object? ErrUseLastResponse = "net/http: use last response";
    }

    // http.PushOptions struct
    [GoType("struct", Name = "PushOptions", Package = "net/http")]
    public class PushOptions
    {
        [GoField(Name = "Method")] public string Method { get; set; } = "";
        [GoField(Name = "Header")] public Header Header { get; set; } = new Header();
    }
}

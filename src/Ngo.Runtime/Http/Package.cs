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
        [GoConst] public static readonly long StatusNotAcceptable = 406;
        [GoConst] public static readonly long StatusProxyAuthRequired = 407;
        [GoConst] public static readonly long StatusRequestTimeout = 408;
        [GoConst] public static readonly long StatusConflict = 409;
        [GoConst] public static readonly long StatusGone = 410;
        [GoConst] public static readonly long StatusLengthRequired = 411;
        [GoConst] public static readonly long StatusPreconditionFailed = 412;
        [GoConst] public static readonly long StatusRequestEntityTooLarge = 413;
        [GoConst] public static readonly long StatusRequestURITooLong = 414;
        [GoConst] public static readonly long StatusRequestedRangeNotSatisfiable = 416;
        [GoConst] public static readonly long StatusExpectationFailed = 417;
        [GoConst] public static readonly long StatusTeapot = 418;
        [GoConst] public static readonly long StatusMisdirectedRequest = 421;
        [GoConst] public static readonly long StatusUnprocessableEntity = 422;
        [GoConst] public static readonly long StatusLocked = 423;
        [GoConst] public static readonly long StatusFailedDependency = 424;
        [GoConst] public static readonly long StatusUpgradeRequired = 426;
        [GoConst] public static readonly long StatusPreconditionRequired = 428;
        [GoConst] public static readonly long StatusTooManyRequests = 429;
        [GoConst] public static readonly long StatusRequestHeaderFieldsTooLarge = 431;
        [GoConst] public static readonly long StatusUnavailableForLegalReasons = 451;
        [GoConst] public static readonly long StatusInternalServerError = 500;
        [GoConst] public static readonly long StatusNotImplemented = 501;
        [GoConst] public static readonly long StatusBadGateway = 502;
        [GoConst] public static readonly long StatusServiceUnavailable = 503;
        [GoConst] public static readonly long StatusGatewayTimeout = 504;
        [GoConst] public static readonly long StatusHTTPVersionNotSupported = 505;
        [GoConst] public static readonly long StatusVariantAlsoNegotiates = 506;
        [GoConst] public static readonly long StatusInsufficientStorage = 507;
        [GoConst] public static readonly long StatusLoopDetected = 508;
        [GoConst] public static readonly long StatusNotExtended = 510;
        [GoConst] public static readonly long StatusNetworkAuthenticationRequired = 511;
        [GoConst] public static readonly long StatusTooEarly = 425;

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
        public static void Handle(string pattern, object handler)
        {
            DefaultServeMux.Handle(pattern, handler);
        }

        [GoFunc]
        public static void HandleFunc(string pattern, Action<ResponseWriter, Request> handler)
        {
            DefaultServeMux.HandleFunc(pattern, handler);
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? ListenAndServe(string addr, object? handler)
        {
            var server = new Server
            {
                Addr = addr,
                Handler = handler,
            };
            return server.ListenAndServe();
        }

        [GoFunc]
        [return: GoReturn("error")]
        public static object? ListenAndServeTLS(string addr, string certFile, string keyFile, object? handler)
        {
            var server = new Server
            {
                Addr = addr,
                Handler = handler,
            };
            return server.ListenAndServeTLS(certFile, keyFile);
        }

        [GoFunc]
        [return: GoReturn("*Request", "error")]
        public static (Request, object?) NewRequest(string method, string url, object? body)
        {
            var request = new Request
            {
                Method = method,
                URLPath = url,
                Body = body,
                Header = new Header(),
                Proto = "HTTP/1.1",
                ProtoMajor = 1,
                ProtoMinor = 1,
            };

            var (parsedUrl, _) = Url.Package.Parse(url);
            if (parsedUrl != null)
            {
                request.URL = parsedUrl;
                request.Host = parsedUrl.Host;
            }

            return (request, null);
        }

        [GoFunc]
        public static void Error(ResponseWriter w, string error, long code)
        {
            w.Header().Set("Content-Type", "text/plain; charset=utf-8");
            w.Header().Set("X-Content-Type-Options", "nosniff");
            w.WriteHeader(code);
            var bytes = System.Text.Encoding.UTF8.GetBytes(error + "\n");
            w.Write(new Slice<byte>(bytes));
        }

        [GoFunc]
        public static void Redirect(ResponseWriter w, Request r, string url, long code)
        {
            w.Header().Set("Location", url);
            if (code < 300 || code > 399)
            {
                code = 302;
            }
            w.WriteHeader(code);
        }

        [GoFunc]
        public static void NotFound(ResponseWriter w, Request r)
        {
            Error(w, "404 page not found", 404);
        }

        [GoFunc]
        [return: GoReturn("Handler")]
        public static object NotFoundHandler()
        {
            return new HandlerFuncWrapper((w, r) => NotFound(w, r));
        }

        [GoFunc]
        public static object MaxBytesReader(object w, object r, long n) => r;

        [GoFunc]
        public static ServeMux NewServeMux() => new ServeMux();

        [GoFunc]
        [return: GoReturn("*url.URL", "error")]
        public static (object?, object?) ProxyFromEnvironment(Request req) => (null, null);

        // http.ProxyURL(fixedURL *url.URL) func(*Request) (*url.URL, error)
        [GoFunc]
        [return: GoReturn("func(*Request) (*url.URL, error)")]
        public static object? ProxyURL(object? fixedURL) => null;

        [GoFunc]
        public static string CanonicalHeaderKey(string s) => Net.Textproto.Package.CanonicalMIMEHeaderKey(s);

        [GoFunc]
        public static void SetCookie(ResponseWriter w, Cookie cookie)
        {
            if (cookie != null && !string.IsNullOrEmpty(cookie.Name))
            {
                w.Header().Add("Set-Cookie", cookie.String());
            }
        }

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
        [GoVar] public static readonly object ErrNotMultipart = "request Content-Type isn't multipart/form-data";
        [GoVar] public static readonly long DefaultMaxHeaderBytes = 1 << 20;
        [GoVar] public static readonly long DefaultMaxIdleConnsPerHost = 2;

        [GoConst] public static readonly long StateNew = 0;
        [GoConst] public static readonly long StateActive = 1;
        [GoConst] public static readonly long StateIdle = 2;
        [GoConst] public static readonly long StateHijacked = 3;
        [GoConst] public static readonly long StateClosed = 4;


        [GoFunc]
        public static void ServeContent(object w, object r, string name, object modtime, object content)
        {
            if (w is ResponseWriter rw && content is Io.IGoReader reader)
            {
                string contentType = DetectContentTypeFromName(name);
                rw.Header().Set("Content-Type", contentType);
                var buf = new byte[32768];
                while (true)
                {
                    var slice = new Slice<byte>(buf);
                    var (n, err) = reader.Read(slice);
                    if (n > 0)
                    {
                        rw.Write(new Slice<byte>(buf, 0, n));
                    }
                    if (err != null)
                    {
                        break;
                    }
                }
            }
        }

        [GoFunc]
        public static void ServeFile(object w, object r, string name)
        {
            if (w is ResponseWriter rw)
            {
                try
                {
                    if (!System.IO.File.Exists(name))
                    {
                        NotFound(rw, r as Request ?? new Request());
                        return;
                    }
                    var bytes = System.IO.File.ReadAllBytes(name);
                    string contentType = DetectContentTypeFromName(name);
                    rw.Header().Set("Content-Type", contentType);
                    rw.Write(new Slice<byte>(bytes));
                }
                catch
                {
                    Error(rw, "500 Internal Server Error", 500);
                }
            }
        }

        [GoFunc]
        public static object FileServer(object root)
        {
            string dir = root as string ?? ".";
            return new FileServerHandler(dir);
        }

        [GoFunc]
        public static object StripPrefix(string prefix, object h)
        {
            return new StripPrefixHandler(prefix, h);
        }

        private static string DetectContentTypeFromName(string name)
        {
            string ext = System.IO.Path.GetExtension(name);
            string mimeType = Mime.Package.TypeByExtension(ext);
            if (!string.IsNullOrEmpty(mimeType))
            {
                return mimeType;
            }
            return "application/octet-stream";
        }

        [GoFunc]
        public static object TimeoutHandler(object h, long dt, string msg) => h;

        [GoFunc]
        public static object AllowQuerySemicolons(object h) => h;

        [GoFunc]
        [return: GoReturn("error")]
        public static object? Serve(object? listener, object? handler) => "not implemented";

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
            return NewRequest(method, url, body);
        }

        // RedirectHandler(url string, code int) Handler
        [GoFunc]
        [return: GoReturn("Handler")]
        public static object RedirectHandler(string url, long code)
        {
            return new RedirectHandlerImpl(url, code);
        }

        // ErrUseLastResponse
        [GoVar] public static readonly object? ErrUseLastResponse = "net/http: use last response";

        [GoFunc]
        [return: GoReturn("time.Time", "error")]
        public static (object, object?) ParseTime(string text)
        {
            return (new Time.GoTimeValue(DateTimeOffset.MinValue), null);
        }
    }

    // http.PushOptions struct
    [GoType("struct", Name = "PushOptions", Package = "net/http")]
    public class PushOptions
    {
        [GoField(Name = "Method")] public string Method { get; set; } = "";
        [GoField(Name = "Header")] public Header Header { get; set; } = new Header();
    }

    internal class RedirectHandlerImpl : IHandler
    {
        private readonly string _url;
        private readonly long _code;

        public RedirectHandlerImpl(string url, long code)
        {
            _url = url;
            _code = code;
        }

        public void ServeHTTP(ResponseWriter w, Request r)
        {
            Package.Redirect(w, r, _url, _code);
        }
    }

    internal class FileServerHandler : IHandler
    {
        private readonly string _root;

        public FileServerHandler(string root)
        {
            _root = root;
        }

        public void ServeHTTP(ResponseWriter w, Request r)
        {
            string urlPath = r.URLPath;
            if (r.URL is Url.GoURL goUrl)
            {
                urlPath = goUrl.Path;
            }
            if (string.IsNullOrEmpty(urlPath))
            {
                urlPath = "/";
            }

            // Clean path and resolve against root
            string filePath = urlPath.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar);
            string fullPath = System.IO.Path.Combine(_root, filePath);

            // Directory listing
            if (System.IO.Directory.Exists(fullPath))
            {
                string indexPath = System.IO.Path.Combine(fullPath, "index.html");
                if (System.IO.File.Exists(indexPath))
                {
                    fullPath = indexPath;
                }
                else
                {
                    Package.NotFound(w, r);
                    return;
                }
            }

            if (!System.IO.File.Exists(fullPath))
            {
                Package.NotFound(w, r);
                return;
            }

            Package.ServeFile(w, r, fullPath);
        }
    }

    internal class StripPrefixHandler : IHandler
    {
        private readonly string _prefix;
        private readonly object _handler;

        public StripPrefixHandler(string prefix, object handler)
        {
            _prefix = prefix;
            _handler = handler;
        }

        public void ServeHTTP(ResponseWriter w, Request r)
        {
            string path = r.URLPath;
            if (r.URL is Url.GoURL goUrl)
            {
                path = goUrl.Path;
            }

            if (!string.IsNullOrEmpty(path) && path.StartsWith(_prefix))
            {
                var newRequest = r.Clone(null);
                string newPath = path.Substring(_prefix.Length);
                if (!newPath.StartsWith("/"))
                {
                    newPath = "/" + newPath;
                }
                newRequest.URLPath = newPath;
                if (newRequest.URL is Url.GoURL newUrl)
                {
                    newUrl.Path = newPath;
                }
                r = newRequest;
            }

            if (_handler is IHandler h)
            {
                h.ServeHTTP(w, r);
            }
            else if (_handler is ServeMux mux)
            {
                mux.ServeHTTP(w, r);
            }
        }
    }
}

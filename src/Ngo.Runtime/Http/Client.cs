using System;
using System.IO;
using System.Net.Http;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "Client", Package = "net/http")]
    public class Client
    {
        private static readonly HttpClient _sharedClient = new HttpClient();

        [GoField(Name = "Transport", Type = "RoundTripper")] public object? Transport { get; set; }
        [GoField(Name = "Timeout")] public long Timeout { get; set; }
        [GoField(Name = "Jar")] public object? Jar { get; set; }
        [GoField(Name = "CheckRedirect")] public object? CheckRedirect { get; set; }

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) Get(string url) => Package.Get(url);

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) Do(Request req)
        {
            try
            {
                string url = req.URLPath;
                if (req.URL is Url.GoURL goUrl)
                {
                    url = goUrl.String();
                }
                if (string.IsNullOrEmpty(url))
                {
                    url = req.RequestURI;
                }

                var httpMethod = new HttpMethod(req.Method ?? "GET");
                var httpReq = new HttpRequestMessage(httpMethod, url);

                // Copy headers
                foreach (var kv in req.Header._values)
                {
                    string key = kv.Key;
                    for (int i = 0; i < kv.Value.Len; i++)
                    {
                        httpReq.Headers.TryAddWithoutValidation(key, kv.Value[i]);
                    }
                }

                // Set body if present
                if (req.Body is IGoReader reader)
                {
                    var ms = new MemoryStream();
                    var buf = new byte[4096];
                    while (true)
                    {
                        var slice = new Slice<byte>(buf);
                        var (n, err) = reader.Read(slice);
                        if (n > 0)
                        {
                            ms.Write(buf, 0, n);
                        }
                        if (err != null)
                        {
                            break;
                        }
                    }
                    ms.Position = 0;
                    httpReq.Content = new StreamContent(ms);

                    // Copy content-type from headers
                    string ct = req.Header.Get("Content-Type");
                    if (!string.IsNullOrEmpty(ct))
                    {
                        httpReq.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ct);
                    }
                }

                if (!string.IsNullOrEmpty(req.Host))
                {
                    httpReq.Headers.Host = req.Host;
                }

                var response = _sharedClient.SendAsync(httpReq).GetAwaiter().GetResult();
                return (new Response(response), null);
            }
            catch (Exception ex)
            {
                return (null!, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) Post(string url, string contentType, object? body) => Package.Post(url, contentType, body);

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) Head(string url)
        {
            try
            {
                var httpReq = new HttpRequestMessage(HttpMethod.Head, url);
                var response = _sharedClient.SendAsync(httpReq).GetAwaiter().GetResult();
                return (new Response(response), null);
            }
            catch (Exception ex)
            {
                return (null!, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("*Response", "error")]
        public (Response, object?) PostForm(string url, object? data)
        {
            // data should be url.Values
            string formBody = "";
            if (data is Url.GoValues values)
            {
                formBody = values.Encode();
            }
            var content = new StringContent(formBody, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
            try
            {
                var response = _sharedClient.PostAsync(url, content).GetAwaiter().GetResult();
                return (new Response(response), null);
            }
            catch (Exception ex)
            {
                return (null!, ex.Message);
            }
        }

        [GoMethod]
        public void CloseIdleConnections() { }
    }
}

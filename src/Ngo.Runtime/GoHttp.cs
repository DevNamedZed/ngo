// -----------------------------------------------------------------------
// <copyright file="GoHttp.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ngo.Runtime
{
    public static class GoHttp
    {
        private static readonly HttpClient _client = new HttpClient();

        // http.Get(url string) (*Response, error)
        public static (GoHttpResponse, object?) Get(string url)
        {
            try
            {
                var response = _client.GetAsync(url).GetAwaiter().GetResult();
                return (new GoHttpResponse(response), null);
            }
            catch (Exception ex)
            {
                return (null!, ex.Message);
            }
        }

        // http.Post(url, contentType string, body io.Reader) (*Response, error)
        public static (GoHttpResponse, object?) Post(string url, string contentType, object? body)
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
                return (new GoHttpResponse(response), null);
            }
            catch (Exception ex)
            {
                return (null!, ex.Message);
            }
        }

        // http.StatusOK etc. — common status codes
        public static readonly long StatusOK = 200;
        public static readonly long StatusCreated = 201;
        public static readonly long StatusBadRequest = 400;
        public static readonly long StatusUnauthorized = 401;
        public static readonly long StatusForbidden = 403;
        public static readonly long StatusNotFound = 404;
        public static readonly long StatusInternalServerError = 500;
    }

    public class GoHttpResponse : IGoReader
    {
        private readonly HttpResponseMessage _response;
        private Stream? _bodyStream;

        public GoHttpResponse(HttpResponseMessage response)
        {
            _response = response;
            StatusCode = (long)response.StatusCode;
            Status = $"{(int)response.StatusCode} {response.ReasonPhrase}";
        }

        public long StatusCode { get; }
        public string Status { get; }

        public GoHttpResponseBody Body => new GoHttpResponseBody(_response);

        public (int, string) Read(Slice<byte> p)
        {
            _bodyStream ??= _response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            var buf = new byte[p.Len];
            var n = _bodyStream.Read(buf, 0, buf.Length);
            for (int i = 0; i < n; i++)
                p[i] = buf[i];
            if (n == 0)
                return (0, "EOF");
            return (n, "");
        }
    }

    public class GoHttpResponseBody : IGoReader
    {
        private readonly HttpResponseMessage _response;
        private Stream? _stream;

        public GoHttpResponseBody(HttpResponseMessage response)
        {
            _response = response;
        }

        public (int, string) Read(Slice<byte> p)
        {
            _stream ??= _response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            var buf = new byte[p.Len];
            var n = _stream.Read(buf, 0, buf.Length);
            for (int i = 0; i < n; i++)
                p[i] = buf[i];
            if (n == 0)
                return (0, "EOF");
            return (n, "");
        }

        public void Close()
        {
            _stream?.Dispose();
            _response.Dispose();
        }
    }
}

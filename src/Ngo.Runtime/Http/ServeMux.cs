using System;
using System.Collections.Generic;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "ServeMux", Package = "net/http")]
    public class ServeMux : IHandler
    {
        private readonly List<MuxEntry> _entries = new List<MuxEntry>();
        private readonly object _lock = new object();

        [GoMethod]
        public void Handle(string pattern, object handler)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                throw new GoPanicException("http: invalid pattern");
            }

            lock (_lock)
            {
                _entries.Add(new MuxEntry
                {
                    Pattern = pattern,
                    Handler = handler,
                });
            }
        }

        [GoMethod]
        public void HandleFunc(string pattern, Action<ResponseWriter, Request> handler)
        {
            Handle(pattern, new HandlerFuncWrapper(handler));
        }

        [GoMethod]
        public void ServeHTTP(ResponseWriter w, Request r)
        {
            var (handler, _) = Handler(r);
            if (handler == null)
            {
                Package.NotFound(w, r);
                return;
            }

            if (handler is IHandler h)
            {
                h.ServeHTTP(w, r);
            }
            else if (handler is HandlerFuncWrapper wrapper)
            {
                wrapper.ServeHTTP(w, r);
            }
        }

        [GoMethod]
        public (object?, string) Handler(Request r)
        {
            string path = r.URLPath;
            if (r.URL is Url.GoURL goUrl)
            {
                path = goUrl.Path;
            }
            if (string.IsNullOrEmpty(path))
            {
                path = "/";
            }

            lock (_lock)
            {
                // First try exact match
                foreach (var entry in _entries)
                {
                    if (entry.Pattern == path)
                    {
                        return (entry.Handler, entry.Pattern);
                    }
                }

                // Then try prefix match (patterns ending with /)
                int bestLength = 0;
                MuxEntry? bestEntry = null;
                foreach (var entry in _entries)
                {
                    if (entry.Pattern.EndsWith("/") && path.StartsWith(entry.Pattern) && entry.Pattern.Length > bestLength)
                    {
                        bestLength = entry.Pattern.Length;
                        bestEntry = entry;
                    }
                }

                if (bestEntry != null)
                {
                    return (bestEntry.Handler, bestEntry.Pattern);
                }
            }

            return (null, "");
        }

        private class MuxEntry
        {
            public string Pattern = "";
            public object Handler = null!;
        }
    }

    internal class HandlerFuncWrapper : IHandler
    {
        private readonly Action<ResponseWriter, Request> _handler;

        public HandlerFuncWrapper(Action<ResponseWriter, Request> handler)
        {
            _handler = handler;
        }

        public void ServeHTTP(ResponseWriter w, Request r)
        {
            _handler(w, r);
        }
    }
}

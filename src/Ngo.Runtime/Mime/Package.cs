using System;
using System.Collections.Generic;
using System.Text;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Mime
{
    [GoPackage("mime")]
    public static class Package
    {
        private static readonly Dictionary<string, string> _mimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".html", "text/html" },
            { ".htm", "text/html" },
            { ".css", "text/css" },
            { ".js", "application/javascript" },
            { ".json", "application/json" },
            { ".xml", "application/xml" },
            { ".txt", "text/plain" },
            { ".csv", "text/csv" },
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".svg", "image/svg+xml" },
            { ".ico", "image/x-icon" },
            { ".webp", "image/webp" },
            { ".bmp", "image/bmp" },
            { ".tiff", "image/tiff" },
            { ".tif", "image/tiff" },
            { ".mp3", "audio/mpeg" },
            { ".wav", "audio/wav" },
            { ".ogg", "audio/ogg" },
            { ".mp4", "video/mp4" },
            { ".webm", "video/webm" },
            { ".avi", "video/x-msvideo" },
            { ".pdf", "application/pdf" },
            { ".zip", "application/zip" },
            { ".gz", "application/gzip" },
            { ".tar", "application/x-tar" },
            { ".wasm", "application/wasm" },
            { ".woff", "font/woff" },
            { ".woff2", "font/woff2" },
            { ".ttf", "font/ttf" },
            { ".otf", "font/otf" },
            { ".eot", "application/vnd.ms-fontobject" },
            { ".doc", "application/msword" },
            { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            { ".xls", "application/vnd.ms-excel" },
            { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            { ".ppt", "application/vnd.ms-powerpoint" },
            { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
            { ".yaml", "application/x-yaml" },
            { ".yml", "application/x-yaml" },
            { ".toml", "application/toml" },
            { ".md", "text/markdown" },
            { ".go", "text/x-go" },
            { ".rs", "text/x-rust" },
            { ".py", "text/x-python" },
            { ".rb", "text/x-ruby" },
            { ".java", "text/x-java" },
            { ".c", "text/x-c" },
            { ".h", "text/x-c" },
            { ".cpp", "text/x-c++src" },
            { ".cs", "text/x-csharp" },
            { ".sh", "application/x-sh" },
            { ".bat", "application/x-msdos-program" },
        };

        private static readonly Dictionary<string, List<string>> _typeToExts = BuildReverseMap();

        private static Dictionary<string, List<string>> BuildReverseMap()
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _mimeTypes)
            {
                if (!map.TryGetValue(kv.Value, out var list))
                {
                    list = new List<string>();
                    map[kv.Value] = list;
                }
                list.Add(kv.Key);
            }
            return map;
        }

        // mime.TypeByExtension(ext string) string
        [GoFunc]
        public static string TypeByExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext))
            {
                return "";
            }
            if (!ext.StartsWith("."))
            {
                ext = "." + ext;
            }
            if (_mimeTypes.TryGetValue(ext, out var mimeType))
            {
                return mimeType;
            }
            return "";
        }

        // mime.ExtensionsByType(typ string) ([]string, error)
        [GoFunc]
        [return: GoReturn("[]string", "error")]
        public static (Slice<string>, object?) ExtensionsByType(string typ)
        {
            if (string.IsNullOrEmpty(typ))
            {
                return (new Slice<string>(), null);
            }
            // Strip parameters (e.g., "text/html; charset=utf-8" -> "text/html")
            int semi = typ.IndexOf(';');
            if (semi >= 0)
            {
                typ = typ.Substring(0, semi).Trim();
            }
            if (_typeToExts.TryGetValue(typ, out var exts))
            {
                return (new Slice<string>(exts.ToArray()), null);
            }
            return (new Slice<string>(), null);
        }

        // mime.FormatMediaType(t string, param map[string]string) string
        [GoFunc]
        public static string FormatMediaType(string t, Map<string, string> param)
        {
            if (string.IsNullOrEmpty(t))
            {
                return "";
            }
            var sb = new StringBuilder();
            sb.Append(t);
            if (param != null)
            {
                foreach (var kv in param)
                {
                    sb.Append("; ");
                    sb.Append(kv.Key);
                    sb.Append('=');
                    // Quote the value if it contains special characters
                    if (NeedsQuoting(kv.Value))
                    {
                        sb.Append('"');
                        sb.Append(kv.Value.Replace("\\", "\\\\").Replace("\"", "\\\""));
                        sb.Append('"');
                    }
                    else
                    {
                        sb.Append(kv.Value);
                    }
                }
            }
            return sb.ToString();
        }

        // mime.ParseMediaType(v string) (mediatype string, params map[string]string, err error)
        [GoFunc]
        [return: GoReturn("string", "map[string]string", "error")]
        public static (string, Map<string, string>, object?) ParseMediaType(string v)
        {
            var parameters = new Map<string, string>();
            if (string.IsNullOrEmpty(v))
            {
                return ("", parameters, "mime: no media type");
            }

            int semi = v.IndexOf(';');
            string mediaType;
            if (semi >= 0)
            {
                mediaType = v.Substring(0, semi).Trim().ToLowerInvariant();
                var paramStr = v.Substring(semi + 1);
                ParseParameters(paramStr, parameters);
            }
            else
            {
                mediaType = v.Trim().ToLowerInvariant();
            }

            if (string.IsNullOrEmpty(mediaType))
            {
                return ("", parameters, "mime: no media type");
            }

            return (mediaType, parameters, null);
        }

        // mime.AddExtensionType(ext, typ string) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? AddExtensionType(string ext, string typ)
        {
            if (!ext.StartsWith("."))
            {
                ext = "." + ext;
            }
            _mimeTypes[ext] = typ;

            if (!_typeToExts.TryGetValue(typ, out var list))
            {
                list = new List<string>();
                _typeToExts[typ] = list;
            }
            if (!list.Contains(ext))
            {
                list.Add(ext);
            }
            return null;
        }

        // WordEncoder constants (type WordEncoder byte)
        [GoVar(Type = "mime.WordEncoder")]
        public static readonly GoWordEncoder BEncoding = new GoWordEncoder { Value = (byte)'b' };

        [GoVar(Type = "mime.WordEncoder")]
        public static readonly GoWordEncoder QEncoding = new GoWordEncoder { Value = (byte)'q' };

        private static bool NeedsQuoting(string s)
        {
            foreach (char c in s)
            {
                if (c < 0x20 || c >= 0x7f || c == '"' || c == '\\' || c == ';' || c == ' ')
                {
                    return true;
                }
            }
            return false;
        }

        private static void ParseParameters(string s, Map<string, string> parameters)
        {
            while (!string.IsNullOrEmpty(s))
            {
                s = s.Trim();
                if (string.IsNullOrEmpty(s))
                {
                    break;
                }

                int eq = s.IndexOf('=');
                if (eq < 0)
                {
                    break;
                }

                string key = s.Substring(0, eq).Trim().ToLowerInvariant();
                s = s.Substring(eq + 1).Trim();

                string value;
                if (s.Length > 0 && s[0] == '"')
                {
                    // Quoted value
                    int end = 1;
                    var sb = new StringBuilder();
                    while (end < s.Length)
                    {
                        if (s[end] == '\\' && end + 1 < s.Length)
                        {
                            sb.Append(s[end + 1]);
                            end += 2;
                        }
                        else if (s[end] == '"')
                        {
                            end++;
                            break;
                        }
                        else
                        {
                            sb.Append(s[end]);
                            end++;
                        }
                    }
                    value = sb.ToString();
                    s = end < s.Length ? s.Substring(end) : "";
                }
                else
                {
                    int semi = s.IndexOf(';');
                    if (semi >= 0)
                    {
                        value = s.Substring(0, semi).Trim();
                        s = s.Substring(semi + 1);
                    }
                    else
                    {
                        value = s.Trim();
                        s = "";
                    }
                }

                parameters.Set(key, value);

                // Skip semicolons
                if (s.Length > 0 && s[0] == ';')
                {
                    s = s.Substring(1);
                }
            }
        }
    }

    // mime.WordEncoder type (named byte)
    [GoType("named", Name = "WordEncoder", Package = "mime", Underlying = "byte")]
    public struct GoWordEncoder
    {
        public byte Value;

        [GoMethod]
        public string Encode(string charset, string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            // RFC 2047 encoding
            if (Value == (byte)'b')
            {
                // Base64 encoding
                var bytes = System.Text.Encoding.UTF8.GetBytes(s);
                var encoded = Convert.ToBase64String(bytes);
                return $"=?{charset}?B?{encoded}?=";
            }
            else
            {
                // Q encoding
                var sb = new StringBuilder();
                sb.Append($"=?{charset}?Q?");
                var bytes = System.Text.Encoding.UTF8.GetBytes(s);
                foreach (byte b in bytes)
                {
                    if (b == ' ')
                    {
                        sb.Append('_');
                    }
                    else if ((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9'))
                    {
                        sb.Append((char)b);
                    }
                    else
                    {
                        sb.Append('=');
                        sb.Append(b.ToString("X2"));
                    }
                }
                sb.Append("?=");
                return sb.ToString();
            }
        }
    }

    // mime.WordDecoder struct
    [GoType("struct", Name = "WordDecoder", Package = "mime")]
    public class GoWordDecoder
    {
        [GoField(Name = "CharsetReader", Type = "func(string, io.Reader) (io.Reader, error)")] public object? CharsetReader;

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, object?) Decode(string word)
        {
            if (string.IsNullOrEmpty(word) || !word.StartsWith("=?") || !word.EndsWith("?="))
            {
                return ("", "mime: invalid RFC 2047 encoded-word");
            }

            var inner = word.Substring(2, word.Length - 4);
            int firstQ = inner.IndexOf('?');
            if (firstQ < 0)
            {
                return ("", "mime: invalid RFC 2047 encoded-word");
            }
            int secondQ = inner.IndexOf('?', firstQ + 1);
            if (secondQ < 0)
            {
                return ("", "mime: invalid RFC 2047 encoded-word");
            }

            string encoding = inner.Substring(firstQ + 1, secondQ - firstQ - 1).ToUpperInvariant();
            string encoded = inner.Substring(secondQ + 1);

            if (encoding == "B")
            {
                var bytes = Convert.FromBase64String(encoded);
                return (System.Text.Encoding.UTF8.GetString(bytes), null);
            }
            else if (encoding == "Q")
            {
                var sb = new StringBuilder();
                for (int i = 0; i < encoded.Length; i++)
                {
                    if (encoded[i] == '_')
                    {
                        sb.Append(' ');
                    }
                    else if (encoded[i] == '=' && i + 2 < encoded.Length)
                    {
                        byte b = Convert.ToByte(encoded.Substring(i + 1, 2), 16);
                        sb.Append((char)b);
                        i += 2;
                    }
                    else
                    {
                        sb.Append(encoded[i]);
                    }
                }
                return (sb.ToString(), null);
            }

            return ("", "mime: unhandled encoding");
        }

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, object?) DecodeHeader(string header)
        {
            if (string.IsNullOrEmpty(header) || !header.Contains("=?"))
            {
                return (header, null);
            }

            var sb = new StringBuilder();
            int pos = 0;
            while (pos < header.Length)
            {
                int start = header.IndexOf("=?", pos, StringComparison.Ordinal);
                if (start < 0)
                {
                    sb.Append(header.Substring(pos));
                    break;
                }
                if (start > pos)
                {
                    sb.Append(header.Substring(pos, start - pos));
                }
                int end = header.IndexOf("?=", start + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    sb.Append(header.Substring(pos));
                    break;
                }
                string word = header.Substring(start, end + 2 - start);
                var (decoded, err) = Decode(word);
                if (err != null)
                {
                    sb.Append(word);
                }
                else
                {
                    sb.Append(decoded);
                }
                pos = end + 2;
            }
            return (sb.ToString(), null);
        }
    }
}

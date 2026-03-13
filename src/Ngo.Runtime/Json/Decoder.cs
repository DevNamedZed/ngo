using System;
using System.Text;
using System.Text.Json;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Json
{
    // json.Decoder struct
    [GoType("struct", Name = "Decoder", Package = "encoding/json")]
    public class Decoder
    {
        private readonly IGoReader? _reader;
        private readonly StringBuilder _buffer = new StringBuilder();
        #pragma warning disable CS0414
        private bool _useNumber;
        private bool _disallowUnknown;
        #pragma warning restore CS0414

        public Decoder()
        {
            _reader = null;
        }

        public Decoder(IGoReader reader)
        {
            _reader = reader;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Decode(object? v)
        {
            if (_reader == null || v == null)
            {
                return "json: invalid decoder or target";
            }

            try
            {
                // Read all available data from the reader
                var chunk = new byte[4096];
                var sliceChunk = new Slice<byte>(chunk);
                while (true)
                {
                    var (n, err) = _reader.Read(sliceChunk);
                    if (n > 0)
                    {
                        for (int i = 0; i < (int)n; i++)
                        {
                            _buffer.Append((char)sliceChunk[i]);
                        }
                    }
                    if (err != null)
                    {
                        break;
                    }
                    if (n == 0)
                    {
                        break;
                    }
                }

                var jsonStr = _buffer.ToString().Trim();
                if (string.IsNullOrEmpty(jsonStr))
                {
                    return "EOF";
                }

                // Find the end of the first JSON value
                int endIdx = FindJsonValueEnd(jsonStr);
                string toParse;
                if (endIdx >= 0 && endIdx < jsonStr.Length - 1)
                {
                    toParse = jsonStr.Substring(0, endIdx + 1);
                    _buffer.Clear();
                    _buffer.Append(jsonStr.Substring(endIdx + 1));
                }
                else
                {
                    toParse = jsonStr;
                    _buffer.Clear();
                }

                var data = new Slice<byte>(global::System.Text.Encoding.UTF8.GetBytes(toParse));
                return Package.Unmarshal(data, v);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        public bool More()
        {
            var str = _buffer.ToString().Trim();
            if (string.IsNullOrEmpty(str))
            {
                return false;
            }
            // Check if there's content that's not just a closing bracket
            foreach (char c in str)
            {
                if (c != ']' && c != '}' && !char.IsWhiteSpace(c) && c != ',')
                {
                    return true;
                }
            }
            return false;
        }

        [GoMethod]
        public void DisallowUnknownFields()
        {
            _disallowUnknown = true;
        }

        [GoMethod]
        public void UseNumber()
        {
            _useNumber = true;
        }

        [GoMethod]
        public long InputOffset()
        {
            return 0;
        }

        [GoMethod]
        [return: GoReturn("json.Token", "error")]
        public (object?, object?) Token()
        {
            return (null, "EOF");
        }

        [GoMethod]
        [return: GoReturn("io.Reader")]
        public object? Buffered()
        {
            return null;
        }

        private static int FindJsonValueEnd(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return -1;
            }

            int i = 0;
            // Skip whitespace
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
            if (i >= s.Length)
            {
                return -1;
            }

            char first = s[i];
            if (first == '{' || first == '[')
            {
                return FindMatchingBracket(s, i);
            }
            if (first == '"')
            {
                return FindStringEnd(s, i);
            }
            // Number, bool, null — read until delimiter
            int start = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != ',' && s[i] != ']' && s[i] != '}')
            {
                i++;
            }
            return i - 1;
        }

        private static int FindMatchingBracket(string s, int start)
        {
            char open = s[start];
            char close = open == '{' ? '}' : ']';
            int depth = 0;
            bool inString = false;
            for (int i = start; i < s.Length; i++)
            {
                if (inString)
                {
                    if (s[i] == '\\')
                    {
                        i++;
                    }
                    else if (s[i] == '"')
                    {
                        inString = false;
                    }
                }
                else
                {
                    if (s[i] == '"')
                    {
                        inString = true;
                    }
                    else if (s[i] == open)
                    {
                        depth++;
                    }
                    else if (s[i] == close)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return i;
                        }
                    }
                }
            }
            return s.Length - 1;
        }

        private static int FindStringEnd(string s, int start)
        {
            for (int i = start + 1; i < s.Length; i++)
            {
                if (s[i] == '\\')
                {
                    i++;
                }
                else if (s[i] == '"')
                {
                    return i;
                }
            }
            return s.Length - 1;
        }
    }
}

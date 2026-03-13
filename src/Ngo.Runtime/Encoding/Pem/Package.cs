using System;
using System.Collections.Generic;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Encoding.Pem
{
    [GoPackage("encoding/pem")]
    public static class Package
    {
        private const string PemBegin = "-----BEGIN ";
        private const string PemEnd = "-----END ";
        private const string PemDashes = "-----";

        // pem.Decode(data []byte) (p *Block, rest []byte)
        [GoFunc]
        [return: GoReturn("*pem.Block", "[]byte")]
        public static (GoBlock?, Slice<byte>) Decode(Slice<byte> data)
        {
            var text = SliceToString(data);

            // Find the BEGIN line
            int beginIdx = text.IndexOf(PemBegin, StringComparison.Ordinal);
            if (beginIdx < 0)
            {
                return (null, data);
            }

            int lineEnd = text.IndexOf('\n', beginIdx);
            if (lineEnd < 0)
            {
                return (null, data);
            }

            string beginLine = text.Substring(beginIdx, lineEnd - beginIdx).TrimEnd('\r');
            if (!beginLine.EndsWith(PemDashes))
            {
                return (null, data);
            }

            string typeName = beginLine.Substring(PemBegin.Length, beginLine.Length - PemBegin.Length - PemDashes.Length);
            string endMarker = PemEnd + typeName + PemDashes;

            int bodyStart = lineEnd + 1;

            // Parse optional headers
            var headers = new Map<string, string>();
            int headerEnd = bodyStart;
            while (headerEnd < text.Length)
            {
                int nextNewline = text.IndexOf('\n', headerEnd);
                if (nextNewline < 0)
                {
                    break;
                }
                string headerLine = text.Substring(headerEnd, nextNewline - headerEnd).TrimEnd('\r');
                if (string.IsNullOrEmpty(headerLine))
                {
                    // Empty line ends headers section (only if we found at least one header)
                    if (headers.Len > 0)
                    {
                        headerEnd = nextNewline + 1;
                    }
                    break;
                }
                int colonIdx = headerLine.IndexOf(':');
                if (colonIdx < 0)
                {
                    break;
                }
                string key = headerLine.Substring(0, colonIdx).Trim();
                string value = headerLine.Substring(colonIdx + 1).Trim();
                headers[key] = value;
                headerEnd = nextNewline + 1;
            }

            if (headers.Len > 0)
            {
                bodyStart = headerEnd;
            }

            // Find the END line
            int endIdx = text.IndexOf(endMarker, bodyStart, StringComparison.Ordinal);
            if (endIdx < 0)
            {
                return (null, data);
            }

            // Extract base64 body
            string base64Body = text.Substring(bodyStart, endIdx - bodyStart).Trim();
            // Remove whitespace/newlines from base64
            var cleanBase64 = new StringBuilder();
            foreach (char c in base64Body)
            {
                if (c != '\n' && c != '\r' && c != ' ' && c != '\t')
                {
                    cleanBase64.Append(c);
                }
            }

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(cleanBase64.ToString());
            }
            catch
            {
                return (null, data);
            }

            var block = new GoBlock
            {
                Type = typeName,
                Headers = headers,
                Bytes = new Slice<byte>(decoded)
            };

            // Find rest after the END line
            int restStart = endIdx + endMarker.Length;
            int restNewline = text.IndexOf('\n', restStart);
            if (restNewline >= 0)
            {
                restStart = restNewline + 1;
            }
            else
            {
                restStart = text.Length;
            }

            var restBytes = System.Text.Encoding.UTF8.GetBytes(text.Substring(restStart));
            return (block, new Slice<byte>(restBytes));
        }

        // pem.Encode(out io.Writer, b *Block) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Encode([GoParam("io.Writer")] object? @out, [GoParam("*pem.Block")] GoBlock? b)
        {
            if (@out is not IGoWriter writer || b == null)
            {
                return null;
            }

            var sb = new StringBuilder();
            sb.Append(PemBegin);
            sb.Append(b.Type);
            sb.Append(PemDashes);
            sb.Append('\n');

            // Write headers if any
            if (b.Headers != null && b.Headers.Len > 0)
            {
                foreach (var kv in b.Headers)
                {
                    sb.Append(kv.Key);
                    sb.Append(": ");
                    sb.Append(kv.Value);
                    sb.Append('\n');
                }
                sb.Append('\n');
            }

            // Write base64-encoded body in 64-char lines
            var bodyBytes = new byte[b.Bytes.Len];
            for (int i = 0; i < b.Bytes.Len; i++)
            {
                bodyBytes[i] = b.Bytes[i];
            }
            string base64 = Convert.ToBase64String(bodyBytes);
            for (int i = 0; i < base64.Length; i += 64)
            {
                int len = System.Math.Min(64, base64.Length - i);
                sb.Append(base64, i, len);
                sb.Append('\n');
            }

            sb.Append(PemEnd);
            sb.Append(b.Type);
            sb.Append(PemDashes);
            sb.Append('\n');

            var outputBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            writer.Write(new Slice<byte>(outputBytes));
            return null;
        }

        // pem.EncodeToMemory(b *Block) []byte
        [GoFunc]
        [return: GoReturn("[]byte")]
        public static Slice<byte> EncodeToMemory([GoParam("*pem.Block")] GoBlock? b)
        {
            if (b == null)
            {
                return new Slice<byte>();
            }

            var sb = new StringBuilder();
            sb.Append(PemBegin);
            sb.Append(b.Type);
            sb.Append(PemDashes);
            sb.Append('\n');

            if (b.Headers != null && b.Headers.Len > 0)
            {
                foreach (var kv in b.Headers)
                {
                    sb.Append(kv.Key);
                    sb.Append(": ");
                    sb.Append(kv.Value);
                    sb.Append('\n');
                }
                sb.Append('\n');
            }

            var bodyBytes = new byte[b.Bytes.Len];
            for (int i = 0; i < b.Bytes.Len; i++)
            {
                bodyBytes[i] = b.Bytes[i];
            }
            string base64 = Convert.ToBase64String(bodyBytes);
            for (int i = 0; i < base64.Length; i += 64)
            {
                int len = System.Math.Min(64, base64.Length - i);
                sb.Append(base64, i, len);
                sb.Append('\n');
            }

            sb.Append(PemEnd);
            sb.Append(b.Type);
            sb.Append(PemDashes);
            sb.Append('\n');

            return new Slice<byte>(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        }

        private static string SliceToString(Slice<byte> s)
        {
            if (s.IsNil || s.Len == 0)
            {
                return "";
            }
            var arr = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
            {
                arr[i] = s[i];
            }
            return System.Text.Encoding.UTF8.GetString(arr);
        }
    }

    // pem.Block struct
    [GoType("struct", Name = "Block", Package = "encoding/pem")]
    public class GoBlock
    {
        [GoField(Name = "Type")] public string Type = "";
        [GoField(Name = "Headers")] public Map<string, string> Headers = new Map<string, string>();
        [GoField(Name = "Bytes")] public Slice<byte> Bytes;
    }
}

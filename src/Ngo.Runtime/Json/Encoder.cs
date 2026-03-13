using System;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Json
{
    // json.Encoder struct
    [GoType("struct", Name = "Encoder", Package = "encoding/json")]
    public class Encoder
    {
        private readonly IGoWriter? _writer;
        private string _prefix = "";
        private string _indent = "";
        private bool _escapeHTML = true;

        public Encoder()
        {
            _writer = null;
        }

        public Encoder(IGoWriter writer)
        {
            _writer = writer;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Encode(object? v)
        {
            if (_writer == null)
            {
                return "json: invalid encoder";
            }

            try
            {
                Slice<byte> jsonBytes;
                object? err;

                if (!string.IsNullOrEmpty(_indent))
                {
                    (jsonBytes, err) = Package.MarshalIndent(v, _prefix, _indent);
                }
                else
                {
                    (jsonBytes, err) = Package.Marshal(v);
                }

                if (err != null)
                {
                    return err;
                }

                // Append newline (Go's Encoder.Encode adds a trailing newline)
                var output = new byte[jsonBytes.Len + 1];
                for (int i = 0; i < jsonBytes.Len; i++)
                {
                    output[i] = jsonBytes[i];
                }
                output[jsonBytes.Len] = (byte)'\n';

                _writer.Write(new Slice<byte>(output));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        public void SetIndent(string prefix, string indent)
        {
            _prefix = prefix;
            _indent = indent;
        }

        [GoMethod]
        public void SetEscapeHTML(bool on)
        {
            _escapeHTML = on;
        }
    }
}

using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.Encoder struct
    [GoType("struct", Name = "Encoder", Package = "encoding/xml")]
    public class GoEncoder
    {
        private readonly IGoWriter? _writer;
        private string _prefix = "";
        private string _indent = "";

        public GoEncoder() { }

        public GoEncoder(IGoWriter? writer)
        {
            _writer = writer;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Encode(object? v)
        {
            if (_writer == null)
            {
                return "xml: nil writer";
            }
            var (xmlBytes, err) = string.IsNullOrEmpty(_indent) ? Package.Marshal(v) : Package.MarshalIndent(v, _prefix, _indent);
            if (err != null)
            {
                return err;
            }
            // Append newline
            var output = new byte[xmlBytes.Len + 1];
            for (int i = 0; i < xmlBytes.Len; i++)
            {
                output[i] = xmlBytes[i];
            }
            output[xmlBytes.Len] = (byte)'\n';
            _writer.Write(new Slice<byte>(output));
            return null;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? EncodeElement(object? v, GoStartElement start) => Encode(v);

        [GoMethod]
        [return: GoReturn("error")]
        public object? EncodeToken([GoParam("xml.Token")] object? t) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Flush() => null;

        [GoMethod]
        public void Indent(string prefix, string indent)
        {
            _prefix = prefix;
            _indent = indent;
        }
    }
}

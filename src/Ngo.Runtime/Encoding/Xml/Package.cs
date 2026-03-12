using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    [GoPackage("encoding/xml")]
    public static class Package
    {
        // xml.Header constant
        [GoConst(Type = "string")]
        public const string Header = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";

        // xml.Marshal(v any) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) Marshal(object? v) => (new Slice<byte>(), null);

        // xml.MarshalIndent(v any, prefix, indent string) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) MarshalIndent(object? v, string prefix, string indent) => (new Slice<byte>(), null);

        // xml.Unmarshal(data []byte, v any) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unmarshal(Slice<byte> data, object? v) => null;

        // xml.NewEncoder(w io.Writer) *Encoder
        [GoFunc]
        [return: GoReturn("*xml.Encoder")]
        public static GoEncoder NewEncoder([GoParam("io.Writer")] object? w) => new GoEncoder();

        // xml.NewDecoder(r io.Reader) *Decoder
        [GoFunc]
        [return: GoReturn("*xml.Decoder")]
        public static GoDecoder NewDecoder([GoParam("io.Reader")] object? r) => new GoDecoder();

        // xml.Escape(w io.Writer, s []byte)
        [GoFunc]
        public static void Escape([GoParam("io.Writer")] object? w, Slice<byte> s) { }

        // xml.EscapeText(w io.Writer, s []byte) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? EscapeText([GoParam("io.Writer")] object? w, Slice<byte> s) => null;

        // xml.CopyToken(t Token) Token
        [GoFunc]
        [return: GoReturn("xml.Token")]
        public static object? CopyToken([GoParam("xml.Token")] object? t) => null;

        // xml.Token interface (empty - it's a union type marker)
        [GoType("interface", Name = "Token", Package = "encoding/xml")]
        public interface IToken { }

        // xml.Marshaler interface
        [GoType("interface", Name = "Marshaler", Package = "encoding/xml")]
        public interface IMarshaler
        {
            [GoMethod]
            [return: GoReturn("[]byte", "error")]
            (Slice<byte>, object?) MarshalXML([GoParam("*xml.Encoder")] GoEncoder? e, GoStartElement start);
        }

        // xml.Unmarshaler interface
        [GoType("interface", Name = "Unmarshaler", Package = "encoding/xml")]
        public interface IUnmarshaler
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? UnmarshalXML([GoParam("*xml.Decoder")] GoDecoder? d, GoStartElement start);
        }

        // xml.MarshalerAttr interface
        [GoType("interface", Name = "MarshalerAttr", Package = "encoding/xml")]
        public interface IMarshalerAttr
        {
            [GoMethod]
            [return: GoReturn("xml.Attr", "error")]
            (GoAttr, object?) MarshalXMLAttr(GoName name);
        }

        // xml.UnmarshalerAttr interface
        [GoType("interface", Name = "UnmarshalerAttr", Package = "encoding/xml")]
        public interface IUnmarshalerAttr
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? UnmarshalXMLAttr(GoAttr attr);
        }

        // xml.TokenReader interface
        [GoType("interface", Name = "TokenReader", Package = "encoding/xml")]
        public interface ITokenReader
        {
            [GoMethod]
            [return: GoReturn("xml.Token", "error")]
            (object?, object?) Token();
        }
    }
}

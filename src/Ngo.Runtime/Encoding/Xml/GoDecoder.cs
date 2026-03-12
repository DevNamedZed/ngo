using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Xml
{
    // xml.Decoder struct
    [GoType("struct", Name = "Decoder", Package = "encoding/xml")]
    public class GoDecoder
    {
        [GoField(Name = "Strict")] public bool Strict;
        [GoField(Name = "AutoClose")] public Slice<string> AutoClose;
        [GoField(Name = "CharsetReader")] public object? CharsetReader;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Decode(object? v) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? DecodeElement(object? v, [GoParam("*xml.StartElement")] object? start) => null;

        [GoMethod]
        [return: GoReturn("xml.Token", "error")]
        public (object?, object?) Token() => (null, null);

        [GoMethod]
        [return: GoReturn("xml.Token", "error")]
        public (object?, object?) RawToken() => (null, null);

        [GoMethod]
        [return: GoReturn("int64")]
        public long InputOffset() => 0;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Skip() => null;
    }
}

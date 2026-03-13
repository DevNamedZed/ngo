using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Encoding.Gob
{
    [GoPackage("encoding/gob")]
    public static class Package
    {
        // gob.NewEncoder(w io.Writer) *Encoder
        [GoFunc]
        [return: GoReturn("*gob.Encoder")]
        public static GoEncoder NewEncoder(object? w) => new GoEncoder();

        // gob.NewDecoder(r io.Reader) *Decoder
        [GoFunc]
        [return: GoReturn("*gob.Decoder")]
        public static GoDecoder NewDecoder(object? r) => new GoDecoder();

        // gob.Register(value interface{})
        [GoFunc]
        public static void Register(object? value) { }

        // gob.RegisterName(name string, value interface{})
        [GoFunc]
        public static void RegisterName(string name, object? value) { }

        // gob.GobEncoder interface
        [GoType("interface", Name = "GobEncoder", Package = "encoding/gob")]
        public interface IGobEncoder
        {
            [GoMethod]
            [return: GoReturn("[]byte", "error")]
            (Slice<byte>, object?) GobEncode();
        }

        // gob.GobDecoder interface
        [GoType("interface", Name = "GobDecoder", Package = "encoding/gob")]
        public interface IGobDecoder
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? GobDecode(Slice<byte> data);
        }
    }

    [GoType("struct", Name = "Encoder", Package = "encoding/gob")]
    public class GoEncoder
    {
        [GoMethod]
        [return: GoReturn("error")]
        public object? Encode(object? e) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? EncodeValue([GoParam("reflect.Value")] object? value) => null;
    }

    [GoType("struct", Name = "Decoder", Package = "encoding/gob")]
    public class GoDecoder
    {
        [GoMethod]
        [return: GoReturn("error")]
        public object? Decode(object? e) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? DecodeValue([GoParam("reflect.Value")] object? value) => null;
    }
}

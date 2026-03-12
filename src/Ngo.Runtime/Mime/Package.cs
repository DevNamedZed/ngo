using System.Collections.Generic;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Mime
{
    [GoPackage("mime")]
    public static class Package
    {
        // mime.TypeByExtension(ext string) string
        [GoFunc]
        public static string TypeByExtension(string ext) => "";

        // mime.ExtensionsByType(typ string) ([]string, error)
        [GoFunc]
        [return: GoReturn("[]string", "error")]
        public static (Slice<string>, object?) ExtensionsByType(string typ) => (new Slice<string>(), null);

        // mime.FormatMediaType(t string, param map[string]string) string
        [GoFunc]
        public static string FormatMediaType(string t, Map<string, string> param) => "";

        // mime.ParseMediaType(v string) (mediatype string, params map[string]string, err error)
        [GoFunc]
        [return: GoReturn("string", "map[string]string", "error")]
        public static (string, Map<string, string>, object?) ParseMediaType(string v) => ("", new Map<string, string>(), null);

        // mime.AddExtensionType(ext, typ string) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? AddExtensionType(string ext, string typ) => null;

        // WordEncoder constants (type WordEncoder byte)
        [GoVar(Type = "mime.WordEncoder")]
        public static readonly GoWordEncoder BEncoding = new GoWordEncoder { Value = (byte)'b' };

        [GoVar(Type = "mime.WordEncoder")]
        public static readonly GoWordEncoder QEncoding = new GoWordEncoder { Value = (byte)'q' };
    }

    // mime.WordEncoder type (named byte)
    [GoType("named", Name = "WordEncoder", Package = "mime", Underlying = "byte")]
    public struct GoWordEncoder
    {
        public byte Value;

        [GoMethod]
        public string Encode(string charset, string s) => s;
    }

    // mime.WordDecoder struct
    [GoType("struct", Name = "WordDecoder", Package = "mime")]
    public class GoWordDecoder
    {
        [GoField(Name = "CharsetReader", Type = "func(string, io.Reader) (io.Reader, error)")] public object? CharsetReader;

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, object?) Decode(string word) => ("", null);

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, object?) DecodeHeader(string header) => ("", null);
    }
}

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Url
{
    [GoType("struct", Name = "URL", Package = "net/url")]
    public class GoURL
    {
        [GoField(Name = "Scheme")] public string Scheme { get; set; } = "";
        [GoField(Name = "Opaque")] public string Opaque { get; set; } = "";
        [GoField(Name = "User", Type = "*Userinfo")] public GoUserinfo? User { get; set; }
        [GoField(Name = "Host")] public string Host { get; set; } = "";
        [GoField(Name = "Path")] public string Path { get; set; } = "";
        [GoField(Name = "RawPath")] public string RawPath { get; set; } = "";
        [GoField(Name = "OmitHost")] public bool OmitHost { get; set; }
        [GoField(Name = "ForceQuery")] public bool ForceQuery { get; set; }
        [GoField(Name = "RawQuery")] public string RawQuery { get; set; } = "";
        [GoField(Name = "Fragment")] public string Fragment { get; set; } = "";
        [GoField(Name = "RawFragment")] public string RawFragment { get; set; } = "";

        [GoMethod]
        public string String() => $"{Scheme}://{Host}{Path}";

        [GoMethod]
        [return: GoReturn("*URL", "error")]
        public (GoURL?, object?) Parse(string @ref)
        {
            return Package.Parse(@ref);
        }

        [GoMethod]
        [return: GoReturn("*URL")]
        public GoURL? ResolveReference(GoURL? @ref) => @ref;

        [GoMethod]
        public string Hostname()
        {
            int colon = Host.LastIndexOf(':');
            if (colon < 0) return Host;
            if (Host.StartsWith("["))
            {
                int bracket = Host.IndexOf(']');
                if (bracket >= 0) return Host.Substring(1, bracket - 1);
            }
            return Host.Substring(0, colon);
        }

        [GoMethod]
        public string Port()
        {
            int colon = Host.LastIndexOf(':');
            if (colon < 0) return "";
            return Host.Substring(colon + 1);
        }

        [GoMethod]
        public string RequestURI()
        {
            var result = Path;
            if (RawQuery != "") result += "?" + RawQuery;
            return result;
        }

        [GoMethod]
        public string EscapedPath() => Path;

        [GoMethod]
        public string EscapedFragment() => Fragment;

        [GoMethod]
        [return: GoReturn("string")]
        public string Redacted() => String();

        [GoMethod]
        public bool IsAbs() => Scheme != "";

        [GoMethod]
        [return: GoReturn("Values")]
        public GoValues Query()
        {
            return new GoValues();
        }

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, object?) MarshalBinary() => (String(), null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? UnmarshalBinary(Slice<byte> text) => null;

        [GoMethod]
        [return: GoReturn("*URL")]
        public GoURL? JoinPath(params string[] elem) => this;
    }
}

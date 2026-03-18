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
        public string String()
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(Scheme))
            {
                sb.Append(Scheme);
                sb.Append("://");
            }
            if (User != null)
            {
                sb.Append(User.String());
                sb.Append('@');
            }
            sb.Append(Host);
            sb.Append(Path);
            if (!string.IsNullOrEmpty(RawQuery))
            {
                sb.Append('?');
                sb.Append(RawQuery);
            }
            if (!string.IsNullOrEmpty(Fragment))
            {
                sb.Append('#');
                sb.Append(Fragment);
            }
            return sb.ToString();
        }

        [GoMethod]
        [return: GoReturn("*URL", "error")]
        public (GoURL?, object?) Parse(string @ref)
        {
            return Package.Parse(@ref);
        }

        [GoMethod]
        [return: GoReturn("*URL")]
        public GoURL? ResolveReference(GoURL? @ref)
        {
            if (@ref == null)
            {
                return this;
            }
            var result = new GoURL
            {
                Scheme = @ref.Scheme,
                Host = @ref.Host,
                Path = @ref.Path,
                RawQuery = @ref.RawQuery,
                Fragment = @ref.Fragment,
                User = @ref.User,
            };
            if (string.IsNullOrEmpty(result.Scheme))
            {
                result.Scheme = Scheme;
            }
            if (string.IsNullOrEmpty(result.Host))
            {
                result.Host = Host;
                if (!result.Path.StartsWith("/"))
                {
                    // Merge paths
                    int lastSlash = Path.LastIndexOf('/');
                    if (lastSlash >= 0)
                    {
                        result.Path = Path.Substring(0, lastSlash + 1) + result.Path;
                    }
                }
            }
            return result;
        }

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
            if (string.IsNullOrEmpty(RawQuery))
            {
                return new GoValues();
            }
            var (values, _) = Package.ParseQuery(RawQuery);
            return values ?? new GoValues();
        }

        [GoMethod]
        [return: GoReturn("string", "error")]
        public (string, object?) MarshalBinary() => (String(), null);

        [GoMethod]
        [return: GoReturn("error")]
        public object? UnmarshalBinary(Slice<byte> text) => null;

        [GoMethod]
        [return: GoReturn("*URL")]
        public GoURL? JoinPath(params string[] elem)
        {
            var result = new GoURL
            {
                Scheme = Scheme,
                Host = Host,
                User = User,
                RawQuery = RawQuery,
                Fragment = Fragment,
            };
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(Path))
            {
                parts.Add(Path.TrimEnd('/'));
            }
            foreach (var e in elem)
            {
                parts.Add(e.Trim('/'));
            }
            result.Path = string.Join("/", parts);
            if (!result.Path.StartsWith("/") && !string.IsNullOrEmpty(result.Host))
            {
                result.Path = "/" + result.Path;
            }
            return result;
        }
    }
}

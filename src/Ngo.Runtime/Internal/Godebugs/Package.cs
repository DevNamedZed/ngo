using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Godebugs
{
    [GoPackage("internal/godebugs")]
    public static class Package
    {
        // All is the table of known GODEBUG settings, sorted by Name.
        [GoVar(Type = "[]godebugs.Info")]
        public static Slice<GoInfo> All = new Slice<GoInfo>(new GoInfo[]
        {
            new GoInfo { Name = "execerrdot", Package_ = "os/exec" },
            new GoInfo { Name = "gocachehash", Package_ = "cmd/go" },
            new GoInfo { Name = "gocachetest", Package_ = "cmd/go" },
            new GoInfo { Name = "gocacheverify", Package_ = "cmd/go" },
            new GoInfo { Name = "gotypesalias", Package_ = "go/types" },
            new GoInfo { Name = "http2client", Package_ = "net/http" },
            new GoInfo { Name = "http2debug", Package_ = "net/http", Opaque = true },
            new GoInfo { Name = "http2server", Package_ = "net/http" },
            new GoInfo { Name = "httplaxcontentlength", Package_ = "net/http", Changed = 22, Old = "1" },
            new GoInfo { Name = "httpmuxgo121", Package_ = "net/http", Changed = 22, Old = "1" },
            new GoInfo { Name = "installgoroot", Package_ = "go/build" },
            new GoInfo { Name = "jstmpllitinterp", Package_ = "html/template" },
            new GoInfo { Name = "multipathtcp", Package_ = "net" },
            new GoInfo { Name = "netdns", Package_ = "net", Opaque = true },
            new GoInfo { Name = "panicnil", Package_ = "runtime", Changed = 21, Old = "1" },
            new GoInfo { Name = "randautoseed", Package_ = "math/rand" },
            new GoInfo { Name = "tarinsecurepath", Package_ = "archive/tar", Changed = 22, Old = "0" },
            new GoInfo { Name = "tls10server", Package_ = "crypto/tls", Changed = 22, Old = "1" },
            new GoInfo { Name = "tlsmaxrsasize", Package_ = "crypto/tls" },
            new GoInfo { Name = "tlsrsakex", Package_ = "crypto/tls", Changed = 22, Old = "1" },
            new GoInfo { Name = "tlsunsafeekm", Package_ = "crypto/tls" },
            new GoInfo { Name = "winreadlinkvolume", Package_ = "os", Changed = 22, Old = "0" },
            new GoInfo { Name = "winsymlink", Package_ = "os", Changed = 22, Old = "0" },
            new GoInfo { Name = "x509keypairleaf", Package_ = "crypto/tls" },
            new GoInfo { Name = "x509negativeserial", Package_ = "crypto/x509" },
            new GoInfo { Name = "x509sha1", Package_ = "crypto/x509" },
            new GoInfo { Name = "x509usefallbackroots", Package_ = "crypto/x509" },
            new GoInfo { Name = "x509usepolicies", Package_ = "crypto/x509" },
            new GoInfo { Name = "zipinsecurepath", Package_ = "archive/zip", Changed = 22, Old = "0" },
        });
    }

    [GoType("struct", Name = "Info", Package = "internal/godebugs")]
    public struct GoInfo
    {
        [GoField] public string Name;
        [GoField(Name = "Package")] public string Package_;
        [GoField] public long Changed;
        [GoField] public string Old;
        [GoField] public bool Opaque;
    }
}

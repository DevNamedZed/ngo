using Ngo.Runtime.Discovery;
using Ngo.Runtime.Time;

namespace Ngo.Runtime.Http
{
    [GoType("struct", Name = "Cookie", Package = "net/http")]
    public class Cookie
    {
        [GoField(Name = "Name")] public string Name { get; set; } = "";
        [GoField(Name = "Value")] public string Value { get; set; } = "";
        [GoField(Name = "Path")] public string Path { get; set; } = "";
        [GoField(Name = "Domain")] public string Domain { get; set; } = "";
        [GoField(Name = "MaxAge")] public long MaxAge { get; set; }
        [GoField(Name = "Secure")] public bool Secure { get; set; }
        [GoField(Name = "HttpOnly")] public bool HttpOnly { get; set; }
        [GoField(Name = "Expires")] public GoTimeValue Expires { get; set; }
        [GoField(Name = "RawExpires")] public string RawExpires { get; set; } = "";
        [GoField(Name = "SameSite")] public long SameSite { get; set; }
        [GoField(Name = "Raw")] public string Raw { get; set; } = "";
        [GoField(Name = "Unparsed")] public Slice<string> Unparsed { get; set; } = new();
        [GoField(Name = "Partitioned")] public bool Partitioned { get; set; }

        [GoMethod]
        public string String() => Name + "=" + Value;

        [GoMethod]
        public bool Valid() => Name != "";
    }
}

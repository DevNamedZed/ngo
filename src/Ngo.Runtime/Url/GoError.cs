using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Url
{
    [GoType("struct", Name = "Error", Package = "net/url")]
    public class GoError
    {
        [GoField(Name = "Op")] public string Op { get; set; } = "";
        [GoField(Name = "URL")] public string URL { get; set; } = "";
        [GoField(Name = "Err", Type = "error")] public object? Err { get; set; }

        [GoMethod]
        public string Error() => $"{Op} {URL}: {Err}";

        [GoMethod]
        public object? Unwrap() => Err;

        [GoMethod]
        public bool Timeout() => false;

        [GoMethod]
        public bool Temporary() => false;
    }
}

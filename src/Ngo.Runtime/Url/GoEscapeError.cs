using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Url
{
    [GoType("named", Name = "EscapeError", Package = "net/url", Underlying = "string")]
    public class GoEscapeError
    {
        [GoMethod]
        public string Error() => "";
    }
}

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Url
{
    [GoType("named", Name = "InvalidHostError", Package = "net/url", Underlying = "string")]
    public class GoInvalidHostError
    {
        [GoMethod]
        public string Error() => "";
    }
}

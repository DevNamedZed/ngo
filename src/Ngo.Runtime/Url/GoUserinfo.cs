using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Url
{
    [GoType("struct", Name = "Userinfo", Package = "net/url")]
    public class GoUserinfo
    {
        internal string username = "";
        internal string password = "";
        internal bool passwordSet;

        [GoMethod]
        public string Username() => username;

        [GoMethod]
        [return: GoReturn("string", "bool")]
        public (string, bool) Password() => (password, passwordSet);

        [GoMethod]
        public string String() => passwordSet ? $"{username}:{password}" : username;
    }
}

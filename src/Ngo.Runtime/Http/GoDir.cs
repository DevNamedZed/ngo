using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Http
{
    [GoType("named", Name = "Dir", Package = "net/http", Underlying = "string")]
    public class GoDir
    {
        [GoMethod]
        [return: GoReturn("http.File", "error")]
        public (IFile?, object?) Open(string name)
        {
            return (null, "not implemented");
        }
    }
}

using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Godebug
{
    /// <summary>
    /// Stub for internal/godebug — runtime debug settings.
    /// Note: different from internal/godebugs (which tracks registered settings).
    /// </summary>
    [GoPackage("internal/godebug")]
    public static class Package
    {
        // func New(name string) *Setting
        [GoFunc]
        [return: GoReturn("*internal/godebug.Setting")]
        public static GoSetting New(string name) => new GoSetting(name);
    }

    [GoType("struct", Name = "Setting", Package = "internal/godebug")]
    public class GoSetting
    {
        private readonly string _name;

        public GoSetting(string name = "") { _name = name; }

        // func (s *Setting) Value() string
        [GoMethod]
        public string Value()
        {
            var env = System.Environment.GetEnvironmentVariable("GODEBUG") ?? "";
            foreach (var part in env.Split(','))
            {
                var eq = part.IndexOf('=');
                if (eq > 0 && part.Substring(0, eq) == _name)
                    return part.Substring(eq + 1);
            }
            return "";
        }

        // func (s *Setting) IncNonDefault()
        [GoMethod]
        public void IncNonDefault() { }

        // func (s *Setting) Name() string
        [GoMethod]
        public string Name() => _name;
    }
}

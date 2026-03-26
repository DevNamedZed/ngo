using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Net
{
    [GoType("struct", Name = "ParseError", Package = "net")]
    public class GoParseError
    {
        [GoField(Name = "Type")] public string Type = "";
        [GoField(Name = "Text")] public string Text = "";

        [GoMethod]
        public string Error()
        {
            return $"invalid {Type}: {Text}";
        }
    }
}

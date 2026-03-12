using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Text.Template
{
    [GoPackage("text/template")]
    public static class Package
    {
        // template.New(name string) *Template
        [GoFunc]
        [return: GoReturn("*template.Template")]
        public static GoTemplate New(string name) => new GoTemplate();

        // template.Must(t *Template, err error) *Template
        [GoFunc]
        [return: GoReturn("*template.Template")]
        public static GoTemplate Must(GoTemplate? t, object? err) => t ?? new GoTemplate();

        // template.ParseFiles(filenames ...string) (*Template, error)
        [GoFunc(IsVariadic = true)]
        [return: GoReturn("*template.Template", "error")]
        public static (GoTemplate, object?) ParseFiles(params string[] filenames) => (new GoTemplate(), null);

        // template.ParseGlob(pattern string) (*Template, error)
        [GoFunc]
        [return: GoReturn("*template.Template", "error")]
        public static (GoTemplate, object?) ParseGlob(string pattern) => (new GoTemplate(), null);

        // template.HTMLEscape(w io.Writer, b []byte)
        [GoFunc]
        public static void HTMLEscape([GoParam("io.Writer")] object? w, Slice<byte> b) { }

        [GoFunc]
        public static string HTMLEscapeString(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        [GoFunc]
        public static string HTMLEscaper(params object[] args) => "";

        [GoFunc]
        public static void JSEscape([GoParam("io.Writer")] object? w, Slice<byte> b) { }

        [GoFunc]
        public static string JSEscapeString(string s) => s ?? "";

        [GoFunc]
        public static string JSEscaper(params object[] args) => "";

        [GoFunc]
        public static string URLQueryEscaper(params object[] args) => "";

        [GoFunc]
        [return: GoReturn("bool", "bool")]
        public static (bool, bool) IsTrue(object? val) => (val != null, true);
    }

    // template.FuncMap type (map[string]interface{})
    [GoType("named", Name = "FuncMap", Package = "text/template", Underlying = "map[string]interface{}")]
    public class GoFuncMap : System.Collections.Generic.Dictionary<string, object?>
    {
    }

    [GoType("struct", Name = "Template", Package = "text/template")]
    public class GoTemplate
    {
        [GoMethod]
        [return: GoReturn("error")]
        public object? Execute(object? wr, object? data) => null;

        [GoMethod]
        [return: GoReturn("error")]
        public object? ExecuteTemplate(object? wr, string name, object? data) => null;

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) Parse(string text) => (this, null);

        [GoMethod]
        public string Name() => "";

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate Funcs(object? funcMap) => this;

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate Option(Slice<string> opt) => this;

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) ParseFiles(Slice<string> filenames) => (this, null);

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) ParseGlob(string pattern) => (this, null);

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate? Lookup(string name) => null;

        [GoMethod]
        [return: GoReturn("[]*template.Template")]
        public Slice<GoTemplate> Templates() => new Slice<GoTemplate>(System.Array.Empty<GoTemplate>());

        [GoField(Name = "Tree", Type = "*parse.Tree", Embedded = true)]
        public object? Tree;

        // Promoted fields from *parse.Tree
        [GoField(Name = "Root")] public object? Root;
        [GoField(Name = "Mode")] public long Mode;
        [GoField(Name = "ParseName")] public string ParseName = "";

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) AddParseTree(string name, object? tree) => (this, null);

        [GoMethod]
        public string DefinedTemplates() => "";

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) Clone() => (new GoTemplate(), null);

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate New(string name) => new GoTemplate();

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate Delims(string left, string right) => this;
    }
}

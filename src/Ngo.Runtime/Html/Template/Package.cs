using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Html.Template
{
    [GoPackage("html/template")]
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

        // template.HTMLEscapeString(s string) string
        [GoFunc]
        public static string HTMLEscapeString(string s) => System.Net.WebUtility.HtmlEncode(s) ?? s;

        // template.HTMLEscaper(args ...interface{}) string
        [GoFunc(IsVariadic = true)]
        public static string HTMLEscaper(params object?[] args) => "";

        // template.JSEscapeString(s string) string
        [GoFunc]
        public static string JSEscapeString(string s) => s;

        // template.URLQueryEscaper(args ...interface{}) string
        [GoFunc(IsVariadic = true)]
        public static string URLQueryEscaper(params object?[] args) => "";
    }

    [GoType("struct", Name = "Template", Package = "html/template")]
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

        // Promoted field from *parse.Tree (for html/template compatibility)
        [GoField(Name = "Root")] public object? Root;
        [GoField(Name = "Mode")] public long Mode;

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

    // Named string types
    [GoType("named", Name = "HTML", Package = "html/template", Underlying = "string")]
    public struct GoHTML { public string Value; }

    [GoType("named", Name = "URL", Package = "html/template", Underlying = "string")]
    public struct GoURL { public string Value; }

    [GoType("named", Name = "JS", Package = "html/template", Underlying = "string")]
    public struct GoJS { public string Value; }

    [GoType("named", Name = "CSS", Package = "html/template", Underlying = "string")]
    public struct GoCSS { public string Value; }

    [GoType("named", Name = "HTMLAttr", Package = "html/template", Underlying = "string")]
    public struct GoHTMLAttr { public string Value; }

    [GoType("named", Name = "JSStr", Package = "html/template", Underlying = "string")]
    public struct GoJSStr { public string Value; }

    [GoType("named", Name = "Srcset", Package = "html/template", Underlying = "string")]
    public struct GoSrcset { public string Value; }
}

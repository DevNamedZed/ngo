using System;
using System.Collections.Generic;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Html.Template
{
    [GoPackage("html/template")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*template.Template")]
        public static GoTemplate New(string name) => new GoTemplate(name);

        [GoFunc]
        [return: GoReturn("*template.Template")]
        public static GoTemplate Must(GoTemplate? t, object? err)
        {
            if (err != null)
            {
                throw new GoPanicException($"template: {err}");
            }
            return t ?? new GoTemplate("");
        }

        [GoFunc]
        public static void HTMLEscape([GoParam("io.Writer")] object writer, [GoParam("[]byte")] Slice<byte> data)
        {
            var escaped = System.Net.WebUtility.HtmlEncode(
                System.Text.Encoding.UTF8.GetString(data.AsSpan()));
            if (writer is Ngo.Runtime.Io.IGoWriter goWriter)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(escaped);
                goWriter.Write(new Slice<byte>(bytes));
            }
        }

        [GoFunc]
        public static string HTMLEscapeString(string s) => System.Net.WebUtility.HtmlEncode(s) ?? s;

        [GoFunc(IsVariadic = true)]
        public static string HTMLEscaper(params object?[] args)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var arg in args)
            {
                sb.Append(System.Net.WebUtility.HtmlEncode(arg?.ToString() ?? ""));
            }
            return sb.ToString();
        }

        [GoFunc]
        public static string JSEscapeString(string s) => s;

        [GoFunc(IsVariadic = true)]
        public static string URLQueryEscaper(params object?[] args)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var arg in args)
            {
                sb.Append(Uri.EscapeDataString(arg?.ToString() ?? ""));
            }
            return sb.ToString();
        }
    }

    [GoType("struct", Name = "Template", Package = "html/template")]
    public class GoTemplate
    {
        private readonly Text.Template.GoTemplate _inner;

        public GoTemplate() : this("") { }

        internal GoTemplate(string name)
        {
            _inner = new Text.Template.GoTemplate(name);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Execute(object? wr, object? data)
        {
            return _inner.Execute(wr, data);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? ExecuteTemplate(object? wr, string name, object? data)
        {
            return _inner.ExecuteTemplate(wr, name, data);
        }

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) Parse(string text)
        {
            _inner.Parse(text);
            return (this, null);
        }

        [GoMethod]
        public string Name() => _inner.Name();

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate Funcs(object? funcMap)
        {
            _inner.Funcs(funcMap);
            return this;
        }

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate Option(Slice<string> opt) => this;

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) ParseFiles(Slice<string> filenames)
        {
            var (_, err) = _inner.ParseFiles(filenames);
            return (this, err);
        }

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) ParseGlob(string pattern) => (this, null);

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate? Lookup(string name)
        {
            var inner = _inner.Lookup(name);
            if (inner == null)
            {
                return null;
            }
            var tmpl = new GoTemplate(name);
            tmpl._inner._templateText = inner._templateText;
            tmpl._inner._funcMap = inner._funcMap;
            tmpl._inner._namedTemplates = inner._namedTemplates;
            return tmpl;
        }

        [GoMethod]
        [return: GoReturn("[]*template.Template")]
        public Slice<GoTemplate> Templates()
        {
            var innerTemplates = _inner.Templates();
            var result = new GoTemplate[innerTemplates.Len];
            for (int i = 0; i < innerTemplates.Len; i++)
            {
                result[i] = new GoTemplate(innerTemplates[i].Name());
            }
            return new Slice<GoTemplate>(result);
        }

        [GoField(Name = "Tree", Type = "*parse.Tree", Embedded = true)]
        public object? Tree;

        [GoField(Name = "Root")] public object? Root;
        [GoField(Name = "Mode")] public long Mode;

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) AddParseTree(string name, object? tree) => (this, null);

        [GoMethod]
        public string DefinedTemplates() => _inner.DefinedTemplates();

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) Clone()
        {
            var (clonedInner, err) = _inner.Clone();
            var clone = new GoTemplate(_inner._name);
            return (clone, err);
        }

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate New(string name)
        {
            var tmpl = new GoTemplate(name);
            tmpl._inner._funcMap = _inner._funcMap;
            tmpl._inner._namedTemplates = _inner._namedTemplates;
            return tmpl;
        }

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate Delims(string left, string right)
        {
            _inner.Delims(left, right);
            return this;
        }
    }

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

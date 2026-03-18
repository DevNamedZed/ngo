using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Text.Template
{
    [GoPackage("text/template")]
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

        [GoFunc(IsVariadic = true)]
        [return: GoReturn("*template.Template", "error")]
        public static (GoTemplate, object?) ParseFiles(params string[] filenames)
        {
            var tmpl = new GoTemplate("");
            foreach (var file in filenames)
            {
                try
                {
                    var content = System.IO.File.ReadAllText(file);
                    var name = System.IO.Path.GetFileName(file);
                    if (string.IsNullOrEmpty(tmpl._name))
                    {
                        tmpl._name = name;
                    }
                    tmpl._namedTemplates[name] = content;
                    if (string.IsNullOrEmpty(tmpl._templateText))
                    {
                        tmpl._templateText = content;
                    }
                }
                catch (Exception ex)
                {
                    return (tmpl, ex.Message);
                }
            }
            return (tmpl, null);
        }

        [GoFunc]
        [return: GoReturn("*template.Template", "error")]
        public static (GoTemplate, object?) ParseGlob(string pattern) => (new GoTemplate(""), null);

        [GoFunc]
        public static void HTMLEscape([GoParam("io.Writer")] object? w, Slice<byte> b)
        {
            if (w is IGoWriter writer)
            {
                var escaped = System.Net.WebUtility.HtmlEncode(System.Text.Encoding.UTF8.GetString(b.AsSpan()));
                writer.Write(new Slice<byte>(System.Text.Encoding.UTF8.GetBytes(escaped)));
            }
        }

        [GoFunc]
        public static string HTMLEscapeString(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        [GoFunc]
        public static string HTMLEscaper(params object[] args)
        {
            var sb = new StringBuilder();
            foreach (var arg in args)
            {
                sb.Append(System.Net.WebUtility.HtmlEncode(arg?.ToString() ?? ""));
            }
            return sb.ToString();
        }

        [GoFunc]
        public static void JSEscape([GoParam("io.Writer")] object? w, Slice<byte> b) { }

        [GoFunc]
        public static string JSEscapeString(string s) => s ?? "";

        [GoFunc]
        public static string JSEscaper(params object[] args) => "";

        [GoFunc]
        public static string URLQueryEscaper(params object[] args)
        {
            var sb = new StringBuilder();
            foreach (var arg in args)
            {
                sb.Append(Uri.EscapeDataString(arg?.ToString() ?? ""));
            }
            return sb.ToString();
        }

        [GoFunc]
        [return: GoReturn("bool", "bool")]
        public static (bool, bool) IsTrue(object? val)
        {
            if (val == null)
            {
                return (false, true);
            }
            if (val is bool b)
            {
                return (b, true);
            }
            if (val is long l)
            {
                return (l != 0, true);
            }
            if (val is int i)
            {
                return (i != 0, true);
            }
            if (val is double d)
            {
                return (d != 0, true);
            }
            if (val is string s)
            {
                return (!string.IsNullOrEmpty(s), true);
            }
            return (true, true);
        }
    }

    [GoType("named", Name = "FuncMap", Package = "text/template", Underlying = "map[string]interface{}")]
    public class GoFuncMap : Dictionary<string, object?>
    {
    }

    [GoType("struct", Name = "Template", Package = "text/template")]
    public class GoTemplate
    {
        internal string _name;
        internal string _templateText = "";
        internal string _leftDelim = "{{";
        internal string _rightDelim = "}}";
        internal Dictionary<string, object?> _funcMap = new Dictionary<string, object?>();
        internal Dictionary<string, string> _namedTemplates = new Dictionary<string, string>();

        [GoField(Name = "Tree", Type = "*parse.Tree", Embedded = true)]
        public object? Tree;

        [GoField(Name = "Root")] public object? Root;
        [GoField(Name = "Mode")] public long Mode;
        [GoField(Name = "ParseName")] public string ParseName = "";

        public GoTemplate() : this("") { }

        internal GoTemplate(string name)
        {
            _name = name;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Execute(object? wr, object? data)
        {
            if (wr == null)
            {
                return "template: nil writer";
            }
            try
            {
                var output = TemplateEngine.Execute(_templateText, data, _leftDelim, _rightDelim, _funcMap, _namedTemplates);
                if (wr is IGoWriter writer)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(output);
                    writer.Write(new Slice<byte>(bytes));
                }
                return null;
            }
            catch (Exception ex)
            {
                return $"template: {ex.Message}";
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? ExecuteTemplate(object? wr, string name, object? data)
        {
            if (_namedTemplates.TryGetValue(name, out var tmplText))
            {
                var saved = _templateText;
                _templateText = tmplText;
                var err = Execute(wr, data);
                _templateText = saved;
                return err;
            }
            return $"template: \"{name}\" is undefined";
        }

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) Parse(string text)
        {
            _templateText = text;
            return (this, null);
        }

        [GoMethod]
        public string Name() => _name;

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate Funcs(object? funcMap)
        {
            if (funcMap is GoFuncMap fm)
            {
                foreach (var kv in fm)
                {
                    _funcMap[kv.Key] = kv.Value;
                }
            }
            else if (funcMap is IDictionary dict)
            {
                foreach (DictionaryEntry kv in dict)
                {
                    _funcMap[kv.Key?.ToString() ?? ""] = kv.Value;
                }
            }
            return this;
        }

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate Option(Slice<string> opt) => this;

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) ParseFiles(Slice<string> filenames)
        {
            for (int i = 0; i < filenames.Len; i++)
            {
                try
                {
                    var content = System.IO.File.ReadAllText(filenames[i]);
                    var name = System.IO.Path.GetFileName(filenames[i]);
                    _namedTemplates[name] = content;
                    if (string.IsNullOrEmpty(_templateText))
                    {
                        _templateText = content;
                    }
                }
                catch (Exception ex)
                {
                    return (this, ex.Message);
                }
            }
            return (this, null);
        }

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) ParseGlob(string pattern) => (this, null);

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate? Lookup(string name)
        {
            if (_namedTemplates.ContainsKey(name))
            {
                var tmpl = new GoTemplate(name);
                tmpl._templateText = _namedTemplates[name];
                tmpl._funcMap = _funcMap;
                tmpl._namedTemplates = _namedTemplates;
                return tmpl;
            }
            return null;
        }

        [GoMethod]
        [return: GoReturn("[]*template.Template")]
        public Slice<GoTemplate> Templates()
        {
            var list = new List<GoTemplate>();
            foreach (var kv in _namedTemplates)
            {
                var t = new GoTemplate(kv.Key);
                t._templateText = kv.Value;
                list.Add(t);
            }
            return new Slice<GoTemplate>(list.ToArray());
        }

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) AddParseTree(string name, object? tree) => (this, null);

        [GoMethod]
        public string DefinedTemplates()
        {
            if (_namedTemplates.Count == 0)
            {
                return "";
            }
            var sb = new StringBuilder("; defined templates are: ");
            bool first = true;
            foreach (var name in _namedTemplates.Keys)
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                sb.Append('"');
                sb.Append(name);
                sb.Append('"');
                first = false;
            }
            return sb.ToString();
        }

        [GoMethod]
        [return: GoReturn("*template.Template", "error")]
        public (GoTemplate, object?) Clone()
        {
            var clone = new GoTemplate(_name);
            clone._templateText = _templateText;
            clone._leftDelim = _leftDelim;
            clone._rightDelim = _rightDelim;
            clone._funcMap = new Dictionary<string, object?>(_funcMap);
            clone._namedTemplates = new Dictionary<string, string>(_namedTemplates);
            return (clone, null);
        }

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate New(string name)
        {
            var tmpl = new GoTemplate(name);
            tmpl._funcMap = _funcMap;
            tmpl._namedTemplates = _namedTemplates;
            tmpl._leftDelim = _leftDelim;
            tmpl._rightDelim = _rightDelim;
            return tmpl;
        }

        [GoMethod]
        [return: GoReturn("*template.Template")]
        public GoTemplate Delims(string left, string right)
        {
            _leftDelim = left;
            _rightDelim = right;
            return this;
        }
    }

    internal static class TemplateEngine
    {
        public static string Execute(string template, object? data, string leftDelim, string rightDelim,
            Dictionary<string, object?> funcMap, Dictionary<string, string> namedTemplates)
        {
            var sb = new StringBuilder();
            int pos = 0;

            while (pos < template.Length)
            {
                int actionStart = template.IndexOf(leftDelim, pos, StringComparison.Ordinal);
                if (actionStart < 0)
                {
                    sb.Append(template.Substring(pos));
                    break;
                }

                sb.Append(template.Substring(pos, actionStart - pos));
                int actionEnd = template.IndexOf(rightDelim, actionStart + leftDelim.Length, StringComparison.Ordinal);
                if (actionEnd < 0)
                {
                    sb.Append(template.Substring(pos));
                    break;
                }

                string action = template.Substring(actionStart + leftDelim.Length, actionEnd - actionStart - leftDelim.Length).Trim();
                pos = actionEnd + rightDelim.Length;

                // Handle actions
                if (action.StartsWith("if "))
                {
                    string condition = action.Substring(3).Trim();
                    pos = ExecuteIf(template, pos, leftDelim, rightDelim, condition, data, funcMap, namedTemplates, sb);
                }
                else if (action.StartsWith("range "))
                {
                    string expr = action.Substring(6).Trim();
                    pos = ExecuteRange(template, pos, leftDelim, rightDelim, expr, data, funcMap, namedTemplates, sb);
                }
                else if (action.StartsWith("with "))
                {
                    string expr = action.Substring(5).Trim();
                    pos = ExecuteWith(template, pos, leftDelim, rightDelim, expr, data, funcMap, namedTemplates, sb);
                }
                else if (action.StartsWith("template "))
                {
                    ExecuteNamedTemplate(action.Substring(9).Trim(), data, namedTemplates, funcMap, sb);
                }
                else if (action.StartsWith("block "))
                {
                    // block is like define + template
                    pos = SkipBlock(template, pos, leftDelim, rightDelim, "block");
                }
                else if (action.StartsWith("define "))
                {
                    pos = SkipBlock(template, pos, leftDelim, rightDelim, "define");
                }
                else if (action == "else" || action == "end" || action.StartsWith("else if"))
                {
                    // Shouldn't reach here — handled by if/range/with
                }
                else if (action.StartsWith("- ") || action.EndsWith(" -"))
                {
                    // Trim whitespace markers — evaluate inner expression
                    string innerAction = action.Trim('-').Trim();
                    sb.Append(EvaluateExpression(innerAction, data, funcMap));
                }
                else
                {
                    sb.Append(EvaluateExpression(action, data, funcMap));
                }
            }

            return sb.ToString();
        }

        private static string EvaluateExpression(string expr, object? data, Dictionary<string, object?> funcMap)
        {
            if (string.IsNullOrEmpty(expr))
            {
                return "";
            }

            // Handle pipe: expr | funcName
            int pipeIdx = expr.LastIndexOf('|');
            if (pipeIdx > 0)
            {
                string left = expr.Substring(0, pipeIdx).Trim();
                string funcName = expr.Substring(pipeIdx + 1).Trim();
                var value = EvaluateExpression(left, data, funcMap);
                return ApplyFunc(funcName, value, funcMap);
            }

            // Handle built-in comparison functions: eq, ne, lt, le, gt, ge
            if (expr.StartsWith("eq ") || expr.StartsWith("ne ") || expr.StartsWith("lt ") ||
                expr.StartsWith("le ") || expr.StartsWith("gt ") || expr.StartsWith("ge "))
            {
                string funcName = expr.Substring(0, 2);
                string args = expr.Substring(3).Trim();
                return EvaluateBuiltinComparison(funcName, args, data, funcMap);
            }

            // Handle built-in logical functions: and, or, not
            if (expr.StartsWith("and "))
            {
                var args = SplitTemplateArgs(expr.Substring(4));
                foreach (var arg in args)
                {
                    string val = EvaluateExpression(arg, data, funcMap);
                    var (truthy, _) = Package.IsTrue(val == "true" ? (object)true : val == "false" ? false : val);
                    if (!truthy)
                    {
                        return val;
                    }
                }
                return args.Count > 0 ? EvaluateExpression(args[args.Count - 1], data, funcMap) : "";
            }
            if (expr.StartsWith("or "))
            {
                var args = SplitTemplateArgs(expr.Substring(3));
                foreach (var arg in args)
                {
                    string val = EvaluateExpression(arg, data, funcMap);
                    var (truthy, _) = Package.IsTrue(val == "true" ? (object)true : val == "false" ? false : val);
                    if (truthy)
                    {
                        return val;
                    }
                }
                return args.Count > 0 ? EvaluateExpression(args[args.Count - 1], data, funcMap) : "";
            }
            if (expr.StartsWith("not "))
            {
                string val = EvaluateExpression(expr.Substring(4).Trim(), data, funcMap);
                var (truthy, _) = Package.IsTrue(val == "true" ? (object)true : val == "false" ? false : val);
                return truthy ? "false" : "true";
            }

            // Handle index function: index .Array 0
            if (expr.StartsWith("index "))
            {
                var args = SplitTemplateArgs(expr.Substring(6));
                if (args.Count >= 2)
                {
                    var collection = ResolveDotExpr(args[0].TrimStart('.'), data);
                    string indexStr = EvaluateExpression(args[1], data, funcMap);
                    if (collection != null && int.TryParse(indexStr, out int idx))
                    {
                        var indexer = collection.GetType().GetProperty("Item");
                        if (indexer != null)
                        {
                            var result = indexer.GetValue(collection, new object[] { idx });
                            return FormatValue(result);
                        }
                    }
                }
                return "";
            }

            // Handle len function: len .Slice
            if (expr.StartsWith("len "))
            {
                var arg = EvaluateExpression(expr.Substring(4).Trim(), data, funcMap);
                return ComputeLen(arg);
            }

            // Handle printf function: printf "format" args...
            if (expr.StartsWith("printf "))
            {
                var args = SplitTemplateArgs(expr.Substring(7));
                if (args.Count >= 1)
                {
                    string format = args[0].Trim('"');
                    var fmtArgs = new object[args.Count - 1];
                    for (int idx = 1; idx < args.Count; idx++)
                    {
                        fmtArgs[idx - 1] = EvaluateExpression(args[idx], data, funcMap);
                    }
                    return Fmt.Package.Sprintf(format, fmtArgs);
                }
                return "";
            }

            // Handle function call: funcName arg1 arg2
            if (!expr.StartsWith(".") && !expr.StartsWith("$") && expr.Contains(" "))
            {
                var parts = expr.Split(new[] { ' ' }, 2);
                if (funcMap.ContainsKey(parts[0]))
                {
                    return ApplyFunc(parts[0], EvaluateExpression(parts[1].Trim(), data, funcMap), funcMap);
                }
            }

            // Dot expression
            if (expr == ".")
            {
                return FormatValue(data);
            }

            if (expr.StartsWith("."))
            {
                return FormatValue(ResolveDotExpr(expr.Substring(1), data));
            }

            // Quoted string
            if (expr.StartsWith("\"") && expr.EndsWith("\""))
            {
                return expr.Substring(1, expr.Length - 2);
            }

            // Number
            if (long.TryParse(expr, out var num))
            {
                return num.ToString();
            }

            // Boolean
            if (expr == "true")
            {
                return "true";
            }
            if (expr == "false")
            {
                return "false";
            }
            if (expr == "nil")
            {
                return "<nil>";
            }

            // Try as function with no args
            if (funcMap.ContainsKey(expr))
            {
                return ApplyFunc(expr, null!, funcMap);
            }

            return FormatValue(ResolveDotExpr(expr, data));
        }

        private static object? ResolveDotExpr(string path, object? data)
        {
            if (data == null || string.IsNullOrEmpty(path))
            {
                return data;
            }

            var parts = path.Split('.');
            object? current = data;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }
                if (current == null)
                {
                    return null;
                }

                var type = current.GetType();

                // Try field
                var field = type.GetField(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    current = field.GetValue(current);
                    continue;
                }

                // Try property
                var prop = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop != null)
                {
                    current = prop.GetValue(current);
                    continue;
                }

                // Try method with no args
                var method = type.GetMethod(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    current = method.Invoke(current, null);
                    continue;
                }

                // Try GoField attribute
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attr = f.GetCustomAttribute<GoFieldAttribute>();
                    if (attr != null && attr.Name == part)
                    {
                        current = f.GetValue(current);
                        goto next;
                    }
                }

                return null;
                next:;
            }
            return current;
        }

        private static string FormatValue(object? value)
        {
            if (value == null)
            {
                return "";
            }
            return Fmt.Package.Sprint(value);
        }

        private static string ApplyFunc(string funcName, string arg, Dictionary<string, object?> funcMap)
        {
            if (funcMap.TryGetValue(funcName, out var fn) && fn != null)
            {
                try
                {
                    var invokeMethod = fn.GetType().GetMethod("Invoke");
                    if (invokeMethod != null)
                    {
                        var result = invokeMethod.Invoke(fn, new object[] { arg });
                        return result?.ToString() ?? "";
                    }
                }
                catch
                {
                    return arg;
                }
            }
            // Go template built-in functions
            return funcName switch
            {
                "len" => ComputeLen(arg),
                "print" => arg,
                "printf" => arg,
                "println" => arg + "\n",
                "html" => System.Net.WebUtility.HtmlEncode(arg),
                "urlquery" => Uri.EscapeDataString(arg),
                "js" => EscapeJS(arg),
                "not" => (arg == "" || arg == "false" || arg == "0" || arg == "<nil>") ? "true" : "false",
                _ => arg,
            };
        }

        private static string EvaluateBuiltinComparison(string funcName, string expr, object? data, Dictionary<string, object?> funcMap)
        {
            // Parse: eq .Field1 .Field2  or  eq .Field "literal"
            var parts = SplitTemplateArgs(expr);
            if (parts.Count < 2)
            {
                return "false";
            }

            string leftStr = EvaluateExpression(parts[0], data, funcMap);
            string rightStr = EvaluateExpression(parts[1], data, funcMap);

            // Try numeric comparison
            bool leftIsNum = double.TryParse(leftStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double leftNum);
            bool rightIsNum = double.TryParse(rightStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double rightNum);

            return funcName switch
            {
                "eq" => (leftStr == rightStr) ? "true" : "false",
                "ne" => (leftStr != rightStr) ? "true" : "false",
                "lt" => (leftIsNum && rightIsNum) ? (leftNum < rightNum ? "true" : "false") :
                        (string.Compare(leftStr, rightStr, StringComparison.Ordinal) < 0 ? "true" : "false"),
                "le" => (leftIsNum && rightIsNum) ? (leftNum <= rightNum ? "true" : "false") :
                        (string.Compare(leftStr, rightStr, StringComparison.Ordinal) <= 0 ? "true" : "false"),
                "gt" => (leftIsNum && rightIsNum) ? (leftNum > rightNum ? "true" : "false") :
                        (string.Compare(leftStr, rightStr, StringComparison.Ordinal) > 0 ? "true" : "false"),
                "ge" => (leftIsNum && rightIsNum) ? (leftNum >= rightNum ? "true" : "false") :
                        (string.Compare(leftStr, rightStr, StringComparison.Ordinal) >= 0 ? "true" : "false"),
                _ => "false",
            };
        }

        private static List<string> SplitTemplateArgs(string expr)
        {
            var result = new List<string>();
            int i = 0;
            while (i < expr.Length)
            {
                // Skip whitespace
                while (i < expr.Length && expr[i] == ' ')
                {
                    i++;
                }
                if (i >= expr.Length)
                {
                    break;
                }

                // Quoted string
                if (expr[i] == '"')
                {
                    int end = expr.IndexOf('"', i + 1);
                    if (end > 0)
                    {
                        result.Add(expr.Substring(i, end - i + 1));
                        i = end + 1;
                    }
                    else
                    {
                        result.Add(expr.Substring(i));
                        break;
                    }
                }
                else
                {
                    // Token until next space
                    int start = i;
                    while (i < expr.Length && expr[i] != ' ')
                    {
                        i++;
                    }
                    result.Add(expr.Substring(start, i - start));
                }
            }
            return result;
        }

        private static string ComputeLen(string arg)
        {
            if (long.TryParse(arg, out _))
            {
                return arg; // Already a number (shouldn't happen, but safe)
            }
            return arg.Length.ToString();
        }

        private static string EscapeJS(string s)
        {
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("'", "\\'")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t")
                    .Replace("<", "\\u003c")
                    .Replace(">", "\\u003e")
                    .Replace("&", "\\u0026");
        }

        private static int ExecuteIf(string template, int pos, string leftDelim, string rightDelim,
            string condition, object? data, Dictionary<string, object?> funcMap,
            Dictionary<string, string> namedTemplates, StringBuilder sb)
        {
            var (trueBody, elseBody, endPos) = ExtractIfBlock(template, pos, leftDelim, rightDelim);

            var condValue = ResolveDotExpr(condition.TrimStart('.'), data);
            var (isTruthy, _) = Package.IsTrue(condValue);

            if (isTruthy)
            {
                sb.Append(Execute(trueBody, data, leftDelim, rightDelim, funcMap, namedTemplates));
            }
            else if (elseBody != null)
            {
                sb.Append(Execute(elseBody, data, leftDelim, rightDelim, funcMap, namedTemplates));
            }

            return endPos;
        }

        private static int ExecuteRange(string template, int pos, string leftDelim, string rightDelim,
            string expr, object? data, Dictionary<string, object?> funcMap,
            Dictionary<string, string> namedTemplates, StringBuilder sb)
        {
            var (body, elseBody, endPos) = ExtractIfBlock(template, pos, leftDelim, rightDelim);

            var collection = ResolveDotExpr(expr.TrimStart('.'), data);
            bool hasItems = false;

            if (collection is IEnumerable enumerable && collection is not string)
            {
                foreach (var item in enumerable)
                {
                    hasItems = true;
                    sb.Append(Execute(body, item, leftDelim, rightDelim, funcMap, namedTemplates));
                }
            }

            if (!hasItems && elseBody != null)
            {
                sb.Append(Execute(elseBody, data, leftDelim, rightDelim, funcMap, namedTemplates));
            }

            return endPos;
        }

        private static int ExecuteWith(string template, int pos, string leftDelim, string rightDelim,
            string expr, object? data, Dictionary<string, object?> funcMap,
            Dictionary<string, string> namedTemplates, StringBuilder sb)
        {
            var (body, elseBody, endPos) = ExtractIfBlock(template, pos, leftDelim, rightDelim);

            var value = ResolveDotExpr(expr.TrimStart('.'), data);
            var (isTruthy, _) = Package.IsTrue(value);

            if (isTruthy)
            {
                sb.Append(Execute(body, value, leftDelim, rightDelim, funcMap, namedTemplates));
            }
            else if (elseBody != null)
            {
                sb.Append(Execute(elseBody, data, leftDelim, rightDelim, funcMap, namedTemplates));
            }

            return endPos;
        }

        private static void ExecuteNamedTemplate(string args, object? data,
            Dictionary<string, string> namedTemplates, Dictionary<string, object?> funcMap, StringBuilder sb)
        {
            // Parse: "name" .Data or "name"
            string name = "";
            if (args.StartsWith("\""))
            {
                int endQuote = args.IndexOf('"', 1);
                if (endQuote > 0)
                {
                    name = args.Substring(1, endQuote - 1);
                }
            }

            if (namedTemplates.TryGetValue(name, out var tmplText))
            {
                sb.Append(Execute(tmplText, data, "{{", "}}", funcMap, namedTemplates));
            }
        }

        private static (string body, string? elseBody, int endPos) ExtractIfBlock(
            string template, int pos, string leftDelim, string rightDelim)
        {
            int depth = 1;
            int bodyStart = pos;
            int elsePos = -1;

            while (pos < template.Length && depth > 0)
            {
                int nextAction = template.IndexOf(leftDelim, pos, StringComparison.Ordinal);
                if (nextAction < 0)
                {
                    break;
                }

                int actionEnd = template.IndexOf(rightDelim, nextAction + leftDelim.Length, StringComparison.Ordinal);
                if (actionEnd < 0)
                {
                    break;
                }

                string action = template.Substring(nextAction + leftDelim.Length, actionEnd - nextAction - leftDelim.Length).Trim();
                int afterAction = actionEnd + rightDelim.Length;

                if (action.StartsWith("if ") || action.StartsWith("range ") || action.StartsWith("with ") || action.StartsWith("block ") || action.StartsWith("define "))
                {
                    depth++;
                }
                else if (action == "end")
                {
                    depth--;
                    if (depth == 0)
                    {
                        string body;
                        string? elseBody = null;
                        if (elsePos >= 0)
                        {
                            body = template.Substring(bodyStart, elsePos - bodyStart);
                            elseBody = template.Substring(elsePos, nextAction - elsePos);
                        }
                        else
                        {
                            body = template.Substring(bodyStart, nextAction - bodyStart);
                        }
                        return (body, elseBody, afterAction);
                    }
                }
                else if (action == "else" && depth == 1)
                {
                    elsePos = afterAction;
                }

                pos = afterAction;
            }

            return (template.Substring(bodyStart), null, template.Length);
        }

        private static int SkipBlock(string template, int pos, string leftDelim, string rightDelim, string blockType)
        {
            int depth = 1;
            while (pos < template.Length && depth > 0)
            {
                int nextAction = template.IndexOf(leftDelim, pos, StringComparison.Ordinal);
                if (nextAction < 0)
                {
                    break;
                }
                int actionEnd = template.IndexOf(rightDelim, nextAction + leftDelim.Length, StringComparison.Ordinal);
                if (actionEnd < 0)
                {
                    break;
                }
                string action = template.Substring(nextAction + leftDelim.Length, actionEnd - nextAction - leftDelim.Length).Trim();
                pos = actionEnd + rightDelim.Length;

                if (action.StartsWith("if ") || action.StartsWith("range ") || action.StartsWith("with ") || action.StartsWith("block ") || action.StartsWith("define "))
                {
                    depth++;
                }
                else if (action == "end")
                {
                    depth--;
                }
            }
            return pos;
        }
    }
}

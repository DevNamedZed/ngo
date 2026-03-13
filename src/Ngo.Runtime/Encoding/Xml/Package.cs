using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Encoding.Xml
{
    [GoPackage("encoding/xml")]
    public static class Package
    {
        // xml.Header constant
        [GoConst(Type = "string")]
        public const string Header = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";

        // xml.Marshal(v any) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) Marshal(object? v)
        {
            try
            {
                var xml = MarshalToString(v, "", "");
                var bytes = System.Text.Encoding.UTF8.GetBytes(xml);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), ex.Message);
            }
        }

        // xml.MarshalIndent(v any, prefix, indent string) ([]byte, error)
        [GoFunc]
        [return: GoReturn("[]byte", "error")]
        public static (Slice<byte>, object?) MarshalIndent(object? v, string prefix, string indent)
        {
            try
            {
                var xml = MarshalToString(v, prefix, indent);
                var bytes = System.Text.Encoding.UTF8.GetBytes(xml);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(), ex.Message);
            }
        }

        // xml.Unmarshal(data []byte, v any) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Unmarshal(Slice<byte> data, object? v)
        {
            if (v == null)
            {
                return "xml: Unmarshal target is nil";
            }
            try
            {
                var bytes = new byte[data.Len];
                for (int i = 0; i < data.Len; i++)
                {
                    bytes[i] = data[i];
                }
                var xmlStr = System.Text.Encoding.UTF8.GetString(bytes);
                var doc = XDocument.Parse(xmlStr);
                PopulateFromXml(doc.Root!, v);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // xml.NewEncoder(w io.Writer) *Encoder
        [GoFunc]
        [return: GoReturn("*xml.Encoder")]
        public static GoEncoder NewEncoder([GoParam("io.Writer")] object? w)
        {
            return new GoEncoder(w as IGoWriter);
        }

        // xml.NewDecoder(r io.Reader) *Decoder
        [GoFunc]
        [return: GoReturn("*xml.Decoder")]
        public static GoDecoder NewDecoder([GoParam("io.Reader")] object? r)
        {
            return new GoDecoder(r as IGoReader);
        }

        // xml.Escape(w io.Writer, s []byte)
        [GoFunc]
        public static void Escape([GoParam("io.Writer")] object? w, Slice<byte> s)
        {
            if (w is not IGoWriter writer)
            {
                return;
            }
            var text = SliceToString(s);
            var escaped = System.Security.SecurityElement.Escape(text) ?? text;
            var bytes = System.Text.Encoding.UTF8.GetBytes(escaped);
            writer.Write(new Slice<byte>(bytes));
        }

        // xml.EscapeText(w io.Writer, s []byte) error
        [GoFunc]
        [return: GoReturn("error")]
        public static object? EscapeText([GoParam("io.Writer")] object? w, Slice<byte> s)
        {
            Escape(w, s);
            return null;
        }

        // xml.CopyToken(t Token) Token
        [GoFunc]
        [return: GoReturn("xml.Token")]
        public static object? CopyToken([GoParam("xml.Token")] object? t) => t;

        // xml.Token interface (empty - it's a union type marker)
        [GoType("interface", Name = "Token", Package = "encoding/xml")]
        public interface IToken { }

        // xml.Marshaler interface
        [GoType("interface", Name = "Marshaler", Package = "encoding/xml")]
        public interface IMarshaler
        {
            [GoMethod]
            [return: GoReturn("[]byte", "error")]
            (Slice<byte>, object?) MarshalXML([GoParam("*xml.Encoder")] GoEncoder? e, GoStartElement start);
        }

        // xml.Unmarshaler interface
        [GoType("interface", Name = "Unmarshaler", Package = "encoding/xml")]
        public interface IUnmarshaler
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? UnmarshalXML([GoParam("*xml.Decoder")] GoDecoder? d, GoStartElement start);
        }

        // xml.MarshalerAttr interface
        [GoType("interface", Name = "MarshalerAttr", Package = "encoding/xml")]
        public interface IMarshalerAttr
        {
            [GoMethod]
            [return: GoReturn("xml.Attr", "error")]
            (GoAttr, object?) MarshalXMLAttr(GoName name);
        }

        // xml.UnmarshalerAttr interface
        [GoType("interface", Name = "UnmarshalerAttr", Package = "encoding/xml")]
        public interface IUnmarshalerAttr
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? UnmarshalXMLAttr(GoAttr attr);
        }

        // xml.TokenReader interface
        [GoType("interface", Name = "TokenReader", Package = "encoding/xml")]
        public interface ITokenReader
        {
            [GoMethod]
            [return: GoReturn("xml.Token", "error")]
            (object?, object?) Token();
        }

        private static string MarshalToString(object? v, string prefix, string indent)
        {
            if (v == null)
            {
                return "";
            }

            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };
            if (!string.IsNullOrEmpty(indent))
            {
                settings.Indent = true;
                settings.IndentChars = indent;
            }

            using (var writer = XmlWriter.Create(sb, settings))
            {
                var type = v.GetType();
                string elementName = GetXmlElementName(type);
                WriteElement(writer, elementName, v, type);
            }

            return sb.ToString();
        }

        private static void WriteElement(XmlWriter writer, string name, object? value, Type type)
        {
            if (value == null)
            {
                return;
            }

            writer.WriteStartElement(name);

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var (xmlName, isAttr, isCharData, isInnerXml, omitEmpty, skip) = GetXmlFieldInfo(field);
                if (skip)
                {
                    continue;
                }

                var fieldValue = field.GetValue(value);
                if (omitEmpty && IsEmpty(fieldValue))
                {
                    continue;
                }

                if (isAttr)
                {
                    writer.WriteAttributeString(xmlName, fieldValue?.ToString() ?? "");
                }
                else if (isCharData)
                {
                    // Will write as text content after all child elements
                }
                else if (isInnerXml)
                {
                    if (fieldValue != null)
                    {
                        writer.WriteRaw(fieldValue.ToString() ?? "");
                    }
                }
                else if (IsSimpleType(field.FieldType))
                {
                    writer.WriteElementString(xmlName, fieldValue?.ToString() ?? "");
                }
                else if (fieldValue != null)
                {
                    WriteElement(writer, xmlName, fieldValue, field.FieldType);
                }
            }

            // Write chardata fields
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var (_, _, isCharData, _, _, skip) = GetXmlFieldInfo(field);
                if (!skip && isCharData)
                {
                    var fieldValue = field.GetValue(value);
                    if (fieldValue != null)
                    {
                        writer.WriteString(fieldValue.ToString() ?? "");
                    }
                }
            }

            writer.WriteEndElement();
        }

        private static void PopulateFromXml(XElement elem, object target)
        {
            var type = target.GetType();

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var (xmlName, isAttr, isCharData, isInnerXml, _, skip) = GetXmlFieldInfo(field);
                if (skip)
                {
                    continue;
                }

                if (isAttr)
                {
                    var attr = elem.Attribute(xmlName);
                    if (attr != null)
                    {
                        SetFieldValue(field, target, attr.Value);
                    }
                }
                else if (isCharData)
                {
                    SetFieldValue(field, target, elem.Value);
                }
                else if (isInnerXml)
                {
                    using var reader = elem.CreateReader();
                    reader.MoveToContent();
                    SetFieldValue(field, target, reader.ReadInnerXml());
                }
                else
                {
                    var child = elem.Element(xmlName);
                    if (child != null)
                    {
                        if (IsSimpleType(field.FieldType))
                        {
                            SetFieldValue(field, target, child.Value);
                        }
                        else
                        {
                            var childObj = field.GetValue(target);
                            if (childObj == null)
                            {
                                childObj = Activator.CreateInstance(field.FieldType);
                                if (childObj != null)
                                {
                                    field.SetValue(target, childObj);
                                }
                            }
                            if (childObj != null)
                            {
                                PopulateFromXml(child, childObj);
                            }
                        }
                    }
                }
            }
        }

        private static string GetXmlElementName(Type type)
        {
            var goType = type.GetCustomAttribute<GoTypeAttribute>();
            if (goType != null && !string.IsNullOrEmpty(goType.Name))
            {
                return goType.Name;
            }
            return type.Name;
        }

        private static (string name, bool isAttr, bool isCharData, bool isInnerXml, bool omitEmpty, bool skip) GetXmlFieldInfo(FieldInfo field)
        {
            string name = field.Name;
            bool isAttr = false;
            bool isCharData = false;
            bool isInnerXml = false;
            bool omitEmpty = false;

            var tagAttr = field.GetCustomAttribute<GoTagAttribute>();
            if (tagAttr != null)
            {
                var xmlTag = ParseStructTag(tagAttr.Tag, "xml");
                if (xmlTag != null)
                {
                    if (xmlTag == "-")
                    {
                        return ("", false, false, false, false, true);
                    }
                    var parts = xmlTag.Split(',');
                    if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                    {
                        name = parts[0];
                    }
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (parts[i] == "attr")
                        {
                            isAttr = true;
                        }
                        else if (parts[i] == "chardata")
                        {
                            isCharData = true;
                        }
                        else if (parts[i] == "innerxml")
                        {
                            isInnerXml = true;
                        }
                        else if (parts[i] == "omitempty")
                        {
                            omitEmpty = true;
                        }
                    }
                }
            }

            // Default: lowercase first char for xml name
            if (name == field.Name)
            {
                var goField = field.GetCustomAttribute<GoFieldAttribute>();
                if (goField != null && !string.IsNullOrEmpty(goField.Name))
                {
                    name = goField.Name;
                }
            }

            return (name, isAttr, isCharData, isInnerXml, omitEmpty, false);
        }

        private static string? ParseStructTag(string tag, string key)
        {
            var search = key + ":\"";
            int idx = tag.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0)
            {
                return null;
            }
            int start = idx + search.Length;
            int end = tag.IndexOf('"', start);
            if (end < 0)
            {
                return null;
            }
            return tag.Substring(start, end - start);
        }

        private static bool IsSimpleType(Type t)
        {
            return t == typeof(string) || t == typeof(long) || t == typeof(int) ||
                   t == typeof(double) || t == typeof(float) || t == typeof(bool) ||
                   t == typeof(byte);
        }

        private static bool IsEmpty(object? value)
        {
            if (value == null)
            {
                return true;
            }
            if (value is string s)
            {
                return s.Length == 0;
            }
            if (value is long l)
            {
                return l == 0;
            }
            if (value is bool b)
            {
                return !b;
            }
            return false;
        }

        private static void SetFieldValue(FieldInfo field, object target, string textValue)
        {
            if (field.FieldType == typeof(string))
            {
                field.SetValue(target, textValue);
            }
            else if (field.FieldType == typeof(long))
            {
                if (long.TryParse(textValue, out long l))
                {
                    field.SetValue(target, l);
                }
            }
            else if (field.FieldType == typeof(int))
            {
                if (int.TryParse(textValue, out int i))
                {
                    field.SetValue(target, i);
                }
            }
            else if (field.FieldType == typeof(double))
            {
                if (double.TryParse(textValue, out double d))
                {
                    field.SetValue(target, d);
                }
            }
            else if (field.FieldType == typeof(bool))
            {
                if (bool.TryParse(textValue, out bool b))
                {
                    field.SetValue(target, b);
                }
            }
        }

        private static string SliceToString(Slice<byte> s)
        {
            var arr = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
            {
                arr[i] = s[i];
            }
            return System.Text.Encoding.UTF8.GetString(arr);
        }
    }
}

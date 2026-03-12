// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Json
{
    [GoPackage("encoding/json")]
    public static class Package
    {
        // json.Marshal(v interface{}) ([]byte, error)
        public static (Slice<byte>, object?) Marshal(object? v)
        {
            try
            {
                var jsonObj = ToJsonObject(v);
                var json = JsonSerializer.Serialize(jsonObj);
                var bytes = global::System.Text.Encoding.UTF8.GetBytes(json);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        // json.MarshalIndent(v interface{}, prefix, indent string) ([]byte, error)
        public static (Slice<byte>, object?) MarshalIndent(object? v, string prefix, string indent)
        {
            try
            {
                var jsonObj = ToJsonObject(v);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(jsonObj, options);
                // Go uses custom prefix/indent; apply basic indent substitution
                if (indent != "    ")
                {
                    json = json.Replace("    ", indent);
                }
                var bytes = global::System.Text.Encoding.UTF8.GetBytes(json);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        // json.NewDecoder(r io.Reader) *Decoder
        public static Decoder NewDecoder(object? r) { return new Decoder(); }

        // json.NewEncoder(w io.Writer) *Encoder
        public static Encoder NewEncoder(object? w) { return new Encoder(); }

        // json.HTMLEscape(dst *bytes.Buffer, src []byte)
        public static void HTMLEscape(object? dst, Slice<byte> src) { }

        // json.Compact(dst *bytes.Buffer, src []byte) error
        [return: GoReturn("error")]
        public static object? Compact(object? dst, Slice<byte> src) { return null; }

        // json.Indent(dst *bytes.Buffer, src []byte, prefix, indent string) error
        [return: GoReturn("error")]
        public static object? Indent(object? dst, Slice<byte> src, string prefix, string indent) { return null; }

        // json.Unmarshal(data []byte, v interface{}) error
        public static object? Unmarshal(Slice<byte> data, object v)
        {
            try
            {
                var bytes = new byte[data.Len];
                for (int i = 0; i < data.Len; i++)
                    bytes[i] = data[i];
                var json = global::System.Text.Encoding.UTF8.GetString(bytes);

                using var doc = JsonDocument.Parse(json);
                PopulateFromJson(doc.RootElement, v);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // json.Indent(dst *bytes.Buffer, src []byte, prefix, indent string) error
        public static object? Indent(Ngo.Runtime.Bytes.Buffer dst, Slice<byte> src, string prefix, string indent)
        {
            try
            {
                var bytes = new byte[src.Len];
                for (int i = 0; i < src.Len; i++)
                    bytes[i] = src[i];
                var json = global::System.Text.Encoding.UTF8.GetString(bytes);
                using var doc = JsonDocument.Parse(json);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var indented = JsonSerializer.Serialize(doc, options);
                if (indent != "    ")
                    indented = indented.Replace("    ", indent);
                var result = global::System.Text.Encoding.UTF8.GetBytes(indented);
                dst.Write(new Slice<byte>(result));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // json.Valid(data []byte) bool
        public static bool Valid(Slice<byte> data)
        {
            try
            {
                var bytes = new byte[data.Len];
                for (int i = 0; i < data.Len; i++)
                    bytes[i] = data[i];
                using var doc = JsonDocument.Parse(bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static object? ToJsonObject(object? v)
        {
            if (v == null) return null;

            var type = v.GetType();

            // Primitives
            if (v is string || v is bool) return v;
            if (v is long l) return l;
            if (v is int i) return i;
            if (v is double d) return d;
            if (v is float f) return f;
            if (v is byte b) return b;

            // Ptr<T> → unwrap to inner value
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Ptr<>))
            {
                var valueField = type.GetField("Value")!;
                return ToJsonObject(valueField.GetValue(v));
            }

            // Slice<T> → array
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Slice<>))
            {
                var lenProp = type.GetProperty("Len")!;
                int len = (int)lenProp.GetValue(v)!;
                var result = new List<object?>();
                var indexer = type.GetProperty("Item")!;
                for (int idx = 0; idx < len; idx++)
                {
                    var elem = indexer.GetValue(v, new object[] { idx });
                    result.Add(ToJsonObject(elem));
                }
                return result;
            }

            // Map<K,V> → dictionary
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Map<,>))
            {
                var dict = new Dictionary<string, object?>();
                var enumerable = v as IEnumerable;
                if (enumerable != null)
                {
                    foreach (var item in enumerable)
                    {
                        var kvpType = item.GetType();
                        var key = kvpType.GetProperty("Key")!.GetValue(item);
                        var value = kvpType.GetProperty("Value")!.GetValue(item);
                        dict[key?.ToString() ?? ""] = ToJsonObject(value);
                    }
                }
                return dict;
            }

            // Struct (value type) or class — serialize fields
            if ((type.IsValueType && !type.IsPrimitive) || type.IsClass)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var (jsonName, omitEmpty, skip) = GetJsonFieldName(field);
                    if (skip) continue;
                    var value = field.GetValue(v);
                    if (omitEmpty && IsEmpty(value)) continue;
                    dict[jsonName] = ToJsonObject(value);
                }
                return dict;
            }

            return v.ToString();
        }

        internal static void PopulateFromJson(JsonElement elem, object target)
        {
            if (elem.ValueKind != JsonValueKind.Object) return;

            var type = target.GetType();
            var boxed = target;

            foreach (var prop in elem.EnumerateObject())
            {
                // Find matching field (case-insensitive first char)
                var field = FindField(type, prop.Name);
                if (field == null) continue;

                var value = ConvertJsonElement(prop.Value, field.FieldType);
                if (value != null)
                    field.SetValue(boxed, value);
            }
        }

        internal static (string jsonName, bool omitEmpty, bool skip) GetJsonFieldName(FieldInfo field)
        {
            var tagAttr = field.GetCustomAttribute<GoTagAttribute>();
            if (tagAttr != null)
            {
                var jsonTag = ParseStructTag(tagAttr.Tag, "json");
                if (jsonTag != null)
                {
                    if (jsonTag == "-") return ("", false, true);
                    var parts = jsonTag.Split(',');
                    var name = parts[0];
                    bool omitEmpty = parts.Length > 1 && parts.Contains("omitempty");
                    if (string.IsNullOrEmpty(name))
                        name = char.ToLowerInvariant(field.Name[0]) + field.Name.Substring(1);
                    return (name, omitEmpty, false);
                }
            }

            // Default: lowercase first letter
            return (char.ToLowerInvariant(field.Name[0]) + field.Name.Substring(1), false, false);
        }

        private static string? ParseStructTag(string tag, string key)
        {
            // Parse Go struct tag format: `json:"name,opts" xml:"name"`
            var search = key + ":\"";
            int idx = tag.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            int start = idx + search.Length;
            int end = tag.IndexOf('"', start);
            if (end < 0) return null;
            return tag.Substring(start, end - start);
        }

        private static bool IsEmpty(object? value)
        {
            if (value == null) return true;
            if (value is string s) return s.Length == 0;
            if (value is long l) return l == 0;
            if (value is int i) return i == 0;
            if (value is double d) return d == 0;
            if (value is bool b) return !b;
            return false;
        }

        private static FieldInfo? FindField(Type type, string jsonName)
        {
            // First check for fields with matching json tag
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var (tagName, _, skip) = GetJsonFieldName(f);
                if (!skip && string.Equals(tagName, jsonName, StringComparison.Ordinal))
                    return f;
            }

            // Try exact match
            var field = type.GetField(jsonName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field;

            // Try PascalCase (capitalize first letter)
            var pascalName = char.ToUpperInvariant(jsonName[0]) + jsonName.Substring(1);
            field = type.GetField(pascalName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field;

            // Case-insensitive search
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(f.Name, jsonName, StringComparison.OrdinalIgnoreCase))
                    return f;
            }

            return null;
        }

        private static object? ConvertJsonElement(JsonElement elem, Type targetType)
        {
            switch (elem.ValueKind)
            {
                case JsonValueKind.String:
                    return elem.GetString();

                case JsonValueKind.Number:
                    if (targetType == typeof(long)) return elem.GetInt64();
                    if (targetType == typeof(int)) return elem.GetInt32();
                    if (targetType == typeof(double)) return elem.GetDouble();
                    if (targetType == typeof(float)) return elem.GetSingle();
                    if (targetType == typeof(byte)) return elem.GetByte();
                    return elem.GetInt64(); // default for Go int

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return null;
            }
        }
    }

    // json.UnsupportedTypeError struct
    [GoType("struct", Name = "UnsupportedTypeError", Package = "encoding/json")]
    public class GoUnsupportedTypeError
    {
        [GoField(Name = "Type")] public object? Type;

        [GoMethod]
        public string Error() => $"json: unsupported type: {Type}";
    }
}

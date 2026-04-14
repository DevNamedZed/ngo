using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Bidirectional JSON persistence for <see cref="CgoSymbolCatalog"/>.
    /// Produces deterministic output (keys sorted, fixed element order,
    /// indented) so cache files diff cleanly and are cache-key stable.
    ///
    /// The format carries an explicit <c>version</c> field; any mismatch
    /// at read time is a hard error rather than a silent upgrade. Any
    /// missing or malformed field likewise throws
    /// <see cref="InvalidDataException"/> with a path that identifies
    /// the offending location — the cache is never partially loaded.
    /// </summary>
    public static class CgoSymbolCatalogSerializer
    {
        private const int FormatVersion = 2;

        public static string Serialize(CgoSymbolCatalog catalog)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteCatalog(writer, catalog);
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static CgoSymbolCatalog Deserialize(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            RequireObject(root, "$");
            RequireVersion(root);

            var catalog = new CgoSymbolCatalog();
            ReadTypedefs(root, catalog);
            ReadStructsAndUnions(root, catalog);
            ReadEnums(root, catalog);
            ReadFunctions(root, catalog);
            ReadFunctionPointers(root, catalog);
            ReadOpaqueTypes(root, catalog);
            ReadMacroConstants(root, catalog);
            return catalog;
        }

        private static void WriteCatalog(Utf8JsonWriter writer, CgoSymbolCatalog catalog)
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", FormatVersion);

            writer.WritePropertyName("typedefs");
            writer.WriteStartObject();
            foreach (string name in SortedKeys(catalog.Typedefs))
            {
                CgoTypedefInfo typedef = catalog.Typedefs[name];
                writer.WritePropertyName(name);
                writer.WriteStartObject();
                writer.WriteString("aliasCType", typedef.AliasCType);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WritePropertyName("structsAndUnions");
            writer.WriteStartObject();
            foreach (string name in SortedKeys(catalog.StructsAndUnions))
            {
                CgoStructInfo structInfo = catalog.StructsAndUnions[name];
                writer.WritePropertyName(name);
                writer.WriteStartObject();
                writer.WriteString("cName", structInfo.CName);
                writer.WriteBoolean("isUnion", structInfo.IsUnion);
                writer.WriteNumber("sizeBytes", structInfo.SizeBytes);
                writer.WriteNumber("alignmentBytes", structInfo.AlignmentBytes);
                writer.WritePropertyName("fields");
                writer.WriteStartArray();
                foreach (CgoFieldInfo field in structInfo.Fields)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", field.Name);
                    writer.WriteString("cType", field.CType);
                    writer.WriteNumber("offsetBytes", field.OffsetBytes);
                    writer.WriteNumber("sizeBytes", field.SizeBytes);
                    writer.WriteNumber("bitOffset", field.BitOffset);
                    writer.WriteNumber("bitSize", field.BitSize);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WritePropertyName("enums");
            writer.WriteStartObject();
            foreach (string name in SortedKeys(catalog.Enums))
            {
                CgoEnumInfo enumInfo = catalog.Enums[name];
                writer.WritePropertyName(name);
                writer.WriteStartObject();
                writer.WriteString("underlyingCType", enumInfo.UnderlyingCType);
                writer.WritePropertyName("values");
                writer.WriteStartArray();
                foreach (CgoEnumValue value in enumInfo.Values)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", value.Name);
                    writer.WriteNumber("value", value.Value);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WritePropertyName("functions");
            writer.WriteStartObject();
            foreach (string name in SortedKeys(catalog.Functions))
            {
                CgoFunctionInfo function = catalog.Functions[name];
                writer.WritePropertyName(name);
                writer.WriteStartObject();
                writer.WriteString("returnType", function.ReturnType);
                writer.WriteBoolean("isVariadic", function.IsVariadic);
                writer.WritePropertyName("parameters");
                writer.WriteStartArray();
                foreach (CgoParameterInfo parameter in function.Parameters)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", parameter.Name);
                    writer.WriteString("cType", parameter.CType);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WritePropertyName("functionPointers");
            writer.WriteStartObject();
            foreach (string name in SortedKeys(catalog.FunctionPointers))
            {
                CgoFunctionPointerInfo pointer = catalog.FunctionPointers[name];
                writer.WritePropertyName(name);
                writer.WriteStartObject();
                writer.WriteString("returnCType", pointer.ReturnCType);
                writer.WriteBoolean("isVariadic", pointer.IsVariadic);
                writer.WritePropertyName("parameterCTypes");
                writer.WriteStartArray();
                foreach (string parameterCType in pointer.ParameterCTypes)
                {
                    writer.WriteStringValue(parameterCType);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WritePropertyName("opaqueTypes");
            writer.WriteStartArray();
            foreach (string name in catalog.OpaqueTypes.Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                writer.WriteStringValue(name);
            }
            writer.WriteEndArray();

            writer.WritePropertyName("macroConstants");
            writer.WriteStartObject();
            foreach (string name in SortedKeys(catalog.MacroConstants))
            {
                CgoMacroConstantInfo constant = catalog.MacroConstants[name];
                writer.WritePropertyName(name);
                writer.WriteStartObject();
                writer.WriteNumber("value", constant.Value);
                writer.WriteString("underlyingCType", constant.UnderlyingCType);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        private static IEnumerable<string> SortedKeys<T>(IReadOnlyDictionary<string, T> dictionary)
        {
            return dictionary.Keys.OrderBy(key => key, StringComparer.Ordinal);
        }

        private static void RequireVersion(JsonElement root)
        {
            if (!root.TryGetProperty("version", out JsonElement versionElement))
            {
                throw new InvalidDataException("catalog.json: missing required field \"version\"");
            }
            if (versionElement.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidDataException("catalog.json: \"version\" must be a number");
            }
            int version = versionElement.GetInt32();
            if (version != FormatVersion)
            {
                throw new InvalidDataException(
                    $"catalog.json: unsupported format version {version} (expected {FormatVersion})");
            }
        }

        private static void ReadTypedefs(JsonElement root, CgoSymbolCatalog catalog)
        {
            JsonElement typedefs = RequireObjectProperty(root, "typedefs");
            foreach (JsonProperty property in typedefs.EnumerateObject())
            {
                JsonElement entry = property.Value;
                RequireObject(entry, $"typedefs.{property.Name}");
                string aliasCType = RequireStringProperty(entry, "aliasCType", $"typedefs.{property.Name}");
                catalog.AddTypedef(new CgoTypedefInfo(property.Name, aliasCType));
            }
        }

        private static void ReadStructsAndUnions(JsonElement root, CgoSymbolCatalog catalog)
        {
            JsonElement structs = RequireObjectProperty(root, "structsAndUnions");
            foreach (JsonProperty property in structs.EnumerateObject())
            {
                string pathPrefix = $"structsAndUnions.{property.Name}";
                JsonElement entry = property.Value;
                RequireObject(entry, pathPrefix);
                string cName = RequireStringProperty(entry, "cName", pathPrefix);
                bool isUnion = RequireBoolProperty(entry, "isUnion", pathPrefix);
                long structSizeBytes = RequireInt64Property(entry, "sizeBytes", pathPrefix);
                long alignmentBytes = RequireInt64Property(entry, "alignmentBytes", pathPrefix);
                JsonElement fieldsElement = RequireProperty(entry, "fields", pathPrefix);
                RequireArray(fieldsElement, $"{pathPrefix}.fields");
                var fields = new List<CgoFieldInfo>();
                int fieldIndex = 0;
                foreach (JsonElement fieldElement in fieldsElement.EnumerateArray())
                {
                    string fieldPath = $"{pathPrefix}.fields[{fieldIndex}]";
                    RequireObject(fieldElement, fieldPath);
                    string fieldName = RequireStringProperty(fieldElement, "name", fieldPath);
                    string fieldCType = RequireStringProperty(fieldElement, "cType", fieldPath);
                    long offsetBytes = RequireInt64Property(fieldElement, "offsetBytes", fieldPath);
                    long fieldSizeBytes = RequireInt64Property(fieldElement, "sizeBytes", fieldPath);
                    int bitOffset = RequireInt32Property(fieldElement, "bitOffset", fieldPath);
                    int bitSize = RequireInt32Property(fieldElement, "bitSize", fieldPath);
                    fields.Add(new CgoFieldInfo(
                        fieldName, fieldCType, offsetBytes, fieldSizeBytes, bitOffset, bitSize));
                    fieldIndex++;
                }
                catalog.AddStructOrUnion(new CgoStructInfo(
                    cName, property.Name, fields, isUnion, structSizeBytes, alignmentBytes));
            }
        }

        private static void ReadEnums(JsonElement root, CgoSymbolCatalog catalog)
        {
            JsonElement enums = RequireObjectProperty(root, "enums");
            foreach (JsonProperty property in enums.EnumerateObject())
            {
                string pathPrefix = $"enums.{property.Name}";
                JsonElement entry = property.Value;
                RequireObject(entry, pathPrefix);
                string underlyingCType = RequireStringProperty(entry, "underlyingCType", pathPrefix);
                JsonElement valuesElement = RequireProperty(entry, "values", pathPrefix);
                RequireArray(valuesElement, $"{pathPrefix}.values");
                var values = new List<CgoEnumValue>();
                int valueIndex = 0;
                foreach (JsonElement valueElement in valuesElement.EnumerateArray())
                {
                    string valuePath = $"{pathPrefix}.values[{valueIndex}]";
                    RequireObject(valueElement, valuePath);
                    string valueName = RequireStringProperty(valueElement, "name", valuePath);
                    long valueInt = RequireInt64Property(valueElement, "value", valuePath);
                    values.Add(new CgoEnumValue(valueName, valueInt));
                    valueIndex++;
                }
                catalog.AddEnum(new CgoEnumInfo(property.Name, underlyingCType, values));
            }
        }

        private static void ReadFunctions(JsonElement root, CgoSymbolCatalog catalog)
        {
            JsonElement functions = RequireObjectProperty(root, "functions");
            foreach (JsonProperty property in functions.EnumerateObject())
            {
                string pathPrefix = $"functions.{property.Name}";
                JsonElement entry = property.Value;
                RequireObject(entry, pathPrefix);
                var function = new CgoFunctionInfo
                {
                    Name = property.Name,
                    ReturnType = RequireStringProperty(entry, "returnType", pathPrefix),
                    IsVariadic = RequireBoolProperty(entry, "isVariadic", pathPrefix),
                };
                JsonElement parametersElement = RequireProperty(entry, "parameters", pathPrefix);
                RequireArray(parametersElement, $"{pathPrefix}.parameters");
                int parameterIndex = 0;
                foreach (JsonElement parameterElement in parametersElement.EnumerateArray())
                {
                    string parameterPath = $"{pathPrefix}.parameters[{parameterIndex}]";
                    RequireObject(parameterElement, parameterPath);
                    function.Parameters.Add(new CgoParameterInfo
                    {
                        Name = RequireStringProperty(parameterElement, "name", parameterPath),
                        CType = RequireStringProperty(parameterElement, "cType", parameterPath),
                    });
                    parameterIndex++;
                }
                catalog.AddFunction(function);
            }
        }

        private static void ReadFunctionPointers(JsonElement root, CgoSymbolCatalog catalog)
        {
            JsonElement pointers = RequireObjectProperty(root, "functionPointers");
            foreach (JsonProperty property in pointers.EnumerateObject())
            {
                string pathPrefix = $"functionPointers.{property.Name}";
                JsonElement entry = property.Value;
                RequireObject(entry, pathPrefix);
                string returnCType = RequireStringProperty(entry, "returnCType", pathPrefix);
                bool isVariadic = RequireBoolProperty(entry, "isVariadic", pathPrefix);
                JsonElement parameterTypesElement = RequireProperty(entry, "parameterCTypes", pathPrefix);
                RequireArray(parameterTypesElement, $"{pathPrefix}.parameterCTypes");
                var parameterCTypes = new List<string>();
                int parameterIndex = 0;
                foreach (JsonElement parameterTypeElement in parameterTypesElement.EnumerateArray())
                {
                    string parameterPath = $"{pathPrefix}.parameterCTypes[{parameterIndex}]";
                    if (parameterTypeElement.ValueKind != JsonValueKind.String)
                    {
                        throw new InvalidDataException($"catalog.json: {parameterPath} must be a string");
                    }
                    parameterCTypes.Add(parameterTypeElement.GetString()!);
                    parameterIndex++;
                }
                catalog.AddFunctionPointer(new CgoFunctionPointerInfo(
                    property.Name, returnCType, parameterCTypes, isVariadic));
            }
        }

        private static void ReadOpaqueTypes(JsonElement root, CgoSymbolCatalog catalog)
        {
            JsonElement opaqueTypes = RequireProperty(root, "opaqueTypes", "$");
            RequireArray(opaqueTypes, "opaqueTypes");
            int index = 0;
            foreach (JsonElement element in opaqueTypes.EnumerateArray())
            {
                string path = $"opaqueTypes[{index}]";
                if (element.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"catalog.json: {path} must be a string");
                }
                catalog.AddOpaqueType(new CgoOpaqueTypeInfo(element.GetString()!));
                index++;
            }
        }

        private static void ReadMacroConstants(JsonElement root, CgoSymbolCatalog catalog)
        {
            JsonElement constants = RequireObjectProperty(root, "macroConstants");
            foreach (JsonProperty property in constants.EnumerateObject())
            {
                string pathPrefix = $"macroConstants.{property.Name}";
                JsonElement entry = property.Value;
                RequireObject(entry, pathPrefix);
                long value = RequireInt64Property(entry, "value", pathPrefix);
                string underlyingCType = RequireStringProperty(entry, "underlyingCType", pathPrefix);
                catalog.AddMacroConstant(new CgoMacroConstantInfo(property.Name, value, underlyingCType));
            }
        }

        private static JsonElement RequireProperty(JsonElement parent, string name, string parentPath)
        {
            if (!parent.TryGetProperty(name, out JsonElement element))
            {
                throw new InvalidDataException($"catalog.json: missing required field \"{parentPath}.{name}\"");
            }
            return element;
        }

        private static JsonElement RequireObjectProperty(JsonElement parent, string name)
        {
            JsonElement element = RequireProperty(parent, name, "$");
            RequireObject(element, name);
            return element;
        }

        private static string RequireStringProperty(JsonElement parent, string name, string parentPath)
        {
            JsonElement element = RequireProperty(parent, name, parentPath);
            if (element.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"catalog.json: \"{parentPath}.{name}\" must be a string");
            }
            return element.GetString()!;
        }

        private static bool RequireBoolProperty(JsonElement parent, string name, string parentPath)
        {
            JsonElement element = RequireProperty(parent, name, parentPath);
            if (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False)
            {
                throw new InvalidDataException($"catalog.json: \"{parentPath}.{name}\" must be a boolean");
            }
            return element.GetBoolean();
        }

        private static long RequireInt64Property(JsonElement parent, string name, string parentPath)
        {
            JsonElement element = RequireProperty(parent, name, parentPath);
            if (element.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidDataException($"catalog.json: \"{parentPath}.{name}\" must be a number");
            }
            if (!element.TryGetInt64(out long value))
            {
                throw new InvalidDataException(
                    $"catalog.json: \"{parentPath}.{name}\" does not fit in signed 64-bit integer");
            }
            return value;
        }

        private static int RequireInt32Property(JsonElement parent, string name, string parentPath)
        {
            JsonElement element = RequireProperty(parent, name, parentPath);
            if (element.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidDataException($"catalog.json: \"{parentPath}.{name}\" must be a number");
            }
            if (!element.TryGetInt32(out int value))
            {
                throw new InvalidDataException(
                    $"catalog.json: \"{parentPath}.{name}\" does not fit in signed 32-bit integer");
            }
            return value;
        }

        private static void RequireObject(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"catalog.json: \"{path}\" must be an object");
            }
        }

        private static void RequireArray(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"catalog.json: \"{path}\" must be an array");
            }
        }
    }
}

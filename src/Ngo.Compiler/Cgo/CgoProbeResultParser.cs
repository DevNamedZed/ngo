using System;
using System.Collections.Generic;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Parses the output of a compiled-and-executed probe program.
    /// The probe prints lines like:
    ///   sizeof_int=4
    ///   alignof_struct_Point=4
    ///   offsetof_struct_Point_x=0
    ///   offsetof_struct_Point_y=4
    ///   enum_RED=0
    ///   enum_GREEN=1
    /// </summary>
    public class CgoProbeResultParser
    {
        /// <summary>
        /// Parse the stdout of the probe executable into structured results.
        /// </summary>
        public CgoProbeResult Parse(string probeOutput)
        {
            var result = new CgoProbeResult();

            foreach (var rawLine in probeOutput.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                int eqIdx = line.IndexOf('=');
                if (eqIdx < 0)
                {
                    continue;
                }

                string key = line.Substring(0, eqIdx);
                string value = line.Substring(eqIdx + 1);

                if (key.StartsWith("sizeof_"))
                {
                    string typeName = key.Substring(7);
                    if (long.TryParse(value, out long size))
                    {
                        result.TypeSizes[typeName] = size;
                    }
                }
                else if (key.StartsWith("alignof_"))
                {
                    string typeName = key.Substring(8);
                    if (long.TryParse(value, out long alignment))
                    {
                        result.TypeAlignments[typeName] = alignment;
                    }
                }
                else if (key.StartsWith("offsetof_"))
                {
                    string fieldKey = key.Substring(9);
                    if (long.TryParse(value, out long offset))
                    {
                        result.FieldOffsets[fieldKey] = offset;
                    }
                }
                else if (key.StartsWith("fieldsizeof_"))
                {
                    string fieldKey = key.Substring(12);
                    if (long.TryParse(value, out long fieldSize))
                    {
                        result.FieldSizes[fieldKey] = fieldSize;
                    }
                }
                else if (key.StartsWith("enum_"))
                {
                    string enumName = key.Substring(5);
                    if (long.TryParse(value, out long enumValue))
                    {
                        result.EnumValues[enumName] = enumValue;
                    }
                }
            }

            return result;
        }
    }

}

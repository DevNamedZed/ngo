using System.Collections.Generic;
using System.Text;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Generates C probe source files that extract type information using the C compiler.
    /// This is how Go's cgo works — it does NOT parse C. It compiles probe files
    /// and reads sizeof/offsetof/alignof from the resulting object file.
    ///
    /// The probe file includes the user's preamble, then declares arrays whose
    /// sizes encode the type information we need. We compile the probe with -c
    /// and read the symbol sizes from the object file.
    /// </summary>
    public class CgoProbeGenerator
    {
        /// <summary>
        /// Generate a probe C source file from the preamble and the C identifiers
        /// referenced in the Go source via the C pseudo-package.
        /// </summary>
        public string Generate(CgoPreamble preamble, CgoProbeRequest request)
        {
            var sb = new StringBuilder();

            // Include stddef.h for offsetof, size_t, etc.
            sb.AppendLine("#include <stddef.h>");
            sb.AppendLine();

            // Include the user's preamble
            sb.AppendLine("/* User preamble */");
            sb.AppendLine(preamble.CSource);
            sb.AppendLine();

            sb.AppendLine("/* Probe declarations */");
            sb.AppendLine();

            // For each type we need sizeof
            foreach (var typeName in request.TypeSizes)
            {
                string safeName = SanitizeName(typeName);
                sb.AppendLine($"char __cgo_sizeof_{safeName}[sizeof({typeName})];");
            }
            sb.AppendLine();

            // For each type we need alignof
            foreach (var typeName in request.TypeAlignments)
            {
                string safeName = SanitizeName(typeName);
                sb.AppendLine($"char __cgo_alignof_{safeName}[_Alignof({typeName})];");
            }
            sb.AppendLine();

            // For each struct field we need offsetof
            foreach (var field in request.FieldOffsets)
            {
                string safeName = SanitizeName($"{field.StructName}_{field.FieldName}");
                sb.AppendLine($"char __cgo_offsetof_{safeName}[offsetof({field.StructName}, {field.FieldName}) + 1];");
            }
            sb.AppendLine();

            // For each struct field we need the field size
            foreach (var field in request.FieldSizes)
            {
                string safeName = SanitizeName($"{field.StructName}_{field.FieldName}");
                sb.AppendLine($"char __cgo_fieldsizeof_{safeName}[sizeof((({field.StructName}*)0)->{field.FieldName})];");
            }
            sb.AppendLine();

            // For each enum constant we need the value
            // We encode it as: positive values use one array, sign uses another
            foreach (var enumVal in request.EnumValues)
            {
                string safeName = SanitizeName(enumVal);
                // Use a compile-time trick: array size must be positive,
                // so we add 1 to handle zero, and use a separate sign indicator
                sb.AppendLine($"char __cgo_enum_{safeName}[({enumVal}) >= 0 ? ({enumVal}) + 1 : -({enumVal}) + 1];");
                sb.AppendLine($"char __cgo_enumsign_{safeName}[({enumVal}) >= 0 ? 1 : 2];");
            }
            sb.AppendLine();

            // For function signature probing, we create a function pointer variable
            // and let the compiler validate the types
            foreach (var func in request.FunctionProbes)
            {
                sb.AppendLine($"/* Function probe: {func.Name} */");
                sb.AppendLine($"typedef __typeof__({func.Name}) __cgo_ftype_{func.Name};");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate a simpler probe that just compiles and runs, printing sizes to stdout.
        /// This is an alternative to reading object files — compile and execute the probe.
        /// Works on all platforms without needing to parse ELF/COFF/Mach-O.
        /// </summary>
        public string GenerateExecutableProbe(CgoPreamble preamble, CgoProbeRequest request)
        {
            var sb = new StringBuilder();

            sb.AppendLine("#include <stdio.h>");
            sb.AppendLine("#include <stddef.h>");
            sb.AppendLine();
            sb.AppendLine("/* User preamble */");
            sb.AppendLine(preamble.CSource);
            sb.AppendLine();

            sb.AppendLine("int main() {");

            // Print type sizes
            foreach (var typeName in request.TypeSizes)
            {
                string safeName = SanitizeName(typeName);
                sb.AppendLine($"    printf(\"sizeof_{safeName}=%zu\\n\", sizeof({typeName}));");
            }

            // Print alignments
            foreach (var typeName in request.TypeAlignments)
            {
                string safeName = SanitizeName(typeName);
                sb.AppendLine($"    printf(\"alignof_{safeName}=%zu\\n\", _Alignof({typeName}));");
            }

            // Print field offsets
            foreach (var field in request.FieldOffsets)
            {
                string safeName = SanitizeName($"{field.StructName}_{field.FieldName}");
                sb.AppendLine($"    printf(\"offsetof_{safeName}=%zu\\n\", offsetof({field.StructName}, {field.FieldName}));");
            }

            // Print field sizes
            foreach (var field in request.FieldSizes)
            {
                string safeName = SanitizeName($"{field.StructName}_{field.FieldName}");
                sb.AppendLine($"    printf(\"fieldsizeof_{safeName}=%zu\\n\", sizeof((({field.StructName}*)0)->{field.FieldName}));");
            }

            // Print enum values
            foreach (var enumVal in request.EnumValues)
            {
                string safeName = SanitizeName(enumVal);
                sb.AppendLine($"    printf(\"enum_{safeName}=%lld\\n\", (long long)({enumVal}));");
            }

            sb.AppendLine("    return 0;");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string SanitizeName(string name)
        {
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }
            return sb.ToString();
        }
    }

}

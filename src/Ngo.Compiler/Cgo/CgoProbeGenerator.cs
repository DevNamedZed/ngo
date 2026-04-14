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
        /// Version tag baked into the anchor-probe cache key so that any
        /// change to the probe's output shape — the anchor variable naming
        /// scheme, the leading includes, the auto-injected typedef block,
        /// etc. — invalidates previously cached object files. Bump this
        /// whenever <see cref="GenerateAnchorProbe"/> emits text that
        /// differs in a way the DWARF reader can observe, otherwise stale
        /// <c>.o</c> files under <c>&lt;cache&gt;/cgo/anchor_*</c> will be
        /// reused and the reader will see the old shape.
        /// </summary>
        public const string AnchorProbeSchemeVersion = "v2-name-embedded-anchors";

        /// <summary>
        /// Version tag for the macro-probe cache key. Bump this alongside
        /// any change to <see cref="GenerateMacroProbe"/>'s output shape
        /// (enumerator naming, wrapping strategy for non-integer macros,
        /// etc.) so stale <c>.o</c> files under
        /// <c>&lt;cache&gt;/cgo/macro_*</c> do not get reused and
        /// misinterpreted.
        /// </summary>
        public const string MacroProbeSchemeVersion = "v3-tagged-enum-with-anchor-variable";

        /// <summary>
        /// Prefix burned into every enumerator name emitted by
        /// <see cref="GenerateMacroProbe"/>. The DWARF reader strips this
        /// prefix to recover the original macro name and register the
        /// enumerator value as a <see cref="CgoMacroConstantInfo"/>.
        /// </summary>
        public const string MacroEnumeratorPrefix = "__ngo_macro_";

        /// <summary>
        /// Prefix used for the tag name of each per-macro
        /// <c>DW_TAG_enumeration_type</c>. The tagged enum would otherwise
        /// be eliminated by DWARF's unused-type pruning because no other
        /// DIE references an untagged anonymous enum.
        /// </summary>
        public const string MacroEnumTagPrefix = "__ngo_macro_enum_";

        /// <summary>
        /// Prefix used for the static variable that references each
        /// per-macro enum type. The variable declaration is what
        /// prevents DWARF unused-type elimination from dropping the
        /// enum DIE, and <c>__attribute__((used))</c> keeps the
        /// variable itself alive against storage-level dead-code
        /// elimination at <c>-O0</c>.
        /// </summary>
        public const string MacroReferenceVariablePrefix = "__ngo_macro_ref_";

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
        /// Generate a minimal probe that compiles and runs, printing sizes to stdout.
        /// Intentionally does <b>not</b> embed the user preamble: primitive
        /// C types (<c>int</c>, <c>long</c>, <c>size_t</c>, …) are defined
        /// by the language and <c>&lt;stddef.h&gt;</c> alone, so the probe
        /// doesn't need anything from the Go package's C comment block.
        /// Keeping the preamble out also sidesteps the link step —
        /// combined preambles in packages like <c>DataDog/zstd</c> carry
        /// wrapper function bodies that call library-only functions
        /// (<c>ZSTD_compressStream2</c> etc.); inlining those into the
        /// probe would force the probe executable to link against the
        /// real library at cgo-analysis time. User-defined types and
        /// struct layouts are probed by the anchor probe instead
        /// (<see cref="GenerateAnchorProbe"/>), which compiles with
        /// <c>-c</c> and therefore never needs to link.
        /// </summary>
        public string GenerateExecutableProbe(CgoPreamble preamble, CgoProbeRequest request)
        {
            var sb = new StringBuilder();

            sb.AppendLine("#include <stdio.h>");
            sb.AppendLine("#include <stddef.h>");
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

        /// <summary>
        /// Generate a probe source file that keeps every C identifier
        /// referenced from Go source alive in the compiled object's
        /// debug information. Each name is anchored with
        /// <c>static __typeof__(name) *__ngo_anchor_&lt;go_name&gt;;</c>:
        /// the GCC/Clang <c>__typeof__</c> extension yields the type of
        /// <c>name</c> whether it denotes a type, function, variable, or
        /// macro-expanded expression, and declaring a pointer forces
        /// DWARF/PDB to carry the underlying type info without
        /// requiring a complete type. That is the only anchoring form
        /// that accepts opaque typedefs such as <c>ZSTD_CCtx</c> or
        /// <c>sqlite3_backup</c>. The anchor variable's name embeds the
        /// Go-side identifier (struct_/union_/enum_ prefixes kept as-is
        /// because they are valid C identifiers) so the DWARF reader
        /// can recover which C symbol each anchor was generated for.
        /// That name-carrying step is what lets library functions like
        /// <c>malloc</c> — which never emit a <c>DW_TAG_subprogram</c>
        /// into the probe's own object file — still surface in the
        /// catalog, via their <c>DW_TAG_subroutine_type</c> reached
        /// through the anchor variable's pointer type.
        /// </summary>
        public string GenerateAnchorProbe(
            CgoPreamble preamble, CgoUsageSet usageSet, CCompilerKind compilerKind)
        {
            if (compilerKind == CCompilerKind.MSVC)
            {
                throw new System.NotSupportedException(
                    "cgo anchor probe generation for MSVC is not yet implemented: " +
                    "MSVC lacks the __typeof__ extension the probe relies on to " +
                    "force DWARF/PDB type emission without sizeof. A MSVC-specific " +
                    "probe strategy must be designed before this path can be enabled.");
            }

            var sb = new StringBuilder();

            sb.AppendLine("#include <stddef.h>");
            sb.AppendLine();
            sb.AppendLine(CgoBuiltinTypedefs.CSourceBlock);
            sb.AppendLine("/* User preamble */");
            sb.AppendLine(preamble.CSource);
            sb.AppendLine();
            sb.AppendLine("/* Anchor references: static __typeof__(X) *__ngo_anchor_X;");
            sb.AppendLine(" * forces the compiler to emit debug info for X's type,");
            sb.AppendLine(" * whether X names a type, function, variable, or macro");
            sb.AppendLine(" * expression, and works for opaque typedefs because only a");
            sb.AppendLine(" * pointer is declared. The anchor variable name embeds the");
            sb.AppendLine(" * Go-side identifier so the DWARF reader can map each");
            sb.AppendLine(" * surviving function type back to the C symbol it anchored. */");

            foreach (string name in usageSet.Names)
            {
                string cExpression = CgoNameTranslator.ToCExpression(name);
                sb.Append("static __typeof__(");
                sb.Append(cExpression);
                sb.Append(") *__ngo_anchor_");
                sb.Append(name);
                sb.AppendLine(";");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate a probe source whose sole purpose is to surface the
        /// values of C <c>#define</c> macros referenced from Go that did
        /// not resolve to any symbol in the typeof-based anchor probe.
        /// DWARF does not carry preprocessor macros by default, so their
        /// values are invisible to the regular reader. The standard cgo
        /// trick — wrap each name in an anonymous <c>enum</c> block —
        /// forces the compiler to evaluate the macro as an integer
        /// constant expression and emit the result as a
        /// <see cref="DwarfTag.Enumerator"/> DIE whose value the reader
        /// then captures.
        /// <para>
        /// Every macro gets its own single-enumerator block rather than
        /// being packed into one shared enum. Separate blocks keep the
        /// per-enumerator DIEs independent in DWARF (one anonymous enum
        /// DIE per macro) so downstream registration does not need to
        /// peek inside shared enumerations and so each value carries a
        /// self-contained <c>DW_AT_type</c>. The leading enumerator name
        /// uses the <see cref="MacroEnumeratorPrefix"/>; the DWARF reader
        /// strips it and stores the rest as a macro constant.
        /// </para>
        /// <para>
        /// Macros whose replacement text is not an integer constant
        /// expression (string literals, function call expressions, etc.)
        /// cause the compile to fail here. That is intentional: an
        /// <c>import "C"</c> reference to such a macro could never
        /// compile on the Go side either, so the user deserves a
        /// diagnostic rather than a silently-skipped symbol.
        /// </para>
        /// </summary>
        public string GenerateMacroProbe(
            CgoPreamble preamble,
            IReadOnlyList<string> macroNames,
            CCompilerKind compilerKind)
        {
            if (compilerKind == CCompilerKind.MSVC)
            {
                throw new System.NotSupportedException(
                    "cgo macro probe generation for MSVC is not yet implemented: " +
                    "the enum-trick surface is portable C, but cache paths and " +
                    "driver invocation for MSVC still need to be threaded through.");
            }
            if (macroNames == null)
            {
                throw new System.ArgumentNullException(nameof(macroNames));
            }

            var sb = new StringBuilder();
            sb.AppendLine("#include <stddef.h>");
            sb.AppendLine();
            sb.AppendLine(CgoBuiltinTypedefs.CSourceBlock);
            sb.AppendLine("/* User preamble */");
            sb.AppendLine(preamble.CSource);
            sb.AppendLine();
            sb.AppendLine("/* Macro value probes: each C #define is wrapped in a tagged");
            sb.AppendLine(" * enum paired with a static variable of the enum's type. The");
            sb.AppendLine(" * compiler evaluates the preprocessor expression as an integer");
            sb.AppendLine(" * constant and emits the resulting value as a DW_TAG_enumerator");
            sb.AppendLine(" * inside the enum's DW_TAG_enumeration_type DIE. DWARF's");
            sb.AppendLine(" * unused-type elimination drops type DIEs that no variable or");
            sb.AppendLine(" * function references; the static __ngo_macro_ref_X variable is");
            sb.AppendLine(" * what keeps each enum alive in the debug info. The used");
            sb.AppendLine(" * attribute defeats dead-storage elimination on the variable.");
            sb.AppendLine(" * The MacroEnumeratorPrefix lets the DWARF reader recognise the");
            sb.AppendLine(" * enumerator names and register them as macro constants rather");
            sb.AppendLine(" * than ordinary enum values. */");
            foreach (string name in macroNames)
            {
                sb.Append("enum ");
                sb.Append(MacroEnumTagPrefix);
                sb.Append(name);
                sb.Append(" { ");
                sb.Append(MacroEnumeratorPrefix);
                sb.Append(name);
                sb.Append(" = (");
                sb.Append(name);
                sb.AppendLine(") };");
                sb.Append("static enum ");
                sb.Append(MacroEnumTagPrefix);
                sb.Append(name);
                sb.Append(' ');
                sb.Append(MacroReferenceVariablePrefix);
                sb.Append(name);
                sb.AppendLine(" __attribute__((used));");
            }

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

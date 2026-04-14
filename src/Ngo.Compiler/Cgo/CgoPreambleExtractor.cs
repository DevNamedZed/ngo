using System.Collections.Generic;
using System.Text;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Extracts the C preamble from comment trivia immediately preceding an import "C" declaration.
    /// Matches Go's cgo behavior: the preamble is the contiguous block of // comments
    /// directly before import "C" with no blank lines between them.
    /// </summary>
    public class CgoPreambleExtractor
    {
        /// <summary>
        /// Extracts the preamble from an import "C" spec.
        /// The preamble comes from the LeadingExtra of the import keyword token.
        /// <paramref name="sourceDirectory"/> is the directory the Go
        /// source file sits in and is embedded on the resulting
        /// <see cref="CgoPreamble"/> so that <c>CgoCompiler.BuildIncludeArgs</c>
        /// can pass it to the probe compiler as <c>-I</c> — that is how
        /// <c>#include "foo.h"</c> in the preamble resolves against
        /// package-local headers (e.g. <c>zstd.h</c> shipped inside
        /// <c>github.com/DataDog/zstd</c>). Pass the empty string only
        /// for synthetic inputs that reference nothing but standard
        /// headers, because the probe compile will then run without
        /// <c>-I</c>.
        /// </summary>
        public CgoPreamble? Extract(ImportSpecSyntax importSpec, SyntaxToken importKeyword, string sourceDirectory)
        {
            string pathText = importSpec.Path.Text?.Trim('"') ?? "";
            string pathValue = importSpec.Path.Value?.ToString()?.Trim('"') ?? pathText;
            if (pathValue != "C")
            {
                return null;
            }

            var leadingExtra = importKeyword.LeadingExtra;
            string sourceDir = sourceDirectory ?? string.Empty;

            if (leadingExtra == null || leadingExtra.Count == 0)
            {
                return new CgoPreamble("", new List<CgoDirective>(), sourceDir);
            }

            var commentLines = new List<string>();
            bool sawBlankLine = false;
            int consecutiveNewlines = 0;

            for (int i = leadingExtra.Count - 1; i >= 0; i--)
            {
                var extra = leadingExtra[i];

                if (extra.Kind == SyntaxKind.LineCommentExtra)
                {
                    if (sawBlankLine)
                    {
                        break;
                    }
                    consecutiveNewlines = 0;

                    string text = extra.Text;
                    if (text.StartsWith("// "))
                    {
                        commentLines.Add(text.Substring(3));
                    }
                    else if (text.StartsWith("//"))
                    {
                        commentLines.Add(text.Substring(2));
                    }
                    else
                    {
                        commentLines.Add(text);
                    }
                }
                else if (extra.Kind == SyntaxKind.BlockCommentExtra)
                {
                    if (sawBlankLine)
                    {
                        break;
                    }
                    consecutiveNewlines = 0;

                    string text = extra.Text;
                    if (text.StartsWith("/*"))
                    {
                        text = text.Substring(2);
                    }
                    if (text.EndsWith("*/"))
                    {
                        text = text.Substring(0, text.Length - 2);
                    }

                    // The outer loop walks leadingExtra from last to first so
                    // the final commentLines.Reverse() restores source order.
                    // A block comment is a single extra but carries many inner
                    // lines; we must push those inner lines in reverse here so
                    // that the final reversal puts them back in forward order.
                    string[] blockLines = text.Split('\n');
                    for (int blockLineIndex = blockLines.Length - 1; blockLineIndex >= 0; blockLineIndex--)
                    {
                        commentLines.Add(blockLines[blockLineIndex].TrimEnd('\r'));
                    }
                }
                else if (extra.Kind == SyntaxKind.EndOfLineExtra)
                {
                    consecutiveNewlines++;
                    if (consecutiveNewlines >= 2)
                    {
                        sawBlankLine = true;
                    }
                }
            }

            commentLines.Reverse();

            var directives = new List<CgoDirective>();
            var cSourceLines = new List<string>();

            foreach (var line in commentLines)
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("#cgo "))
                {
                    var directive = CgoDirectiveParser.Parse(trimmed);
                    if (directive != null)
                    {
                        directives.Add(directive);
                    }
                }
                else
                {
                    cSourceLines.Add(line);
                }
            }

            string cSource = string.Join("\n", cSourceLines);
            return new CgoPreamble(cSource, directives, sourceDir);
        }

        /// <summary>
        /// Extract a single preamble that represents every <c>import "C"</c>
        /// found anywhere in <paramref name="packageFiles"/>, concatenated
        /// into one C source and with directive lists merged. Go's cgo
        /// treats all <c>import "C"</c> preambles in a package as one
        /// combined translation unit — a wrapper function defined in
        /// <c>zstd_stream.go</c> is in scope for a <c>C.zstd_compress(...)</c>
        /// reference from <c>zstd.go</c>. The anchor probe therefore has
        /// to see the union of them, otherwise symbols defined inline in
        /// sibling files appear "undeclared" when the probe is compiled.
        /// <paramref name="sourceDirectory"/> is the package directory
        /// and is stored on the returned preamble so <c>-I</c> can be
        /// emitted against package-local headers.
        /// <para>
        /// <c>#include</c> lines are de-duplicated by exact text match:
        /// multiple files in a package routinely include the same
        /// headers (<c>&lt;stdlib.h&gt;</c>, package-local <c>.h</c>
        /// files, etc.), and some of those headers lack <c>#ifndef</c>
        /// guards (e.g. <c>pkcs11go.h</c> in <c>miekg/pkcs11</c>), which
        /// would trigger duplicate-typedef errors when the combined
        /// preamble is compiled as one translation unit. Go's real cgo
        /// sidesteps this by compiling each file's preamble in its own
        /// translation unit; we instead combine text and dedupe the
        /// include lines that would otherwise be textually identical.
        /// </para>
        /// </summary>
        public CgoPreamble ExtractCombined(
            IReadOnlyList<SourceFileSyntax> packageFiles, string sourceDirectory)
        {
            string sourceDir = sourceDirectory ?? string.Empty;
            var combinedCSource = new StringBuilder();
            var combinedDirectives = new List<CgoDirective>();
            var seenIncludes = new HashSet<string>();

            foreach (var file in packageFiles)
            {
                foreach (var importDecl in file.Imports)
                {
                    foreach (var spec in importDecl.Specs)
                    {
                        CgoPreamble? preamble = Extract(spec, importDecl.ImportKeyword, sourceDir);
                        if (preamble == null || !preamble.HasCSource)
                        {
                            preamble = Extract(spec, spec.Path, sourceDir);
                        }

                        if (preamble == null)
                        {
                            continue;
                        }

                        if (preamble.HasCSource)
                        {
                            if (combinedCSource.Length > 0)
                            {
                                combinedCSource.Append('\n');
                            }
                            AppendDedupedSource(combinedCSource, preamble.CSource, seenIncludes);
                        }

                        foreach (var directive in preamble.Directives)
                        {
                            combinedDirectives.Add(directive);
                        }
                    }
                }
            }

            return new CgoPreamble(combinedCSource.ToString(), combinedDirectives, sourceDir);
        }

        private static void AppendDedupedSource(
            StringBuilder destination, string cSource, HashSet<string> seenIncludes)
        {
            string[] lines = cSource.Split('\n');
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("#include"))
                {
                    string includeKey = trimmed.TrimEnd();
                    if (!seenIncludes.Add(includeKey))
                    {
                        continue;
                    }
                }
                destination.Append(line);
                if (lineIndex < lines.Length - 1)
                {
                    destination.Append('\n');
                }
            }
        }
    }
}

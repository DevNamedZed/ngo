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
        /// </summary>
        public CgoPreamble? Extract(ImportSpecSyntax importSpec, SyntaxToken importKeyword, string sourceFilePath)
        {
            string pathText = importSpec.Path.Text?.Trim('"') ?? "";
            string pathValue = importSpec.Path.Value?.ToString()?.Trim('"') ?? pathText;
            if (pathValue != "C")
            {
                return null;
            }

            var leadingExtra = importKeyword.LeadingExtra;
            string sourceDir = System.IO.Path.GetDirectoryName(sourceFilePath) ?? ".";

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
                    foreach (var line in text.Split('\n'))
                    {
                        commentLines.Add(line.TrimEnd('\r'));
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
    }
}

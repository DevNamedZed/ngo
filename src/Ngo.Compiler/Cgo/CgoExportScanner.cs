using System.Collections.Generic;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Scans Go source files for //export directives on functions.
    /// In Go, a comment "//export FuncName" immediately before a function declaration
    /// makes that Go function callable from C code.
    /// </summary>
    public class CgoExportScanner
    {
        /// <summary>
        /// Scan function declarations for //export comments.
        /// Returns a map of Go function name → C export name.
        /// </summary>
        public Dictionary<string, string> Scan(IReadOnlyList<FunctionDeclarationSyntax> functions)
        {
            var exports = new Dictionary<string, string>();

            foreach (var func in functions)
            {
                // Check leading trivia on the func keyword for //export comments
                var funcToken = func.FuncKeyword;
                if (funcToken.LeadingExtra == null)
                {
                    continue;
                }

                foreach (var extra in funcToken.LeadingExtra)
                {
                    if (extra.Kind != SyntaxKind.LineCommentExtra)
                    {
                        continue;
                    }

                    string text = extra.Text;
                    if (text.StartsWith("//export "))
                    {
                        string exportName = text.Substring(9).Trim();
                        if (!string.IsNullOrEmpty(exportName))
                        {
                            string goFuncName = func.Name?.Text ?? "";
                            if (!string.IsNullOrEmpty(goFuncName))
                            {
                                exports[goFuncName] = exportName;
                            }
                        }
                    }
                }
            }

            return exports;
        }
    }
}

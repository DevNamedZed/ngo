using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Extracts C function declarations from preamble text.
    /// This handles user-defined functions written directly in the preamble comments.
    /// For #include'd functions, the C compiler probe handles type resolution.
    ///
    /// Parses patterns like:
    ///   int add(int a, int b) { ... }
    ///   void greet(const char* name);
    ///   double compute(double x, double y);
    ///   extern int lookup(const char* key);
    /// </summary>
    public class CgoPreambleFunctionExtractor
    {
        // Regex for C function declarations/definitions
        // Matches: [static|extern] return_type func_name(params) [{body}|;]
        private static readonly Regex FunctionPattern = new Regex(
            @"(?:^|\n)\s*" +                              // Start of line
            @"(?:(?:static|extern|inline)\s+)*" +          // Optional qualifiers
            @"((?:(?:const|unsigned|signed|long|short|struct|enum)\s+)*\w[\w\s\*]*?)" + // Return type (group 1)
            @"\s+(\w+)\s*" +                               // Function name (group 2)
            @"\(([^)]*)\)" +                               // Parameters (group 3)
            @"\s*(?:\{[^}]*\}|;)",                         // Body or semicolon
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Regex for individual parameters
        private static readonly Regex ParamPattern = new Regex(
            @"((?:(?:const|unsigned|signed|long|short|struct|enum)\s+)*\w[\w\s\*]*?)" + // Type
            @"\s+(\w+)\s*$",                                                             // Name
            RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Extract function declarations from preamble C source.
        /// </summary>
        public List<CgoFunctionInfo> Extract(string cSource)
        {
            var functions = new List<CgoFunctionInfo>();

            if (string.IsNullOrWhiteSpace(cSource))
            {
                return functions;
            }

            foreach (Match match in FunctionPattern.Matches(cSource))
            {
                string returnType = CleanType(match.Groups[1].Value);
                string funcName = match.Groups[2].Value.Trim();
                string paramList = match.Groups[3].Value.Trim();

                // Skip preprocessor macros, typedefs, and keywords that look like functions
                if (IsKeyword(funcName))
                {
                    continue;
                }

                var func = new CgoFunctionInfo
                {
                    Name = funcName,
                    ReturnType = returnType,
                    IsVariadic = paramList.Contains("..."),
                };

                // Parse parameters
                if (!string.IsNullOrEmpty(paramList) && paramList != "void")
                {
                    foreach (var paramStr in SplitParams(paramList))
                    {
                        string trimmed = paramStr.Trim();
                        if (trimmed == "..." || string.IsNullOrEmpty(trimmed))
                        {
                            continue;
                        }

                        var paramMatch = ParamPattern.Match(trimmed);
                        if (paramMatch.Success)
                        {
                            func.Parameters.Add(new CgoParameterInfo
                            {
                                CType = CleanType(paramMatch.Groups[1].Value),
                                Name = paramMatch.Groups[2].Value.Trim(),
                            });
                        }
                        else
                        {
                            // Parameter without a name (e.g., just "int")
                            func.Parameters.Add(new CgoParameterInfo
                            {
                                CType = CleanType(trimmed),
                                Name = $"p{func.Parameters.Count}",
                            });
                        }
                    }
                }

                functions.Add(func);
            }

            return functions;
        }

        /// <summary>
        /// Extract struct declarations from preamble C source.
        /// </summary>
        public List<CgoStructInfo> ExtractStructs(string cSource)
        {
            var structs = new List<CgoStructInfo>();

            if (string.IsNullOrWhiteSpace(cSource))
            {
                return structs;
            }

            // Match: struct Name { field_type field_name; ... };
            var structPattern = new Regex(
                @"(?:typedef\s+)?struct\s+(\w+)\s*\{([^}]*)\}\s*(\w*)\s*;",
                RegexOptions.Compiled | RegexOptions.Singleline);

            foreach (Match match in structPattern.Matches(cSource))
            {
                string structTag = match.Groups[1].Value.Trim();
                string fieldsStr = match.Groups[2].Value.Trim();
                string typedefName = match.Groups[3].Value.Trim();

                string goName = !string.IsNullOrEmpty(typedefName) ? typedefName : structTag;

                var structInfo = new CgoStructInfo
                {
                    CName = $"struct {structTag}",
                    GoName = goName,
                };

                // Parse fields
                foreach (var fieldLine in fieldsStr.Split(';'))
                {
                    string trimmed = fieldLine.Trim();
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        continue;
                    }

                    var fieldMatch = ParamPattern.Match(trimmed);
                    if (fieldMatch.Success)
                    {
                        structInfo.Fields.Add(new CgoFieldInfo
                        {
                            CType = CleanType(fieldMatch.Groups[1].Value),
                            Name = fieldMatch.Groups[2].Value.Trim(),
                        });
                    }
                }

                structs.Add(structInfo);
            }

            return structs;
        }

        private static string CleanType(string type)
        {
            return type.Trim().Replace("  ", " ");
        }

        private static List<string> SplitParams(string paramList)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < paramList.Length; i++)
            {
                char c = paramList[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    result.Add(paramList.Substring(start, i - start));
                    start = i + 1;
                }
            }
            result.Add(paramList.Substring(start));
            return result;
        }

        private static bool IsKeyword(string name)
        {
            return name switch
            {
                "if" or "else" or "while" or "for" or "do" or "switch" or "case" or
                "return" or "break" or "continue" or "goto" or "sizeof" or "typeof" or
                "typedef" or "struct" or "enum" or "union" or "const" or "static" or
                "extern" or "inline" or "void" or "register" or "volatile" or
                "include" or "define" or "ifdef" or "ifndef" or "endif" or "pragma" => true,
                _ => false,
            };
        }
    }
}

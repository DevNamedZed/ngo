using System.Collections.Generic;
using Ngo.Compiler.Language;
using Ngo.Compiler.Language.Syntax;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// AST walker that finds every <c>C.&lt;ident&gt;</c> selector
    /// expression across a package's source files. The resulting
    /// <see cref="CgoUsageSet"/> drives probe generation so the C
    /// compiler keeps all referenced symbols in the compiled probe's
    /// debug info for later DWARF/PDB reading.
    /// </summary>
    public sealed class CgoUsageCollector
    {
        private const string CgoImportIdentifier = "C";

        public static CgoUsageSet Collect(IReadOnlyList<SourceFileSyntax> sourceFiles)
        {
            var usageSet = new CgoUsageSet();
            var walker = new CgoSelectorWalker(usageSet);
            foreach (SourceFileSyntax sourceFile in sourceFiles)
            {
                walker.Visit(sourceFile);
            }
            return usageSet;
        }

        private sealed class CgoSelectorWalker : SyntaxVisitor
        {
            private readonly CgoUsageSet _usageSet;

            public CgoSelectorWalker(CgoUsageSet usageSet)
            {
                _usageSet = usageSet;
            }

            protected override void VisitSelectorExpression(SelectorExpressionSyntax node)
            {
                if (IsCgoReference(node))
                {
                    string identifier = node.Name.Text;
                    if (!string.IsNullOrEmpty(identifier) && !CgoPseudoNames.IsPseudoName(identifier))
                    {
                        _usageSet.Add(identifier, node.Name.Span);
                    }
                }

                DefaultVisit(node);
            }

            private static bool IsCgoReference(SelectorExpressionSyntax node)
            {
                if (node.Expression is IdentifierNameSyntax identifier)
                {
                    return identifier.Identifier.Text == CgoImportIdentifier;
                }
                return false;
            }
        }
    }
}

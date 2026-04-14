using System.Collections.Generic;
using Ngo.Compiler.Language;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// The full set of C identifiers referenced from Go source as
    /// <c>C.&lt;ident&gt;</c>. Produced by <see cref="CgoUsageCollector"/>
    /// and fed to the probe generator so that every referenced symbol
    /// is kept alive in the compiled probe's debug information. Source
    /// locations are retained so diagnostics can point at the first
    /// Go reference when the underlying preamble rejects a name.
    /// </summary>
    public sealed class CgoUsageSet
    {
        private readonly SortedSet<string> _names = new();
        private readonly Dictionary<string, TextSpan> _firstSeenAt = new();

        public int Count
        {
            get { return _names.Count; }
        }

        /// <summary>
        /// Unique identifiers in deterministic (sorted) order so cache
        /// keys and diagnostics are stable across runs.
        /// </summary>
        public IReadOnlyCollection<string> Names
        {
            get { return _names; }
        }

        public TextSpan FirstSeenAt(string name)
        {
            return _firstSeenAt[name];
        }

        public bool Contains(string name)
        {
            return _names.Contains(name);
        }

        public void Add(string name, TextSpan firstSeen)
        {
            if (_names.Add(name))
            {
                _firstSeenAt[name] = firstSeen;
            }
        }
    }
}

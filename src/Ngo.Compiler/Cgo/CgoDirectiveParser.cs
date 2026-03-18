namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Parses #cgo directives from preamble lines.
    /// Matches Go's cgo behavior for CFLAGS, LDFLAGS, and pkg-config directives,
    /// with optional OS constraints (windows, linux, darwin, freebsd, etc.).
    /// </summary>
    public static class CgoDirectiveParser
    {
        /// <summary>
        /// Parse a single #cgo directive line.
        /// Examples:
        ///   "#cgo CFLAGS: -I${SRCDIR}/include"
        ///   "#cgo LDFLAGS: -L/usr/lib -lmylib"
        ///   "#cgo windows LDFLAGS: -lws2_32"
        ///   "#cgo linux,darwin CFLAGS: -DUNIX"
        ///   "#cgo pkg-config: libpng"
        /// </summary>
        public static CgoDirective? Parse(string line)
        {
            if (!line.StartsWith("#cgo "))
            {
                return null;
            }

            string remaining = line.Substring(5).Trim(); // After "#cgo "

            // Find the colon separator between kind and value
            int colonIdx = remaining.IndexOf(':');
            if (colonIdx < 0)
            {
                return null;
            }

            string kindPart = remaining.Substring(0, colonIdx).Trim();
            string value = remaining.Substring(colonIdx + 1).Trim();

            // Parse optional OS constraint
            // kindPart could be:
            //   "CFLAGS"                     — no constraint
            //   "windows LDFLAGS"            — single OS
            //   "linux,darwin CFLAGS"        — multiple OS
            //   "!windows CFLAGS"            — negated OS
            //   "pkg-config"                 — pkg-config directive
            string? osConstraint = null;
            string kind;

            string[] parts = kindPart.Split(' ');
            if (parts.Length == 1)
            {
                // No OS constraint — kindPart is just the kind
                kind = parts[0];
            }
            else if (parts.Length == 2)
            {
                // First part is OS constraint, second is kind
                osConstraint = parts[0];
                kind = parts[1];
            }
            else
            {
                return null; // Invalid format
            }

            // Validate kind
            if (kind != "CFLAGS" && kind != "LDFLAGS" && kind != "CPPFLAGS" && kind != "CXXFLAGS" && kind != "FFLAGS" && kind != "pkg-config")
            {
                return null;
            }

            return new CgoDirective(osConstraint, kind, value);
        }
    }

    /// <summary>
    /// Represents a parsed #cgo directive.
    /// </summary>
    public class CgoDirective
    {
        /// <summary>OS constraint (null = all platforms, "windows", "linux", "darwin", "linux,darwin", "!windows").</summary>
        public string? OsConstraint { get; }

        /// <summary>Directive kind: "CFLAGS", "LDFLAGS", "CPPFLAGS", "CXXFLAGS", "FFLAGS", "pkg-config".</summary>
        public string Kind { get; }

        /// <summary>The raw value string (flags or pkg-config package names).</summary>
        public string Value { get; }

        public CgoDirective(string? osConstraint, string kind, string value)
        {
            OsConstraint = osConstraint;
            Kind = kind;
            Value = value;
        }

        /// <summary>
        /// Check if this directive applies to the given OS.
        /// Handles comma-separated lists and negation (!windows).
        /// </summary>
        public bool MatchesOS(string currentOS)
        {
            if (OsConstraint == null)
            {
                return true; // No constraint — applies everywhere
            }

            // Negation
            if (OsConstraint.StartsWith("!"))
            {
                string excluded = OsConstraint.Substring(1);
                return !MatchesOSList(excluded, currentOS);
            }

            return MatchesOSList(OsConstraint, currentOS);
        }

        /// <summary>
        /// Returns the value with ${SRCDIR} expanded.
        /// </summary>
        public string ExpandedValue(string sourceDirectory)
        {
            return Value.Replace("${SRCDIR}", sourceDirectory);
        }

        private static bool MatchesOSList(string osList, string currentOS)
        {
            foreach (string os in osList.Split(','))
            {
                if (os.Trim().Equals(currentOS, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}

using System;

namespace Ngo.Compiler.Cgo
{
    /// <summary>
    /// Snapshot of cgo-relevant environment variables. Go-primary names
    /// (<c>CC</c>, <c>CXX</c>, <c>CGO_ENABLED</c>, <c>CGO_CFLAGS</c>, ...)
    /// are always read; unprefixed Make conventions (<c>CFLAGS</c>,
    /// <c>CPPFLAGS</c>, <c>LDFLAGS</c>, <c>CXXFLAGS</c>) are used as
    /// fallbacks when the Go-primary variant is unset.
    /// </summary>
    public sealed class CgoEnvironment
    {
        public CgoEnvironment(
            string? cc,
            string? cxx,
            string? cgoEnabled,
            string? cFlags,
            string? cppFlags,
            string? cxxFlags,
            string? ldFlags)
        {
            CC = cc;
            CXX = cxx;
            CgoEnabled = cgoEnabled;
            CFlags = cFlags;
            CppFlags = cppFlags;
            CxxFlags = cxxFlags;
            LdFlags = ldFlags;
        }

        /// <summary>The <c>CC</c> environment variable (shared Go/Make).</summary>
        public string? CC { get; }

        /// <summary>The <c>CXX</c> environment variable (shared Go/Make).</summary>
        public string? CXX { get; }

        /// <summary>
        /// The <c>CGO_ENABLED</c> environment variable. A literal "0"
        /// disables cgo entirely (matching Go). Any other value — or
        /// being unset — defers to compiler availability.
        /// </summary>
        public string? CgoEnabled { get; }

        /// <summary>Merged <c>CGO_CFLAGS</c> then <c>CFLAGS</c>.</summary>
        public string? CFlags { get; }

        /// <summary>Merged <c>CGO_CPPFLAGS</c> then <c>CPPFLAGS</c>.</summary>
        public string? CppFlags { get; }

        /// <summary>Merged <c>CGO_CXXFLAGS</c> then <c>CXXFLAGS</c>.</summary>
        public string? CxxFlags { get; }

        /// <summary>Merged <c>CGO_LDFLAGS</c> then <c>LDFLAGS</c>.</summary>
        public string? LdFlags { get; }

        public static CgoEnvironment Load()
        {
            return new CgoEnvironment(
                cc: Environment.GetEnvironmentVariable("CC"),
                cxx: Environment.GetEnvironmentVariable("CXX"),
                cgoEnabled: Environment.GetEnvironmentVariable("CGO_ENABLED"),
                cFlags: Coalesce("CGO_CFLAGS", "CFLAGS"),
                cppFlags: Coalesce("CGO_CPPFLAGS", "CPPFLAGS"),
                cxxFlags: Coalesce("CGO_CXXFLAGS", "CXXFLAGS"),
                ldFlags: Coalesce("CGO_LDFLAGS", "LDFLAGS"));
        }

        private static string? Coalesce(string primary, string fallback)
        {
            string? value = Environment.GetEnvironmentVariable(primary);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
            return Environment.GetEnvironmentVariable(fallback);
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="Package.cs" company="Ziad">
//  Copyright 2016 Ziad
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Filepath
{
    /// <summary>
    /// Runtime support for Go's path/filepath package.
    /// </summary>
    [GoPackage("path/filepath")]
    public static class Package
    {
        /// <summary>filepath.Separator — OS-specific path separator.</summary>
        [GoConst(Type = "rune")]
        public static readonly int Separator = System.IO.Path.DirectorySeparatorChar;

        /// <summary>filepath.ListSeparator — OS-specific path list separator.</summary>
        [GoConst(Type = "rune")]
        public static readonly int ListSeparator = System.IO.Path.PathSeparator;

        /// <summary>filepath.SkipDir — sentinel error to skip directory in Walk.</summary>
        [GoVar(Type = "error")]
        public static readonly object SkipDir = "skip this directory";

        /// <summary>filepath.SkipAll — sentinel error to stop Walk entirely (Go 1.20).</summary>
        [GoVar(Type = "error")]
        public static readonly object SkipAll = "skip everything and stop the walk";

        /// <summary>filepath.ErrBadPattern — error for bad glob patterns.</summary>
        [GoVar(Type = "error")]
        public static readonly object ErrBadPattern = "syntax error in pattern";

        /// <summary>filepath.Join(elem ...string) string</summary>
        [GoFunc(IsVariadic = true)]
        public static string Join(params string[] elems)
        {
            return System.IO.Path.Combine(elems);
        }

        /// <summary>filepath.Dir(path string) string</summary>
        [GoFunc]
        public static string Dir(string path)
        {
            return System.IO.Path.GetDirectoryName(path) ?? ".";
        }

        /// <summary>filepath.Base(path string) string</summary>
        [GoFunc]
        public static string Base(string path)
        {
            var name = System.IO.Path.GetFileName(path);
            return name == "" ? "." : name;
        }

        /// <summary>filepath.Ext(path string) string</summary>
        [GoFunc]
        public static string Ext(string path)
        {
            return System.IO.Path.GetExtension(path);
        }

        /// <summary>filepath.Clean(path string) string</summary>
        [GoFunc]
        public static string Clean(string path)
        {
            return System.IO.Path.GetFullPath(path);
        }

        /// <summary>filepath.IsAbs(path string) bool</summary>
        [GoFunc]
        public static bool IsAbs(string path)
        {
            return System.IO.Path.IsPathRooted(path);
        }

        /// <summary>filepath.Abs(path string) (string, error)</summary>
        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, string) Abs(string path)
        {
            return (System.IO.Path.GetFullPath(path), "");
        }

        /// <summary>filepath.Rel(basepath, targpath string) (string, error)</summary>
        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, string) Rel(string basepath, string targpath)
        {
            try
            {
                return (System.IO.Path.GetRelativePath(basepath, targpath), "");
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        /// <summary>filepath.Match(pattern, name string) (matched bool, err error)</summary>
        [GoFunc]
        [return: GoReturn("bool", "error")]
        public static (bool, string) Match(string pattern, string name)
        {
            try
            {
                // Simple glob matching: * matches any sequence, ? matches one char
                bool matched = MatchGlob(pattern, name);
                return (matched, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>filepath.Glob(pattern string) (matches []string, err error)</summary>
        [GoFunc]
        [return: GoReturn("[]string", "error")]
        public static (Slice<string>, string) Glob(string pattern)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(pattern);
                var filePattern = System.IO.Path.GetFileName(pattern);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                var files = Directory.GetFiles(dir, filePattern);
                return (new Slice<string>(files), "");
            }
            catch (Exception ex)
            {
                return (new Slice<string>(Array.Empty<string>()), ex.Message);
            }
        }

        /// <summary>filepath.Split(path string) (dir, file string)</summary>
        [GoFunc]
        public static (string, string) Split(string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path) ?? "";
            var file = System.IO.Path.GetFileName(path);
            // Go's Split keeps the trailing separator on dir
            if (dir.Length > 0 && !dir.EndsWith(System.IO.Path.DirectorySeparatorChar) && !dir.EndsWith(System.IO.Path.AltDirectorySeparatorChar))
                dir += System.IO.Path.DirectorySeparatorChar;
            return (dir, file);
        }

        /// <summary>filepath.ToSlash(path string) string</summary>
        [GoFunc]
        public static string ToSlash(string path)
        {
            return path.Replace(System.IO.Path.DirectorySeparatorChar, '/');
        }

        /// <summary>filepath.FromSlash(path string) string</summary>
        [GoFunc]
        public static string FromSlash(string path)
        {
            return path.Replace('/', System.IO.Path.DirectorySeparatorChar);
        }

        /// <summary>filepath.HasPrefix(p, prefix string) bool — deprecated but still exported.</summary>
        [GoFunc]
        public static bool HasPrefix(string p, string prefix)
        {
            return p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>filepath.VolumeName(path string) string</summary>
        [GoFunc]
        public static string VolumeName(string path)
        {
            var root = System.IO.Path.GetPathRoot(path);
            // On Unix, root is "/" which is not a volume name in Go's sense
            if (root == "/" || root == null) return "";
            // On Windows, strip trailing backslash: "C:\" -> "C:"
            return root.TrimEnd(System.IO.Path.DirectorySeparatorChar);
        }

        /// <summary>filepath.EvalSymlinks(path string) (string, error)</summary>
        [GoFunc]
        [return: GoReturn("string", "error")]
        public static (string, string) EvalSymlinks(string path)
        {
            try
            {
                var resolved = System.IO.Path.GetFullPath(path);
                return (resolved, "");
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        /// <summary>
        /// filepath.Walk(root string, fn WalkFunc) error
        /// WalkFunc = func(path string, info os.FileInfo, err error) error
        /// Simplified: fn receives (path, nil, nil) for each entry.
        /// </summary>
        [GoFunc]
        [return: GoReturn("error")]
        public static object? Walk(string root, Action<string, object?, object?> fn)
        {
            try
            {
                WalkDirInternal(root, fn);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// filepath.WalkDir(root string, fn fs.WalkDirFunc) error
        /// WalkDirFunc = func(path string, d fs.DirEntry, err error) error
        /// Simplified: fn receives (path, nil, nil) for each entry.
        /// </summary>
        [GoFunc]
        [return: GoReturn("error")]
        public static object? WalkDir(string root, Action<string, object?, object?> fn)
        {
            try
            {
                WalkDirInternal(root, fn);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>filepath.SplitList(path string) []string</summary>
        [GoFunc]
        public static Slice<string> SplitList(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new Slice<string>(Array.Empty<string>());
            var parts = path.Split(System.IO.Path.PathSeparator);
            return new Slice<string>(parts);
        }

        /// <summary>filepath.IsLocal(path string) bool — Go 1.20</summary>
        [GoFunc]
        public static bool IsLocal(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (System.IO.Path.IsPathRooted(path)) return false;
            // Reject paths that escape via ".."
            var cleaned = System.IO.Path.GetFullPath(System.IO.Path.Combine(".", path));
            var cwd = System.IO.Path.GetFullPath(".");
            return cleaned.StartsWith(cwd, StringComparison.Ordinal);
        }

        private static void WalkDirInternal(string dir, Action<string, object?, object?> fn)
        {
            fn(dir, null, null);
            try
            {
                foreach (var entry in Directory.GetFileSystemEntries(dir))
                {
                    if (Directory.Exists(entry))
                        WalkDirInternal(entry, fn);
                    else
                        fn(entry, null, null);
                }
            }
            catch
            {
                // Skip directories we can't read
            }
        }

        // WalkFunc type: func(path string, info os.FileInfo, err error) error
        // Defined as a named type alias for the function signature

        private static bool MatchGlob(string pattern, string name)
        {
            int pi = 0, ni = 0;
            int starPi = -1, starNi = -1;

            while (ni < name.Length)
            {
                if (pi < pattern.Length && (pattern[pi] == '?' || pattern[pi] == name[ni]))
                {
                    pi++;
                    ni++;
                }
                else if (pi < pattern.Length && pattern[pi] == '*')
                {
                    starPi = pi;
                    starNi = ni;
                    pi++;
                }
                else if (starPi >= 0)
                {
                    pi = starPi + 1;
                    starNi++;
                    ni = starNi;
                }
                else
                {
                    return false;
                }
            }

            while (pi < pattern.Length && pattern[pi] == '*')
                pi++;

            return pi == pattern.Length;
        }
    }

    // filepath.WalkFunc is func(path string, info os.FileInfo, err error) error
    [GoType("named", Name = "WalkFunc", Package = "path/filepath", Underlying = "func(string, os.FileInfo, error) error")]
    public class GoWalkFunc
    {
    }
}

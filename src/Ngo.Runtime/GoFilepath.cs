// -----------------------------------------------------------------------
// <copyright file="GoFilepath.cs" company="Ziad">
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

using System.IO;

namespace Ngo.Runtime
{
    /// <summary>
    /// Runtime support for Go's path/filepath package.
    /// </summary>
    public static class GoFilepath
    {
        /// <summary>filepath.Join(elem ...string) string</summary>
        public static string Join(params string[] elems)
        {
            return Path.Combine(elems);
        }

        /// <summary>filepath.Dir(path string) string</summary>
        public static string Dir(string path)
        {
            return Path.GetDirectoryName(path) ?? ".";
        }

        /// <summary>filepath.Base(path string) string</summary>
        public static string Base(string path)
        {
            var name = Path.GetFileName(path);
            return name == "" ? "." : name;
        }

        /// <summary>filepath.Ext(path string) string</summary>
        public static string Ext(string path)
        {
            return Path.GetExtension(path);
        }

        /// <summary>filepath.Clean(path string) string</summary>
        public static string Clean(string path)
        {
            return Path.GetFullPath(path);
        }

        /// <summary>filepath.IsAbs(path string) bool</summary>
        public static bool IsAbs(string path)
        {
            return Path.IsPathRooted(path);
        }

        /// <summary>filepath.Abs(path string) (string, error)</summary>
        public static (string, string) Abs(string path)
        {
            return (Path.GetFullPath(path), "");
        }

        /// <summary>filepath.Rel(basepath, targpath string) (string, error)</summary>
        public static (string, string) Rel(string basepath, string targpath)
        {
            try
            {
                return (Path.GetRelativePath(basepath, targpath), "");
            }
            catch (System.Exception ex)
            {
                return ("", ex.Message);
            }
        }

        /// <summary>filepath.Match(pattern, name string) (matched bool, err error)</summary>
        public static (bool, string) Match(string pattern, string name)
        {
            try
            {
                // Simple glob matching: * matches any sequence, ? matches one char
                bool matched = MatchGlob(pattern, name);
                return (matched, "");
            }
            catch (System.Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>filepath.Glob(pattern string) (matches []string, err error)</summary>
        public static (Slice<string>, string) Glob(string pattern)
        {
            try
            {
                var dir = Path.GetDirectoryName(pattern);
                var filePattern = Path.GetFileName(pattern);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                var files = Directory.GetFiles(dir, filePattern);
                return (new Slice<string>(files), "");
            }
            catch (System.Exception ex)
            {
                return (new Slice<string>(System.Array.Empty<string>()), ex.Message);
            }
        }

        /// <summary>
        /// filepath.Walk(root string, fn WalkFunc) error
        /// WalkFunc = func(path string, info os.FileInfo, err error) error
        /// Simplified: fn receives (path, nil, nil) for each entry.
        /// </summary>
        public static object? Walk(string root, System.Action<string, object?, object?> fn)
        {
            try
            {
                WalkDir(root, fn);
                return null;
            }
            catch (System.Exception ex)
            {
                return ex.Message;
            }
        }

        private static void WalkDir(string dir, System.Action<string, object?, object?> fn)
        {
            fn(dir, null, null);
            try
            {
                foreach (var entry in Directory.GetFileSystemEntries(dir))
                {
                    if (Directory.Exists(entry))
                        WalkDir(entry, fn);
                    else
                        fn(entry, null, null);
                }
            }
            catch
            {
                // Skip directories we can't read
            }
        }

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
}

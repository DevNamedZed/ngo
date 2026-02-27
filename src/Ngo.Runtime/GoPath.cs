// -----------------------------------------------------------------------
// <copyright file="GoPath.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class GoPath
    {
        public static string Base(string path)
        {
            if (path == "") return ".";
            // Strip trailing slashes
            while (path.Length > 0 && path[path.Length - 1] == '/')
                path = path.Substring(0, path.Length - 1);
            if (path == "") return "/";
            int i = path.LastIndexOf('/');
            if (i >= 0) path = path.Substring(i + 1);
            return path;
        }

        public static string Dir(string path)
        {
            int i = path.LastIndexOf('/');
            if (i < 0) return ".";
            var dir = path.Substring(0, i);
            // Strip trailing slashes
            while (dir.Length > 1 && dir[dir.Length - 1] == '/')
                dir = dir.Substring(0, dir.Length - 1);
            if (dir == "") return "/";
            return dir;
        }

        public static string Ext(string path)
        {
            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (path[i] == '.') return path.Substring(i);
                if (path[i] == '/') break;
            }
            return "";
        }

        public static string Join(string a, string b)
        {
            if (a == "") return b;
            if (b == "") return a;
            return a.TrimEnd('/') + "/" + b.TrimStart('/');
        }

        public static string Clean(string path)
        {
            if (path == "") return ".";
            return path;
        }

        public static bool IsAbs(string path)
        {
            return path.Length > 0 && path[0] == '/';
        }
    }
}

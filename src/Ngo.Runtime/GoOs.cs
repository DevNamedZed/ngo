// -----------------------------------------------------------------------
// <copyright file="GoOs.cs" company="Ziad">
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

namespace Ngo.Runtime
{
    public static class GoOs
    {
        // os.Args []string — returns command-line arguments
        public static Slice<string> Args
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                return new Slice<string>(args);
            }
        }

        public static void Exit(long code)
        {
            Environment.Exit((int)code);
        }

        public static string Getenv(string key)
        {
            return Environment.GetEnvironmentVariable(key) ?? "";
        }

        public static void Setenv(string key, string value)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        // os.Create(name string) (*File, error)
        public static (GoFile, object?) Create(string name)
        {
            try
            {
                var stream = File.Create(name);
                return (new GoFile(stream, name), null);
            }
            catch (Exception ex)
            {
                return (GoFile.Null, ex.Message);
            }
        }

        // os.Open(name string) (*File, error)
        public static (GoFile, object?) Open(string name)
        {
            try
            {
                var stream = File.OpenRead(name);
                return (new GoFile(stream, name), null);
            }
            catch (Exception ex)
            {
                return (GoFile.Null, ex.Message);
            }
        }

        // os.ReadFile(name string) ([]byte, error)
        public static (Slice<byte>, object?) ReadFile(string name)
        {
            try
            {
                var bytes = File.ReadAllBytes(name);
                return (new Slice<byte>(bytes), null);
            }
            catch (Exception ex)
            {
                return (new Slice<byte>(Array.Empty<byte>()), ex.Message);
            }
        }

        // os.WriteFile(name string, data []byte, perm FileMode) error
        public static object? WriteFile(string name, Slice<byte> data, long perm)
        {
            try
            {
                var bytes = new byte[data.Len];
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] = data[i];
                File.WriteAllBytes(name, bytes);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Remove(name string) error
        public static object? Remove(string name)
        {
            try
            {
                File.Delete(name);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.MkdirAll(path string, perm FileMode) error
        public static object? MkdirAll(string path, long perm)
        {
            try
            {
                Directory.CreateDirectory(path);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // os.Getwd() (string, error)
        public static (string, object?) Getwd()
        {
            try
            {
                return (Directory.GetCurrentDirectory(), null);
            }
            catch (Exception ex)
            {
                return ("", ex.Message);
            }
        }

        // os.Stdin, os.Stdout, os.Stderr
        public static readonly GoFile Stdin = new GoFile(Console.OpenStandardInput(), "/dev/stdin");
        public static readonly GoFile Stdout = new GoFile(Console.OpenStandardOutput(), "/dev/stdout");
        public static readonly GoFile Stderr = new GoFile(Console.OpenStandardError(), "/dev/stderr");
    }

    /// <summary>
    /// Represents Go's *os.File, implementing IGoReader, IGoWriter, IGoCloser.
    /// </summary>
    public sealed class GoFile : IGoReader, IGoWriter, IGoCloser
    {
        private readonly Stream? _stream;
        private readonly string _name;

        public static readonly GoFile Null = new GoFile(null, "<nil>");

        public GoFile(Stream? stream, string name)
        {
            _stream = stream;
            _name = name;
        }

        public string Name() => _name;

        public (int, string) Read(Slice<byte> p)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                var buf = new byte[p.Len];
                int n = _stream.Read(buf, 0, buf.Length);
                for (int i = 0; i < n; i++)
                    p[i] = buf[i];
                if (n == 0) return (0, GoIo.EOF);
                return (n, "");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        public (int, string) Write(Slice<byte> p)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                var buf = new byte[p.Len];
                for (int i = 0; i < buf.Length; i++)
                    buf[i] = p[i];
                _stream.Write(buf, 0, buf.Length);
                return (buf.Length, "");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        // WriteString writes a string directly
        public (long, string) WriteString(string s)
        {
            if (_stream == null) return (0, "os: file is nil");
            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(s);
                _stream.Write(bytes, 0, bytes.Length);
                return (bytes.Length, "");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        public string Close()
        {
            if (_stream == null) return "";
            try
            {
                _stream.Close();
                return "";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public override string ToString() => $"&{{{_name}}}";
    }
}

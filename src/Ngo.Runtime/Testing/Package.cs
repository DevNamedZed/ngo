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
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Testing
{
    [GoPackage("testing")]
    public static class Package
    {
        private static bool? _verbose;
        private static bool? _short;

        [GoFunc]
        public static bool Verbose()
        {
            if (_verbose.HasValue)
            {
                return _verbose.Value;
            }
            // Check environment variable (set by test runner)
            string? envVerbose = System.Environment.GetEnvironmentVariable("NGO_TEST_VERBOSE");
            if (envVerbose == "1" || envVerbose == "true")
            {
                _verbose = true;
                return true;
            }
            // Check command line args for -test.v
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "-test.v" || arg == "-v")
                {
                    _verbose = true;
                    return true;
                }
            }
            _verbose = false;
            return false;
        }

        [GoFunc]
        public static bool Short()
        {
            if (_short.HasValue)
            {
                return _short.Value;
            }
            string? envShort = System.Environment.GetEnvironmentVariable("NGO_TEST_SHORT");
            if (envShort == "1" || envShort == "true")
            {
                _short = true;
                return true;
            }
            foreach (var arg in System.Environment.GetCommandLineArgs())
            {
                if (arg == "-test.short" || arg == "-short")
                {
                    _short = true;
                    return true;
                }
            }
            _short = false;
            return false;
        }

        [GoFunc]
        [return: GoReturn("*testing.M")]
        public static GoTestingM MainStart(object? deps, Slice<object?> tests, Slice<object?> benchmarks, Slice<object?> fuzzTargets, Slice<object?> examples)
            => new GoTestingM();
    }

    // testing.M struct
    [GoType("struct", Name = "M", Package = "testing")]
    public class GoTestingM
    {
        [GoMethod]
        public long Run() => 0;
    }

    // testing.TB interface
    [GoType("interface", Name = "TB", Package = "testing")]
    public interface IGoTestingTB
    {
        void Error(params object[] args);
        void Errorf(string format, params object[] args);
        void Fail();
        void FailNow();
        bool Failed();
        void Fatal(params object[] args);
        void Fatalf(string format, params object[] args);
        void Helper();
        void Log(params object[] args);
        void Logf(string format, params object[] args);
        string Name();
        void Skip(params object[] args);
        void Skipf(string format, params object[] args);
        void SkipNow();
        bool Skipped();
        string TempDir();
    }
}

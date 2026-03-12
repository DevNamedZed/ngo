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

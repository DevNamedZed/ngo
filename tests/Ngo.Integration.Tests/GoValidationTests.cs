// -----------------------------------------------------------------------
// <copyright file="GoValidationTests.cs" company="Ziad">
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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Integration.Tests;

[TestClass]
public class GoValidationTests : PackageTestBase
{
    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoArgon2()
    {
        // alexedwards/argon2id — uses crypto, may not pass but let's check
        var dir = EnsureModule("github.com/alexedwards/argon2id", "v0.0.0-20230305115115-4b3c3280a736");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("argon2id", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoValidate()
    {
        // asaskevich/govalidator — validators and sanitizers
        var dir = EnsureModule("github.com/asaskevich/govalidator", "v0.0.0-20230301143203-a9d515a09cc2");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("govalidator", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoSemver()
    {
        // Masterminds/semver/v3 — semver parsing (already have one, try different)
        var dir = EnsureModule("github.com/Masterminds/semver/v3", "v3.2.1");
        var errors = AnalyzePackageDir(dir);
        AssertZeroErrors("semver-v3", errors);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void Analyze_GoColor()
    {
        // gookit/color — terminal color output
        var dir = EnsureModule("github.com/gookit/color", "v1.5.4");
        var errors = AnalyzePackageDir(dir);
        DumpErrors("gookit-color", errors);
    }
}

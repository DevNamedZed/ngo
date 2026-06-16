// -----------------------------------------------------------------------
// <copyright file="ClrTypeNameTests.cs" company="Ziad">
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Emit;

namespace Ngo.Compiler.Tests.Emit;

[TestClass]
public class ClrTypeNameTests
{
    [DataTestMethod]
    [DataRow("struct{table [32]affineLookupTable;initOnce Once}")]
    [DataRow("struct{Once Once;v map[string]reflect.Value}")]
    [DataRow("struct{List []covMetaBlob;Hash [16]byte}")]
    [DataRow("plainIdentifier123")]
    [DataRow("struct{}")]
    [DataRow("")]
    public void EscapeProducesALegalIdentifier(string fingerprint)
    {
        var encoded = ClrTypeName.Escape(fingerprint);

        Assert.IsTrue(encoded.All(IsLegalIdentifierCharacter),
            $"encoded name '{encoded}' contains a character that is illegal in a .NET type-name segment");
        Assert.IsFalse(encoded.Contains('['), "encoded name must not contain the reserved '['");
    }

    [DataTestMethod]
    [DataRow("struct{table [32]affineLookupTable;initOnce Once}")]
    [DataRow("struct{Once Once;v map[string]reflect.Value}")]
    [DataRow("func(int, string) (bool, error)")]
    [DataRow("snake_case_name")]
    [DataRow("")]
    public void EscapeRoundTrips(string fingerprint)
    {
        Assert.AreEqual(fingerprint, ClrTypeName.Unescape(ClrTypeName.Escape(fingerprint)));
    }

    [TestMethod]
    public void EscapeIsDeterministic()
    {
        const string fingerprint = "struct{table [32]affineLookupTable;initOnce Once}";
        Assert.AreEqual(ClrTypeName.Escape(fingerprint), ClrTypeName.Escape(fingerprint));
    }

    [TestMethod]
    public void StructsThatTruncateToTheSamePrefixEncodeDistinctly()
    {
        // Both names share the prefix "struct{table " — the bug truncated at the first '[',
        // collapsing them onto one another. The encoding must keep them distinct.
        var first = ClrTypeName.Escape("struct{table [32]affineLookupTable;initOnce Once}");
        var second = ClrTypeName.Escape("struct{table nafLookupTable8;initOnce Once}");
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void AsciiLettersAndDigitsPassThrough()
    {
        Assert.AreEqual("Once123", ClrTypeName.Escape("Once123"));
    }

    private static bool IsLegalIdentifierCharacter(char character)
    {
        return (character >= 'A' && character <= 'Z')
            || (character >= 'a' && character <= 'z')
            || (character >= '0' && character <= '9')
            || character == '_';
    }
}

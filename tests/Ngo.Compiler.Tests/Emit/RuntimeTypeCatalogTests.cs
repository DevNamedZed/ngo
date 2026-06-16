// -----------------------------------------------------------------------
// <copyright file="RuntimeTypeCatalogTests.cs" company="Ziad">
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
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ngo.Compiler.Emit;
using Ngo.Runtime;
using Ngo.Runtime.Discovery;

namespace Ngo.Compiler.Tests.Emit;

[TestClass]
public class RuntimeTypeCatalogTests
{
    private static Assembly RuntimeAssembly => typeof(Slice<>).Assembly;

    private static RuntimeTypeCatalog Catalog() => new(RuntimeAssembly);

    [TestMethod]
    public void ResolvesByClrFullName()
    {
        Assert.AreSame(typeof(Slice<>), Catalog().ResolveByClrFullName(typeof(Slice<>).FullName!));
    }

    [TestMethod]
    public void ResolvesGenericTypeByBareShortName()
    {
        // Slice`1 is also indexed under "Slice" so a Go-name lookup finds the arity-suffixed type.
        Assert.AreSame(typeof(Slice<>), Catalog().ResolveByShortNameInNamespace("Slice", "Ngo.Runtime"));
        Assert.AreSame(typeof(Slice<>), Catalog().ResolveByShortNameInNamespace("Slice`1", "Ngo.Runtime"));
    }

    [TestMethod]
    public void ResolvesEveryGoTypeAttributeByName()
    {
        var catalog = Catalog();
        foreach (var type in RuntimeAssembly.GetTypes())
        {
            var goType = type.GetCustomAttribute<GoTypeAttribute>();
            if (goType?.Name == null)
            {
                continue;
            }
            // First-wins index: the resolved type must itself carry that Go name.
            var resolved = catalog.ResolveByGoTypeName(goType.Name);
            Assert.IsNotNull(resolved, $"GoType '{goType.Name}' did not resolve");
            Assert.AreEqual(goType.Name, resolved!.GetCustomAttribute<GoTypeAttribute>()!.Name);
        }
    }

    [TestMethod]
    public void ResolvesEveryGoPackageClassByImportPath()
    {
        var catalog = Catalog();
        foreach (var type in RuntimeAssembly.GetTypes())
        {
            var goPackage = type.GetCustomAttribute<GoPackageAttribute>();
            if (goPackage == null)
            {
                continue;
            }
            Assert.AreSame(type, catalog.ResolvePackageClass(goPackage.ImportPath));
            Assert.AreSame(type, catalog.ResolveByGoPackageAndName(goPackage.ImportPath, type.Name));
        }
    }

    [TestMethod]
    public void MissesReturnNull()
    {
        var catalog = Catalog();
        Assert.IsNull(catalog.ResolveByClrFullName("Ngo.Runtime.NoSuchType"));
        Assert.IsNull(catalog.ResolveByGoTypeName("NoSuchGoType"));
        Assert.IsNull(catalog.ResolvePackageClass("no/such/package"));
        Assert.IsNull(catalog.ResolveByShortNameInNamespace("Slice`1", "System.Wrong"));
    }
}

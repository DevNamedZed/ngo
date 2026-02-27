// -----------------------------------------------------------------------
// <copyright file="PtrTests.cs" company="Ziad">
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

namespace Ngo.Runtime.Tests;

[TestClass]
public class PtrTests
{
    [TestMethod]
    public void Default_value()
    {
        var p = new Ptr<int>();
        Assert.AreEqual(0, p.Value);
    }

    [TestMethod]
    public void Initial_value()
    {
        var p = new Ptr<int>(42);
        Assert.AreEqual(42, p.Value);
    }

    [TestMethod]
    public void Mutate_through_pointer()
    {
        var p = new Ptr<int>(10);
        p.Value = 20;
        Assert.AreEqual(20, p.Value);
    }

    [TestMethod]
    public void Shared_reference()
    {
        var p = new Ptr<int>(10);
        var alias = p;
        alias.Value = 99;
        Assert.AreEqual(99, p.Value); // same object
    }

    [TestMethod]
    public void Struct_pointer()
    {
        var p = new Ptr<Point>(new Point { X = 1, Y = 2 });
        Assert.AreEqual(1, p.Value.X);
        Assert.AreEqual(2, p.Value.Y);

        p.Value = new Point { X = 10, Y = 20 };
        Assert.AreEqual(10, p.Value.X);
    }

    [TestMethod]
    public void ToString_format()
    {
        var p = new Ptr<int>(42);
        Assert.AreEqual("&42", p.ToString());
    }

    private struct Point
    {
        public int X;
        public int Y;
    }
}

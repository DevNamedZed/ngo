// -----------------------------------------------------------------------
// <copyright file="GoroutineTests.cs" company="Ziad">
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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Runtime.Tests;

[TestClass]
public class GoroutineTests
{
    [TestMethod]
    public void Go_runs_action()
    {
        int value = 0;
        Goroutine.Go(() => Interlocked.Exchange(ref value, 42));
        Assert.IsTrue(Goroutine.WaitAll(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(42, value);
    }

    [TestMethod]
    public void Go_with_argument()
    {
        int value = 0;
        Goroutine.Go<int>(x => Interlocked.Exchange(ref value, x), 99);
        Assert.IsTrue(Goroutine.WaitAll(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(99, value);
    }

    [TestMethod]
    public void Go_multiple_goroutines()
    {
        int counter = 0;
        for (int i = 0; i < 10; i++)
        {
            Goroutine.Go(() => Interlocked.Increment(ref counter));
        }

        Assert.IsTrue(Goroutine.WaitAll(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(10, counter);
    }

    [TestMethod]
    public void Go_panic_does_not_crash_program()
    {
        int value = 0;
        Goroutine.Go(() => throw new GoPanicException("boom"));
        Goroutine.Go(() => Interlocked.Exchange(ref value, 1));
        Assert.IsTrue(Goroutine.WaitAll(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, value); // second goroutine still ran
    }

    [TestMethod]
    public void Go_with_channel_communication()
    {
        var ch = new Channel<int>(1);
        Goroutine.Go(() => ch.Send(42));
        var (val, ok) = ch.Receive();
        Assert.AreEqual(42, val);
        Assert.IsTrue(ok);
        Assert.IsTrue(Goroutine.WaitAll(TimeSpan.FromSeconds(5)));
    }
}

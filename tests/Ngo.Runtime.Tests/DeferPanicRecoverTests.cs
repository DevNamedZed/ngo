// -----------------------------------------------------------------------
// <copyright file="DeferPanicRecoverTests.cs" company="Ziad">
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Runtime.Tests;

[TestClass]
public class DeferPanicRecoverTests
{
    [TestMethod]
    public void Defer_executes_in_lifo_order()
    {
        var stack = new DeferStack();
        var order = new List<int>();
        stack.Push(() => order.Add(1));
        stack.Push(() => order.Add(2));
        stack.Push(() => order.Add(3));
        stack.Execute();
        CollectionAssert.AreEqual(new[] { 3, 2, 1 }, order);
    }

    [TestMethod]
    public void Defer_empty_stack_is_noop()
    {
        var stack = new DeferStack();
        stack.Execute(); // should not throw
    }

    [TestMethod]
    public void Defer_count()
    {
        var stack = new DeferStack();
        Assert.AreEqual(0, stack.Count);
        stack.Push(() => { });
        Assert.AreEqual(1, stack.Count);
        stack.Push(() => { });
        Assert.AreEqual(2, stack.Count);
        stack.Execute();
        Assert.AreEqual(0, stack.Count);
    }

    [TestMethod]
    public void Defer_panic_in_deferred_func_still_runs_remaining()
    {
        var stack = new DeferStack();
        var order = new List<int>();
        stack.Push(() => order.Add(1));
        stack.Push(() => throw new GoPanicException("boom"));
        stack.Push(() => order.Add(3));

        var ex = Assert.ThrowsException<GoPanicException>(() => stack.Execute());
        Assert.AreEqual("boom", ex.Value);
        CollectionAssert.AreEqual(new[] { 3, 1 }, order); // deferred 2 panicked, but 1 still ran
    }

    [TestMethod]
    public void Panic_exception_stores_value()
    {
        var ex = new GoPanicException("test panic");
        Assert.AreEqual("test panic", ex.Value);
        StringAssert.Contains(ex.Message, "test panic");
    }

    [TestMethod]
    public void Panic_exception_nil_value()
    {
        var ex = new GoPanicException(null);
        Assert.IsNull(ex.Value);
    }

    [TestMethod]
    public void Recover_returns_null_when_no_panic()
    {
        var result = GoRecover.Recover();
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Recover_in_deferred_function_during_panic()
    {
        var stack = new DeferStack();
        object? recovered = null;
        stack.Push(() =>
        {
            recovered = GoRecover.Recover();
        });

        var panic = new GoPanicException("boom");
        stack.ExecuteWithRecover(panic);
        Assert.AreEqual("boom", recovered);
    }

    [TestMethod]
    public void Recover_stops_panic_propagation()
    {
        var stack = new DeferStack();
        stack.Push(() =>
        {
            GoRecover.Recover();
        });

        var panic = new GoPanicException("boom");
        var result = stack.ExecuteWithRecover(panic);
        Assert.AreEqual("boom", result); // recovered successfully
    }

    [TestMethod]
    public void Defer_with_recover_pattern()
    {
        // Simulates: defer func() { if r := recover(); r != nil { ... } }()
        var stack = new DeferStack();
        object? caught = null;
        stack.Push(() =>
        {
            var r = GoRecover.Recover();
            if (r != null) caught = r;
        });

        var panic = new GoPanicException(42);
        stack.ExecuteWithRecover(panic);
        Assert.AreEqual(42, caught);
    }
}

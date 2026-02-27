// -----------------------------------------------------------------------
// <copyright file="ChannelTests.cs" company="Ziad">
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ngo.Runtime.Tests;

[TestClass]
public class ChannelTests
{
    [TestMethod]
    public void Buffered_channel_send_receive()
    {
        var ch = new Channel<int>(3);
        ch.Send(1);
        ch.Send(2);
        ch.Send(3);

        Assert.AreEqual(3, ch.Length);
        Assert.AreEqual((1, true), ch.Receive());
        Assert.AreEqual((2, true), ch.Receive());
        Assert.AreEqual((3, true), ch.Receive());
        Assert.AreEqual(0, ch.Length);
    }

    [TestMethod]
    public async Task Unbuffered_channel_rendezvous()
    {
        var ch = new Channel<int>();
        int received = 0;

        var sender = Task.Run(() => ch.Send(42));
        var receiver = Task.Run(() =>
        {
            var (val, ok) = ch.Receive();
            received = val;
        });

        await Task.WhenAll(sender, receiver).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(42, received);
    }

    [TestMethod]
    public void Close_wakes_receivers()
    {
        var ch = new Channel<int>(1);
        ch.Send(10);
        ch.Close();

        // Buffered value is still available
        Assert.AreEqual((10, true), ch.Receive());
        // After buffer empty and closed, receive returns (default, false)
        Assert.AreEqual((0, false), ch.Receive());
    }

    [TestMethod]
    public void Send_on_closed_channel_panics()
    {
        var ch = new Channel<int>(1);
        ch.Close();
        Assert.ThrowsException<GoPanicException>(() => ch.Send(1));
    }

    [TestMethod]
    public void Close_already_closed_panics()
    {
        var ch = new Channel<int>(1);
        ch.Close();
        Assert.ThrowsException<GoPanicException>(() => ch.Close());
    }

    [TestMethod]
    public void Capacity_property()
    {
        var unbuffered = new Channel<int>();
        Assert.AreEqual(0, unbuffered.Capacity);

        var buffered = new Channel<int>(5);
        Assert.AreEqual(5, buffered.Capacity);
    }

    [TestMethod]
    public async Task Multiple_senders_receivers()
    {
        var ch = new Channel<int>(10);
        var received = new List<int>();

        var senders = new Task[5];
        for (int i = 0; i < 5; i++)
        {
            int val = i;
            senders[i] = Task.Run(() => ch.Send(val));
        }

        await Task.WhenAll(senders).WaitAsync(TimeSpan.FromSeconds(5));

        for (int i = 0; i < 5; i++)
        {
            var (val, ok) = ch.Receive();
            Assert.IsTrue(ok);
            received.Add(val);
        }

        Assert.AreEqual(5, received.Count);
        received.Sort();
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, received);
    }

    [TestMethod]
    public async Task Unbuffered_blocks_sender_until_receiver()
    {
        var ch = new Channel<int>();
        var sent = false;

        var sender = Task.Run(() =>
        {
            ch.Send(1);
            Volatile.Write(ref sent, true);
        });

        // Give sender time to start blocking
        await Task.Delay(50);
        Assert.IsFalse(Volatile.Read(ref sent)); // sender should be blocked

        var (val, ok) = ch.Receive(); // unblock sender
        await sender.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(Volatile.Read(ref sent));
        Assert.AreEqual(1, val);
        Assert.IsTrue(ok);
    }

    [TestMethod]
    public async Task Close_wakes_blocked_receivers()
    {
        var ch = new Channel<int>();
        bool receiverDone = false;
        bool receivedOk = true;

        var receiver = Task.Run(() =>
        {
            var (_, ok) = ch.Receive();
            Volatile.Write(ref receivedOk, ok);
            Volatile.Write(ref receiverDone, true);
        });

        await Task.Delay(50); // let receiver block
        ch.Close();
        await receiver.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(Volatile.Read(ref receiverDone));
        Assert.IsFalse(Volatile.Read(ref receivedOk));
    }

    [TestMethod]
    public void TrySend_buffered_succeeds()
    {
        var ch = new Channel<int>(1);
        Assert.IsTrue(ch.TrySend(42));
        Assert.AreEqual((42, true), ch.Receive());
    }

    [TestMethod]
    public void TrySend_full_buffer_returns_false()
    {
        var ch = new Channel<int>(1);
        ch.Send(1);
        Assert.IsFalse(ch.TrySend(2));
    }

    [TestMethod]
    public void TrySend_unbuffered_no_receiver_returns_false()
    {
        var ch = new Channel<int>();
        Assert.IsFalse(ch.TrySend(42));
    }

    [TestMethod]
    public void TryReceive_buffered_succeeds()
    {
        var ch = new Channel<int>(1);
        ch.Send(10);
        var (value, ok, completed) = ch.TryReceive();
        Assert.IsTrue(completed);
        Assert.IsTrue(ok);
        Assert.AreEqual(10, value);
    }

    [TestMethod]
    public void TryReceive_empty_returns_not_completed()
    {
        var ch = new Channel<int>();
        var (_, _, completed) = ch.TryReceive();
        Assert.IsFalse(completed);
    }

    [TestMethod]
    public void TryReceive_closed_empty_returns_completed_not_ok()
    {
        var ch = new Channel<int>();
        ch.Close();
        var (_, ok, completed) = ch.TryReceive();
        Assert.IsTrue(completed);
        Assert.IsFalse(ok);
    }
}

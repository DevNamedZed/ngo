using System;
using System.Collections.Generic;
using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os.Signal
{
    /// <summary>
    /// Runtime support for Go's os/signal package.
    /// Maps Console.CancelKeyPress (SIGINT/Ctrl+C) and AppDomain.ProcessExit (SIGTERM) to Go channel notifications.
    /// </summary>
    [GoPackage("os/signal")]
    public static class Package
    {
        private static readonly object _lock = new object();
        private static readonly List<SignalRegistration> _registrations = new List<SignalRegistration>();
        private static bool _cancelKeyPressHooked;
        private static bool _processExitHooked;

        // Go signal constants
        private const int SIGINT = 2;
        private const int SIGTERM = 15;

        [GoFunc(IsVariadic = true)]
        public static void Notify(object c, params object[] sig)
        {
            if (c == null)
            {
                return;
            }

            lock (_lock)
            {
                var reg = new SignalRegistration { Channel = c };
                if (sig != null && sig.Length > 0)
                {
                    foreach (var s in sig)
                    {
                        int sigNum = ExtractSignalNumber(s);
                        if (sigNum > 0)
                        {
                            reg.Signals.Add(sigNum);
                        }
                    }
                }
                // If no signals specified, catch all
                if (reg.Signals.Count == 0)
                {
                    reg.CatchAll = true;
                }
                _registrations.Add(reg);

                EnsureHooked(reg);
            }
        }

        [GoFunc]
        public static void Stop(object c)
        {
            if (c == null)
            {
                return;
            }

            lock (_lock)
            {
                _registrations.RemoveAll(r => ReferenceEquals(r.Channel, c));
            }
        }

        [GoFunc(IsVariadic = true)]
        public static void Reset(params object[] sig)
        {
            lock (_lock)
            {
                if (sig == null || sig.Length == 0)
                {
                    _registrations.Clear();
                    return;
                }

                var sigNums = new HashSet<int>();
                foreach (var s in sig)
                {
                    int num = ExtractSignalNumber(s);
                    if (num > 0)
                    {
                        sigNums.Add(num);
                    }
                }

                foreach (var reg in _registrations)
                {
                    foreach (var num in sigNums)
                    {
                        reg.Signals.Remove(num);
                    }
                }
                _registrations.RemoveAll(r => !r.CatchAll && r.Signals.Count == 0);
            }
        }

        [GoFunc(IsVariadic = true)]
        public static void Ignore(params object[] sig)
        {
            // Mark signals as ignored — for now just remove registrations for these signals
            Reset(sig);
        }

        [GoFunc(IsVariadic = true)]
        public static (object, object) NotifyContext(object parent, params object[] signals)
        {
            if (parent is not Context.GoContext parentCtx)
            {
                throw new InvalidOperationException("os/signal.NotifyContext: parent must be a context.Context");
            }

            // Create a context that is canceled when the signal is received
            var (ctx, cancel) = Context.GoContext.WithCancel(parentCtx);

            // Create a channel and register for signals
            var ch = new Channel<IGoOsSignal>(1);
            Notify(ch, signals);

            // Start a goroutine to wait for signal and cancel context
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var trySend = ch.GetType().GetMethod("Receive");
                if (trySend != null)
                {
                    trySend.Invoke(ch, null);
                }
                cancel();
                Stop(ch);
            });

            return (ctx, (Action)cancel);
        }

        private static void EnsureHooked(SignalRegistration reg)
        {
            bool needsCancelKey = reg.CatchAll || reg.Signals.Contains(SIGINT);
            bool needsProcessExit = reg.CatchAll || reg.Signals.Contains(SIGTERM);

            if (needsCancelKey && !_cancelKeyPressHooked)
            {
                Console.CancelKeyPress += OnCancelKeyPress;
                _cancelKeyPressHooked = true;
            }

            if (needsProcessExit && !_processExitHooked)
            {
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                _processExitHooked = true;
            }
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            DispatchSignal(SIGINT);
        }

        private static void OnProcessExit(object? sender, EventArgs e)
        {
            DispatchSignal(SIGTERM);
        }

        private static void DispatchSignal(int sigNum)
        {
            List<SignalRegistration> regs;
            lock (_lock)
            {
                regs = new List<SignalRegistration>(_registrations);
            }

            string sigName = sigNum switch
            {
                SIGINT => "interrupt",
                SIGTERM => "terminated",
                _ => $"signal {sigNum}"
            };
            var signal = new GoOsSignal(sigNum, sigName);

            foreach (var reg in regs)
            {
                if (reg.CatchAll || reg.Signals.Contains(sigNum))
                {
                    TrySendSignal(reg.Channel, signal);
                }
            }
        }

        private static void TrySendSignal(object channel, GoOsSignal signal)
        {
            // Try to send via Channel<GoOsSignal>.TrySend
            var chanType = channel.GetType();
            var trySendMethod = chanType.GetMethod("TrySend");
            if (trySendMethod != null)
            {
                trySendMethod.Invoke(channel, new object[] { signal });
            }
        }

        private static int ExtractSignalNumber(object? s)
        {
            if (s is GoOsSignal)
            {
                // GoOsSignal doesn't expose signum publicly, but we can match by name
                string name = s.ToString() ?? "";
                if (name == "interrupt")
                {
                    return SIGINT;
                }
                if (name == "terminated" || name == "killed")
                {
                    return SIGTERM;
                }
                return 0;
            }
            if (s is long l)
            {
                return (int)l;
            }
            if (s is int i)
            {
                return i;
            }
            return 0;
        }

        private class SignalRegistration
        {
            public object Channel = null!;
            public HashSet<int> Signals = new HashSet<int>();
            public bool CatchAll;
        }
    }
}

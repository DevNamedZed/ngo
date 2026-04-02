using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Ngo.Runtime.Net
{
    internal class GoPipeConn : IGoNetConn
    {
        private readonly BlockingCollection<byte[]> _readQueue;
        private readonly BlockingCollection<byte[]> _writeQueue;
        private byte[]? _currentChunk;
        private int _currentOffset;
        private volatile bool _closed;
        private readonly string _name;
        private int _readTimeoutMs;
        private int _writeTimeoutMs;

        internal GoPipeConn(BlockingCollection<byte[]> readQueue, BlockingCollection<byte[]> writeQueue, string name)
        {
            _readQueue = readQueue;
            _writeQueue = writeQueue;
            _name = name;
        }

        public (long, string) Read(Slice<byte> b)
        {
            if (_closed)
            {
                return (0, "EOF");
            }

            int totalRead = 0;
            while (totalRead < b.Len)
            {
                if (_currentChunk == null || _currentOffset >= _currentChunk.Length)
                {
                    try
                    {
                        if (!_readQueue.TryTake(out _currentChunk, 100))
                        {
                            if (_closed)
                            {
                                return totalRead > 0 ? (totalRead, null!) : (0, "EOF");
                            }
                            if (totalRead > 0)
                            {
                                return (totalRead, null!);
                            }
                            // Block waiting
                            _currentChunk = _readQueue.Take();
                        }
                        _currentOffset = 0;
                    }
                    catch (InvalidOperationException)
                    {
                        return totalRead > 0 ? (totalRead, null!) : (0, "EOF");
                    }
                }

                if (_currentChunk != null)
                {
                    int available = _currentChunk.Length - _currentOffset;
                    int toCopy = global::System.Math.Min(available, b.Len - totalRead);
                    for (int i = 0; i < toCopy; i++)
                    {
                        b[totalRead + i] = _currentChunk[_currentOffset + i];
                    }
                    _currentOffset += toCopy;
                    totalRead += toCopy;
                }
            }
            return (totalRead, null!);
        }

        public (long, string) Write(Slice<byte> b)
        {
            if (_closed)
            {
                return (0, "io: write on closed pipe");
            }

            var chunk = new byte[b.Len];
            for (int i = 0; i < b.Len; i++)
            {
                chunk[i] = b[i];
            }

            try
            {
                _writeQueue.Add(chunk);
                return (b.Len, null!);
            }
            catch (InvalidOperationException)
            {
                return (0, "io: write on closed pipe");
            }
        }

        public string Close()
        {
            _closed = true;
            _readQueue.CompleteAdding();
            _writeQueue.CompleteAdding();
            return null!;
        }

        public IGoNetAddr LocalAddr() => new PipeAddr();
        public IGoNetAddr RemoteAddr() => new PipeAddr();
        public string SetDeadline(object t)
        {
            SetReadDeadline(t);
            SetWriteDeadline(t);
            return null!;
        }

        public string SetReadDeadline(object t)
        {
            if (t is Ngo.Runtime.Time.GoTimeValue timeVal)
            {
                var duration = timeVal.Sub(Ngo.Runtime.Time.GoTime.Now());
                _readTimeoutMs = duration > 0 ? (int)(duration / 1_000_000) : 1;
            }
            else
            {
                _readTimeoutMs = 0;
            }
            return null!;
        }

        public string SetWriteDeadline(object t)
        {
            if (t is Ngo.Runtime.Time.GoTimeValue timeVal)
            {
                var duration = timeVal.Sub(Ngo.Runtime.Time.GoTime.Now());
                _writeTimeoutMs = duration > 0 ? (int)(duration / 1_000_000) : 1;
            }
            else
            {
                _writeTimeoutMs = 0;
            }
            return null!;
        }

        internal static (GoPipeConn, GoPipeConn) CreatePair()
        {
            var queueA = new BlockingCollection<byte[]>(64);
            var queueB = new BlockingCollection<byte[]>(64);
            var connA = new GoPipeConn(queueB, queueA, "pipe-a");
            var connB = new GoPipeConn(queueA, queueB, "pipe-b");
            return (connA, connB);
        }
    }

    internal class PipeAddr : IGoNetAddr
    {
        public string Network() => "pipe";
        public string String() => "pipe";
    }
}

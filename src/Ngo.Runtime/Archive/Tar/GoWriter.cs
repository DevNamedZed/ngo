using System;
using System.Formats.Tar;
using System.IO;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Archive.Tar
{
    [GoType("struct", Name = "Writer", Package = "archive/tar")]
    public class GoWriter
    {
        private readonly TarWriter? _writer;
        private readonly GoWriterStream? _stream;
        private MemoryStream? _currentEntryData;
        private GoHeader? _currentHeader;
        private bool _closed;

        public GoWriter() { }

        internal GoWriter(IGoWriter? writer)
        {
            if (writer != null)
            {
                _stream = new GoWriterStream(writer);
                _writer = new TarWriter(_stream, leaveOpen: false);
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? WriteHeader([GoParam("*tar.Header")] GoHeader? hdr)
        {
            if (_writer == null)
            {
                return "archive/tar: writer not initialized";
            }
            if (_closed)
            {
                return Package.ErrWriteAfterClose;
            }

            // Flush previous entry if any
            FlushCurrentEntry();

            _currentHeader = hdr;
            _currentEntryData = new MemoryStream();
            return null;
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> b)
        {
            if (_currentEntryData == null)
            {
                return (0, "archive/tar: write without header");
            }

            var buf = new byte[b.Len];
            for (int i = 0; i < b.Len; i++)
            {
                buf[i] = b[i];
            }
            _currentEntryData.Write(buf, 0, buf.Length);
            return (b.Len, null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close()
        {
            if (_closed)
            {
                return null;
            }
            _closed = true;

            try
            {
                FlushCurrentEntry();
                _writer?.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Flush()
        {
            try
            {
                FlushCurrentEntry();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private void FlushCurrentEntry()
        {
            if (_currentHeader == null || _writer == null)
            {
                return;
            }

            try
            {
                var entryType = _currentHeader.Typeflag switch
                {
                    (byte)'5' => TarEntryType.Directory,
                    (byte)'2' => TarEntryType.SymbolicLink,
                    (byte)'1' => TarEntryType.HardLink,
                    _ => TarEntryType.RegularFile,
                };

                TarEntry entry;
                if (entryType == TarEntryType.Directory)
                {
                    entry = new PaxTarEntry(entryType, _currentHeader.Name);
                }
                else
                {
                    entry = new PaxTarEntry(entryType, _currentHeader.Name);
                    if (_currentEntryData != null && _currentEntryData.Length > 0)
                    {
                        _currentEntryData.Position = 0;
                        entry.DataStream = _currentEntryData;
                    }
                }

                if (_currentHeader.Mode > 0)
                {
                    entry.Mode = (UnixFileMode)_currentHeader.Mode;
                }

                _writer.WriteEntry(entry);
            }
            catch
            {
                // Best effort
            }

            _currentHeader = null;
            _currentEntryData = null;
        }
    }

    /// <summary>
    /// Adapts IGoWriter to System.IO.Stream for TarWriter consumption.
    /// </summary>
    internal class GoWriterStream : Stream
    {
        private readonly IGoWriter _writer;

        public GoWriterStream(IGoWriter writer)
        {
            _writer = writer;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var slice = new Slice<byte>(buffer, offset, count);
            _writer.Write(slice);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

using System;
using System.Formats.Tar;
using System.IO;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Archive.Tar
{
    [GoType("struct", Name = "Reader", Package = "archive/tar")]
    public class GoReader
    {
        private readonly TarReader? _reader;
        private TarEntry? _currentEntry;
        private Stream? _currentStream;

        public GoReader() { }

        internal GoReader(IGoReader? reader)
        {
            if (reader != null)
            {
                var stream = new GoReaderStream(reader);
                _reader = new TarReader(stream, leaveOpen: false);
            }
        }

        [GoMethod]
        [return: GoReturn("*tar.Header", "error")]
        public (GoHeader?, object?) Next()
        {
            if (_reader == null)
            {
                return (null, "EOF");
            }

            try
            {
                _currentEntry = _reader.GetNextEntry();
                if (_currentEntry == null)
                {
                    return (null, "EOF");
                }

                var header = new GoHeader
                {
                    Name = _currentEntry.Name ?? "",
                    Size = _currentEntry.Length,
                    Mode = (long)_currentEntry.Mode,
                    Linkname = _currentEntry.LinkName ?? "",
                };

                // Map entry type to Go type flag
                header.Typeflag = _currentEntry.EntryType switch
                {
                    TarEntryType.RegularFile => Package.TypeReg,
                    TarEntryType.Directory => Package.TypeDir,
                    TarEntryType.SymbolicLink => Package.TypeSymlink,
                    TarEntryType.HardLink => Package.TypeLink,
                    TarEntryType.CharacterDevice => Package.TypeChar,
                    TarEntryType.BlockDevice => Package.TypeBlock,
                    TarEntryType.Fifo => Package.TypeFifo,
                    _ => Package.TypeReg,
                };

                // Get the data stream for reading
                _currentStream = _currentEntry.DataStream;

                return (header, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read(Slice<byte> b)
        {
            if (_currentStream == null)
            {
                return (0, "EOF");
            }

            try
            {
                var buf = new byte[b.Len];
                int n = _currentStream.Read(buf, 0, buf.Length);
                for (int i = 0; i < n; i++)
                {
                    b[i] = buf[i];
                }
                if (n == 0)
                {
                    return (0, "EOF");
                }
                return (n, null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }
    }

    /// <summary>
    /// Adapts IGoReader to System.IO.Stream for TarReader consumption.
    /// </summary>
    internal class GoReaderStream : Stream
    {
        private readonly IGoReader _reader;

        public GoReaderStream(IGoReader reader)
        {
            _reader = reader;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var slice = new Slice<byte>(new byte[count]);
            var (n, err) = _reader.Read(slice);
            for (int i = 0; i < n; i++)
            {
                buffer[offset + i] = slice[i];
            }
            return n;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

using System;
using System.IO;
using System.IO.Compression;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Compress.Flate
{
    [GoPackage("compress/flate")]
    public static class Package
    {
        [GoConst(Type = "int")]
        public const long NoCompression = 0;

        [GoConst(Type = "int")]
        public const long BestSpeed = 1;

        [GoConst(Type = "int")]
        public const long BestCompression = 9;

        [GoConst(Type = "int")]
        public const long DefaultCompression = -1;

        [GoConst(Type = "int")]
        public const long HuffmanOnly = -2;

        // flate.NewReader(r io.Reader) io.ReadCloser
        [GoFunc]
        [return: GoReturn("io.ReadCloser")]
        public static GoFlateReader NewReader(object? r) => new GoFlateReader(r);

        // flate.NewWriter(w io.Writer, level int) (*Writer, error)
        [GoFunc]
        [return: GoReturn("*flate.Writer", "error")]
        public static (GoWriter, object?) NewWriter(object? w, [GoParam("int")] long level)
        {
            return (new GoWriter(w, level), null);
        }

        // flate.NewReaderDict(r io.Reader, dict []byte) io.ReadCloser
        [GoFunc]
        [return: GoReturn("io.ReadCloser")]
        public static GoFlateReader NewReaderDict(object? r, Slice<byte> dict) => new GoFlateReader(r);

        // flate.NewWriterDict(w io.Writer, level int, dict []byte) (*Writer, error)
        [GoFunc]
        [return: GoReturn("*flate.Writer", "error")]
        public static (GoWriter, object?) NewWriterDict(object? w, [GoParam("int")] long level, Slice<byte> dict)
        {
            return (new GoWriter(w, level), null);
        }

        // flate.Reader interface
        [GoType("interface", Name = "Reader", Package = "compress/flate")]
        public interface IReader
        {
            [GoMethod]
            [return: GoReturn("int", "error")]
            (long, object?) Read(Slice<byte> p);

            [GoMethod]
            [return: GoReturn("byte", "error")]
            (byte, object?) ReadByte();
        }

        // flate.Resetter interface
        [GoType("interface", Name = "Resetter", Package = "compress/flate")]
        public interface IResetter
        {
            [GoMethod]
            [return: GoReturn("error")]
            object? Reset(object? r, Slice<byte> dict);
        }

        internal static CompressionLevel MapLevel(long level)
        {
            if (level == NoCompression)
            {
                return CompressionLevel.NoCompression;
            }
            if (level == BestSpeed || level == HuffmanOnly)
            {
                return CompressionLevel.Fastest;
            }
            if (level == BestCompression)
            {
                return CompressionLevel.SmallestSize;
            }
            return CompressionLevel.Optimal;
        }
    }

    [GoType("struct", Name = "Reader", Package = "compress/flate")]
    public class GoFlateReader : IGoReadCloser
    {
        private readonly DeflateStream _stream;
        private readonly GoReaderStream _input;

        public GoFlateReader(object? r)
        {
            _input = new GoReaderStream(r as IGoReader);
            _stream = new DeflateStream(_input, CompressionMode.Decompress, leaveOpen: true);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Read(Slice<byte> p)
        {
            try
            {
                var buf = new byte[p.Len];
                int n = _stream.Read(buf, 0, buf.Length);
                for (int i = 0; i < n; i++)
                {
                    p[i] = buf[i];
                }
                if (n == 0)
                {
                    return (0, "EOF");
                }
                return (n, "");
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string Close()
        {
            _stream.Dispose();
            return "";
        }
    }

    [GoType("struct", Name = "Writer", Package = "compress/flate")]
    public class GoWriter
    {
        private readonly DeflateStream? _stream;
        private readonly GoWriterStream _output;

        public GoWriter() : this(null, -1) { }

        public GoWriter(object? w, long level)
        {
            _output = new GoWriterStream(w as IGoWriter);
            _stream = new DeflateStream(_output, Package.MapLevel(level), leaveOpen: true);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> p)
        {
            if (_stream == null)
            {
                return (0, "flate: writer not initialized");
            }
            try
            {
                var buf = new byte[p.Len];
                for (int i = 0; i < p.Len; i++)
                {
                    buf[i] = p[i];
                }
                _stream.Write(buf, 0, buf.Length);
                return (p.Len, null);
            }
            catch (Exception ex)
            {
                return (0, ex.Message);
            }
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Close()
        {
            try
            {
                _stream?.Dispose();
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
                _stream?.Flush();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        public void Reset(object? w) { }
    }

    // Adapter: wraps IGoReader as a System.IO.Stream for .NET compression APIs
    internal class GoReaderStream : Stream
    {
        private readonly IGoReader? _reader;

        public GoReaderStream(IGoReader? reader)
        {
            _reader = reader;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_reader == null)
            {
                return 0;
            }
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

    // flate.WriteError struct
    [GoType("struct", Name = "WriteError", Package = "compress/flate")]
    public class GoWriteError
    {
        [GoField(Name = "Err", Type = "error")] public object? Err;

        [GoMethod]
        [return: GoReturn("string")]
        public string Error() => "flate: write error: " + (Err?.ToString() ?? "");
    }

    // flate.ReadError struct
    [GoType("struct", Name = "ReadError", Package = "compress/flate")]
    public class GoReadError
    {
        [GoField(Name = "Err", Type = "error")] public object? Err;

        [GoMethod]
        [return: GoReturn("string")]
        public string Error() => "flate: read error: " + (Err?.ToString() ?? "");
    }

    // flate.CorruptInputError named type (int64)
    [GoType("named", Name = "CorruptInputError", Package = "compress/flate", Underlying = "int64")]
    public class GoCorruptInputError
    {
        public long Value;

        [GoMethod]
        [return: GoReturn("string")]
        public string Error() => $"flate: corrupt input before offset {Value}";
    }

    // Adapter: wraps IGoWriter as a System.IO.Stream for .NET compression APIs
    internal class GoWriterStream : Stream
    {
        private readonly IGoWriter? _writer;

        public GoWriterStream(IGoWriter? writer)
        {
            _writer = writer;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_writer == null)
            {
                return;
            }
            var slice = new Slice<byte>(new byte[count]);
            for (int i = 0; i < count; i++)
            {
                slice[i] = buffer[offset + i];
            }
            _writer.Write(slice);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}

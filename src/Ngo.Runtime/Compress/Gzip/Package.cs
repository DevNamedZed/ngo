using System;
using System.IO;
using System.IO.Compression;
using Ngo.Runtime.Compress.Flate;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Compress.Gzip
{
    [GoPackage("compress/gzip")]
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

        // gzip.NewWriter(w io.Writer) *Writer
        [GoFunc]
        [return: GoReturn("*gzip.Writer")]
        public static GoWriter NewWriter(object? w) => new GoWriter(w, DefaultCompression);

        // gzip.NewWriterLevel(w io.Writer, level int) (*Writer, error)
        [GoFunc]
        [return: GoReturn("*gzip.Writer", "error")]
        public static (GoWriter, object?) NewWriterLevel(object? w, [GoParam("int")] long level)
        {
            return (new GoWriter(w, level), null);
        }

        // gzip.NewReader(r io.Reader) (*Reader, error)
        [GoFunc]
        [return: GoReturn("*gzip.Reader", "error")]
        public static (GoReader?, object?) NewReader(object? r)
        {
            try
            {
                return (new GoReader(r), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
    }

    [GoType("struct", Name = "Header", Package = "compress/gzip")]
    public class GoHeader
    {
        [GoField(Name = "Comment")]
        public string Comment = "";

        [GoField(Name = "Extra", Type = "[]byte")]
        public Slice<byte> Extra = new Slice<byte>();

        [GoField(Name = "Name")]
        public string Name = "";

        [GoField(Name = "OS")]
        public byte OS = 255;
    }

    [GoType("struct", Name = "Writer", Package = "compress/gzip")]
    public class GoWriter
    {
        private readonly GZipStream? _stream;
        private readonly GoWriterStream _output;

        [GoField(Name = "Comment")]
        public string Comment = "";

        [GoField(Name = "Extra", Type = "[]byte")]
        public Slice<byte> Extra = new Slice<byte>();

        [GoField(Name = "Name")]
        public string Name = "";

        [GoField(Name = "OS")]
        public byte OS = 255;

        public GoWriter() : this(null, -1) { }

        public GoWriter(object? w, long level)
        {
            _output = new GoWriterStream(w as IGoWriter);
            _stream = new GZipStream(_output, Flate.Package.MapLevel(level), leaveOpen: true);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> p)
        {
            if (_stream == null)
            {
                return (0, "gzip: writer not initialized");
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

    [GoType("struct", Name = "Reader", Package = "compress/gzip")]
    public class GoReader
    {
        private GZipStream? _stream;
        private GoReaderStream? _input;

        [GoField(Name = "Header", Embedded = true)]
        public GoHeader Header = new GoHeader();

        public GoReader() { }

        public GoReader(object? r)
        {
            _input = new GoReaderStream(r as IGoReader);
            _stream = new GZipStream(_input, CompressionMode.Decompress, leaveOpen: true);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Read(Slice<byte> p)
        {
            if (_stream == null)
            {
                return (0, "gzip: reader not initialized");
            }
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
                return (n, null);
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
        public object? Reset(object? r)
        {
            try
            {
                _stream?.Dispose();
                _input = new GoReaderStream(r as IGoReader);
                _stream = new GZipStream(_input, CompressionMode.Decompress, leaveOpen: true);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        [GoMethod]
        public bool Multistream(bool ok) => false;
    }
}

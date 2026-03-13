using System;
using System.IO;
using System.IO.Compression;
using Ngo.Runtime.Compress.Flate;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Compress.Zlib
{
    [GoPackage("compress/zlib")]
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

        // zlib.NewReader(r io.Reader) (io.ReadCloser, error)
        [GoFunc]
        [return: GoReturn("io.ReadCloser", "error")]
        public static (GoZlibReader?, object?) NewReader(object? r)
        {
            try
            {
                return (new GoZlibReader(r), null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // zlib.NewReaderDict(r io.Reader, dict []byte) (io.ReadCloser, error)
        [GoFunc]
        [return: GoReturn("io.ReadCloser", "error")]
        public static (GoZlibReader?, object?) NewReaderDict(object? r, Slice<byte> dict)
        {
            return NewReader(r);
        }

        // zlib.NewWriter(w io.Writer) *Writer
        [GoFunc]
        [return: GoReturn("*zlib.Writer")]
        public static GoWriter NewWriter(object? w) => new GoWriter(w, DefaultCompression);

        // zlib.NewWriterLevel(w io.Writer, level int) (*Writer, error)
        [GoFunc]
        [return: GoReturn("*zlib.Writer", "error")]
        public static (GoWriter, object?) NewWriterLevel(object? w, [GoParam("int")] long level)
        {
            return (new GoWriter(w, level), null);
        }

        // zlib.NewWriterLevelDict(w io.Writer, level int, dict []byte) (*Writer, error)
        [GoFunc]
        [return: GoReturn("*zlib.Writer", "error")]
        public static (GoWriter, object?) NewWriterLevelDict(object? w, [GoParam("int")] long level, Slice<byte> dict)
        {
            return (new GoWriter(w, level), null);
        }
    }

    [GoType("struct", Name = "Reader", Package = "compress/zlib")]
    public class GoZlibReader : IGoReadCloser
    {
        private readonly ZLibStream? _stream;
        private readonly GoReaderStream _input;

        public GoZlibReader(object? r)
        {
            _input = new GoReaderStream(r as IGoReader);
            _stream = new ZLibStream(_input, CompressionMode.Decompress, leaveOpen: true);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (int, string) Read(Slice<byte> p)
        {
            if (_stream == null)
            {
                return (0, "zlib: reader not initialized");
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
            _stream?.Dispose();
            return "";
        }
    }

    [GoType("struct", Name = "Writer", Package = "compress/zlib")]
    public class GoWriter
    {
        private readonly ZLibStream? _stream;
        private readonly GoWriterStream _output;

        public GoWriter() : this(null, -1) { }

        public GoWriter(object? w, long level)
        {
            _output = new GoWriterStream(w as IGoWriter);
            _stream = new ZLibStream(_output, Flate.Package.MapLevel(level), leaveOpen: true);
        }

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> p)
        {
            if (_stream == null)
            {
                return (0, "zlib: writer not initialized");
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
}

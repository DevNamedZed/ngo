using System;
using System.Collections.Generic;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Bufio
{
    [GoPackage("bufio")]
    public static class Package
    {
        [GoFunc]
        public static Scanner NewScanner([GoParam("interface{}")] IGoReader r) => new Scanner(r);

        [GoFunc]
        public static Reader NewReader([GoParam("interface{}")] IGoReader r) => new Reader(r);

        [GoFunc]
        public static Reader NewReaderSize([GoParam("interface{}")] IGoReader rd, [GoParam("int")] long size) => new Reader(rd, (int)size);

        [GoFunc]
        public static Writer NewWriter([GoParam("interface{}")] IGoWriter w) => new Writer(w);

        [GoFunc]
        public static Writer NewWriterSize([GoParam("interface{}")] IGoWriter w, [GoParam("int")] long size) => new Writer(w, (int)size);

        [GoFunc]
        public static ReadWriter NewReadWriter(Reader r, Writer w) => new ReadWriter(r, w);

        [GoVar]
        public static readonly Func<Slice<byte>, bool, (long, Slice<byte>, object?)> ScanLines = ScanLinesImpl;

        [GoVar]
        public static readonly Func<Slice<byte>, bool, (long, Slice<byte>, object?)> ScanWords = ScanWordsImpl;

        [GoVar]
        public static readonly Func<Slice<byte>, bool, (long, Slice<byte>, object?)> ScanBytes = ScanBytesImpl;

        [GoVar]
        public static readonly Func<Slice<byte>, bool, (long, Slice<byte>, object?)> ScanRunes = ScanRunesImpl;

        [GoConst]
        public const long MaxScanTokenSize = 64 * 1024;

        [GoVar(Type = "error")]
        public static readonly object? ErrBufferFull = Ngo.Runtime.Errors.Package.New("bufio: buffer full");

        [GoVar(Type = "error")]
        public static readonly object? ErrFinalToken = Ngo.Runtime.Errors.Package.New("bufio: final token");

        [GoVar(Type = "error")]
        public static readonly object? ErrTooLong = Ngo.Runtime.Errors.Package.New("bufio.Scanner: token too long");

        [GoVar(Type = "error")]
        public static readonly object? ErrNegativeAdvance = Ngo.Runtime.Errors.Package.New("bufio.Scanner: SplitFunc returns negative advance count");

        [GoVar(Type = "error")]
        public static readonly object? ErrAdvanceTooFar = Ngo.Runtime.Errors.Package.New("bufio.Scanner: SplitFunc returns advance count beyond input");

        [GoVar(Type = "error")]
        public static readonly object? ErrBadReadCount = Ngo.Runtime.Errors.Package.New("bufio.Scanner: Read returned impossible count");

        private static (long, Slice<byte>, object?) ScanLinesImpl(Slice<byte> data, bool atEOF)
        {
            if (data.Len == 0 && atEOF)
                return (0, default(Slice<byte>), null);

            for (int i = 0; i < data.Len; i++)
            {
                if (data[i] == (byte)'\n')
                {
                    int end = i;
                    if (end > 0 && data[end - 1] == (byte)'\r')
                        end--;
                    return (i + 1, data.Reslice(0, end), null);
                }
            }

            if (atEOF)
            {
                int end = data.Len;
                if (end > 0 && data[end - 1] == (byte)'\r')
                    end--;
                return (data.Len, data.Reslice(0, end), null);
            }

            return (0, default(Slice<byte>), null);
        }

        private static (long, Slice<byte>, object?) ScanWordsImpl(Slice<byte> data, bool atEOF)
        {
            int start = 0;
            while (start < data.Len && (data[start] == (byte)' ' || data[start] == (byte)'\t' ||
                   data[start] == (byte)'\n' || data[start] == (byte)'\r'))
                start++;

            if (start >= data.Len)
            {
                if (atEOF)
                    return (data.Len, default(Slice<byte>), null);
                return (0, default(Slice<byte>), null);
            }

            int end = start;
            while (end < data.Len && data[end] != (byte)' ' && data[end] != (byte)'\t' &&
                   data[end] != (byte)'\n' && data[end] != (byte)'\r')
                end++;

            if (end < data.Len || atEOF)
                return (end, data.Reslice(start, end), null);

            return (0, default(Slice<byte>), null);
        }

        private static (long, Slice<byte>, object?) ScanBytesImpl(Slice<byte> data, bool atEOF)
        {
            if (data.Len > 0)
                return (1, data.Reslice(0, 1), null);
            return (0, default(Slice<byte>), null);
        }

        internal static byte[] SliceToArray(Slice<byte> s)
        {
            var arr = new byte[s.Len];
            for (int i = 0; i < s.Len; i++)
                arr[i] = s[i];
            return arr;
        }

        private static (long, Slice<byte>, object?) ScanRunesImpl(Slice<byte> data, bool atEOF)
        {
            if (data.Len == 0)
                return (0, default(Slice<byte>), null);

            if (data[0] < 0x80)
                return (1, data.Reslice(0, 1), null);

            int size = 1;
            byte b = data[0];
            if ((b & 0xE0) == 0xC0) size = 2;
            else if ((b & 0xF0) == 0xE0) size = 3;
            else if ((b & 0xF8) == 0xF0) size = 4;

            if (size <= data.Len)
                return (size, data.Reslice(0, size), null);

            if (atEOF)
                return (1, data.Reslice(0, 1), null);

            return (0, default(Slice<byte>), null);
        }
    }
}

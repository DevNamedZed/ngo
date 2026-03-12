using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Csv
{
    [GoType("struct", Name = "Reader", Package = "encoding/csv")]
    public class Reader
    {
        private readonly TextReader _reader;
        public long Comma = ',';

        public Reader(IGoReader reader)
        {
            _reader = new StreamReader(new ReaderStream(reader));
        }

        public (Slice<string>, object?) Read()
        {
            var line = _reader.ReadLine();
            if (line == null)
                return (new Slice<string>(Array.Empty<string>()), "EOF");

            var fields = ParseLine(line, (char)Comma);
            return (new Slice<string>(fields), null);
        }

        public (Slice<Slice<string>>, object?) ReadAll()
        {
            var records = new List<Slice<string>>();
            while (true)
            {
                var (record, err) = Read();
                if (err != null)
                    break;
                records.Add(record);
            }
            return (new Slice<Slice<string>>(records.ToArray()), null);
        }

        private static string[] ParseLine(string line, char comma)
        {
            var fields = new List<string>();
            int i = 0;
            while (i <= line.Length)
            {
                if (i == line.Length)
                {
                    if (fields.Count > 0 || line.Length == 0)
                        break;
                }

                if (i < line.Length && line[i] == '"')
                {
                    // Quoted field
                    var sb = new StringBuilder();
                    i++; // skip opening quote
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"')
                            {
                                sb.Append('"');
                                i += 2;
                            }
                            else
                            {
                                i++; // skip closing quote
                                break;
                            }
                        }
                        else
                        {
                            sb.Append(line[i]);
                            i++;
                        }
                    }
                    fields.Add(sb.ToString());
                    if (i < line.Length && line[i] == comma)
                        i++; // skip comma
                }
                else
                {
                    // Unquoted field
                    int start = i;
                    while (i < line.Length && line[i] != comma)
                        i++;
                    fields.Add(line[start..i]);
                    if (i < line.Length)
                        i++; // skip comma
                    else
                        break;
                }
            }
            return fields.ToArray();
        }

        private class ReaderStream : Stream
        {
            private readonly IGoReader _reader;
            public ReaderStream(IGoReader r) => _reader = r;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count)
            {
                var slice = new Slice<byte>(buffer, offset, count);
                var (n, _) = _reader.Read(slice);
                return (int)n;
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}

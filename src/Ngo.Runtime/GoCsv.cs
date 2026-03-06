// -----------------------------------------------------------------------
// <copyright file="GoCsv.cs" company="Ziad">
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
using System.IO;
using System.Text;

namespace Ngo.Runtime
{
    public static class GoCsv
    {
        public static GoCsvReader NewReader(object r)
        {
            if (r is IGoReader reader)
                return new GoCsvReader(reader);
            throw new InvalidOperationException("csv.NewReader requires an io.Reader");
        }

        public static GoCsvWriter NewWriter(object w)
        {
            if (w is IGoWriter writer)
                return new GoCsvWriter(writer);
            throw new InvalidOperationException("csv.NewWriter requires an io.Writer");
        }
    }

    public class GoCsvReader
    {
        private readonly TextReader _reader;
        public long Comma = ',';

        public GoCsvReader(IGoReader reader)
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

    public class GoCsvWriter
    {
        private readonly IGoWriter _writer;
        public long Comma = ',';

        public GoCsvWriter(IGoWriter writer)
        {
            _writer = writer;
        }

        public void Write(Slice<string> record)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < record.Len; i++)
            {
                if (i > 0) sb.Append((char)Comma);
                var field = record[i];
                if (NeedsQuoting(field, (char)Comma))
                {
                    sb.Append('"');
                    sb.Append(field.Replace("\"", "\"\""));
                    sb.Append('"');
                }
                else
                {
                    sb.Append(field);
                }
            }
            sb.Append("\r\n");
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            _writer.Write(new Slice<byte>(bytes));
        }

        public void WriteAll(Slice<Slice<string>> records)
        {
            for (int i = 0; i < records.Len; i++)
            {
                Write(records[i]);
            }
            Flush();
        }

        public void Flush()
        {
            // No buffering in our implementation
        }

        private static bool NeedsQuoting(string field, char comma)
        {
            return field.Contains(comma) || field.Contains('"') || field.Contains('\n') || field.Contains('\r');
        }
    }
}

using System.Text;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Csv
{
    public class Writer
    {
        private readonly IGoWriter _writer;
        public long Comma = ',';

        public Writer(IGoWriter writer)
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
            var bytes = global::System.Text.Encoding.UTF8.GetBytes(sb.ToString());
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

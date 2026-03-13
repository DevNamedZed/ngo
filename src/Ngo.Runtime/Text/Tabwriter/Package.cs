using System;
using System.Collections.Generic;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Text.Tabwriter
{
    [GoPackage("text/tabwriter")]
    public static class Package
    {
        // tabwriter.NewWriter(output io.Writer, minwidth, tabwidth, padding int, padchar byte, flags uint) *Writer
        [GoFunc]
        [return: GoReturn("*tabwriter.Writer")]
        public static GoWriter NewWriter(object? output, [GoParam("int")] long minwidth,
            [GoParam("int")] long tabwidth, [GoParam("int")] long padding, byte padchar,
            [GoParam("uint")] ulong flags)
        {
            var w = new GoWriter();
            w.Init(output, minwidth, tabwidth, padding, padchar, flags);
            return w;
        }

        // Constants
        [GoConst(Type = "uint")]
        public const long FilterHTML = 1;

        [GoConst(Type = "uint")]
        public const long StripEscape = 2;

        [GoConst(Type = "uint")]
        public const long AlignRight = 4;

        [GoConst(Type = "uint")]
        public const long DiscardEmptyColumns = 8;

        [GoConst(Type = "uint")]
        public const long TabIndent = 16;

        [GoConst(Type = "uint")]
        public const long Debug = 32;

        [GoConst(Type = "byte")]
        public const long Escape = 0xFF;
    }

    [GoType("struct", Name = "Writer", Package = "text/tabwriter")]
    public class GoWriter : IGoWriter
    {
        private IGoWriter? _output;
        private int _minwidth;
        private int _tabwidth;
        private int _padding;
        private byte _padchar;
        private ulong _flags;
        private readonly List<List<string>> _lines = new List<List<string>>();
        private List<string> _currentLine = new List<string>();
        private StringBuilder _currentCell = new StringBuilder();

        [GoMethod]
        [return: GoReturn("int", "error")]
        public (long, object?) Write(Slice<byte> buf)
        {
            int totalWritten = 0;
            for (int i = 0; i < buf.Len; i++)
            {
                byte b = buf[i];
                if (b == (byte)'\t')
                {
                    _currentLine.Add(_currentCell.ToString());
                    _currentCell.Clear();
                }
                else if (b == (byte)'\n')
                {
                    _currentLine.Add(_currentCell.ToString());
                    _currentCell.Clear();
                    _lines.Add(_currentLine);
                    _currentLine = new List<string>();
                }
                else
                {
                    _currentCell.Append((char)b);
                }
                totalWritten++;
            }
            return (totalWritten, null);
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Flush()
        {
            if (_output == null)
            {
                return null;
            }

            // Flush any remaining content
            if (_currentCell.Length > 0 || _currentLine.Count > 0)
            {
                _currentLine.Add(_currentCell.ToString());
                _currentCell.Clear();
                _lines.Add(_currentLine);
                _currentLine = new List<string>();
            }

            if (_lines.Count == 0)
            {
                return null;
            }

            // Calculate max width for each column
            int maxCols = 0;
            foreach (var line in _lines)
            {
                if (line.Count > maxCols)
                {
                    maxCols = line.Count;
                }
            }

            var colWidths = new int[maxCols];
            foreach (var line in _lines)
            {
                for (int col = 0; col < line.Count; col++)
                {
                    int cellWidth = VisibleWidth(line[col]);
                    if (cellWidth > colWidths[col])
                    {
                        colWidths[col] = cellWidth;
                    }
                }
            }

            // Ensure minimum widths
            for (int col = 0; col < maxCols; col++)
            {
                if (colWidths[col] < _minwidth)
                {
                    colWidths[col] = _minwidth;
                }
            }

            // Write formatted output
            bool alignRight = (_flags & (ulong)Package.AlignRight) != 0;
            char pad = (char)_padchar;

            foreach (var line in _lines)
            {
                var sb = new StringBuilder();
                for (int col = 0; col < line.Count; col++)
                {
                    string cell = line[col];
                    int cellWidth = VisibleWidth(cell);
                    bool isLastCol = (col == line.Count - 1);

                    if (isLastCol)
                    {
                        // Last column: no padding needed
                        sb.Append(cell);
                    }
                    else
                    {
                        int totalWidth = colWidths[col] + _padding;
                        if (alignRight)
                        {
                            // Right-align: pad on left
                            int padCount = totalWidth - cellWidth;
                            if (padCount > 0)
                            {
                                sb.Append(pad, padCount);
                            }
                            sb.Append(cell);
                        }
                        else
                        {
                            // Left-align: pad on right
                            sb.Append(cell);
                            int padCount = totalWidth - cellWidth;
                            if (padCount > 0)
                            {
                                sb.Append(pad, padCount);
                            }
                        }
                    }
                }
                sb.Append('\n');

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                _output.Write(new Slice<byte>(bytes));
            }

            _lines.Clear();
            return null;
        }

        [GoMethod]
        [return: GoReturn("*tabwriter.Writer")]
        public GoWriter Init(object? output, [GoParam("int")] long minwidth,
            [GoParam("int")] long tabwidth, [GoParam("int")] long padding, byte padchar,
            [GoParam("uint")] ulong flags)
        {
            _output = output as IGoWriter;
            _minwidth = (int)minwidth;
            _tabwidth = (int)tabwidth;
            _padding = (int)padding;
            _padchar = padchar == 0 ? (byte)' ' : padchar;
            _flags = flags;
            _lines.Clear();
            _currentLine = new List<string>();
            _currentCell.Clear();
            return this;
        }

        private static int VisibleWidth(string s)
        {
            // Simple visible width — count characters (ignoring escape sequences for now)
            return s.Length;
        }
    }
}

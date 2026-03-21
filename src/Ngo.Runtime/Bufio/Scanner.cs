using System;
using System.Collections.Generic;
using System.Text;
using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Bufio
{
    [GoType("struct", Name = "Scanner", Package = "bufio")]
    public sealed class Scanner
    {
        private readonly IGoReader _reader;
        private byte[] _buf;
        private int _bufLen;
        private int _bufPos;
        private string _token;
        private Slice<byte> _tokenBytes;
        private bool _done;
        private object? _err;
        private Func<Slice<byte>, bool, (long, Slice<byte>, object?)>? _split;
        private int _maxTokenSize;

        public Scanner(IGoReader reader)
        {
            _reader = reader;
            _buf = new byte[4096];
            _bufLen = 0;
            _bufPos = 0;
            _token = "";
            _tokenBytes = default(Slice<byte>);
            _done = false;
            _err = null;
            _split = null;
            _maxTokenSize = (int)Package.MaxScanTokenSize;
        }

        [GoMethod]
        public bool Scan()
        {
            if (_done)
                return false;

            var splitFn = _split ?? Package.ScanLines;

            var line = new List<byte>();
            while (true)
            {
                if (_bufPos >= _bufLen)
                {
                    var slice = new Slice<byte>(_buf);
                    var (n, err) = _reader.Read(slice);
                    if (n == 0)
                    {
                        _done = true;
                        if (line.Count > 0)
                        {
                            var remaining = new Slice<byte>(line.ToArray());
                            var (_, token, splitErr) = splitFn(remaining, true);
                            if (splitErr != null)
                            {
                                _err = splitErr;
                                return false;
                            }
                            if (token.Len > 0)
                            {
                                _tokenBytes = token;
                                _token = global::System.Text.Encoding.UTF8.GetString(Package.SliceToArray(token));
                                return true;
                            }
                        }
                        if (err != null && err is string s && s != "")
                            _err = err;
                        return false;
                    }
                    _bufLen = n;
                    _bufPos = 0;
                }

                byte b = _buf[_bufPos++];
                line.Add(b);

                var data = new Slice<byte>(line.ToArray());
                var (advance, tok, sErr) = splitFn(data, false);
                if (sErr != null)
                {
                    _err = sErr;
                    _done = true;
                    return false;
                }
                if (advance > 0)
                {
                    int consumed = (int)advance;
                    if (consumed < line.Count)
                    {
                        var remainder = line.GetRange(consumed, line.Count - consumed);
                        line.Clear();
                        line.AddRange(remainder);
                    }
                    else
                    {
                        line.Clear();
                    }

                    if (tok.Len > 0 || advance > 0)
                    {
                        _tokenBytes = tok;
                        _token = global::System.Text.Encoding.UTF8.GetString(Package.SliceToArray(tok));
                        return true;
                    }
                }
            }
        }

        [GoMethod]
        public string Text() => _token;

        [GoMethod]
        public Slice<byte> Bytes() => _tokenBytes;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Err() => _err;

        [GoMethod]
        public void Split([GoParam("func([]byte, bool) (int, []byte, error)")] Func<Slice<byte>, bool, (long, Slice<byte>, object?)> split)
        {
            _split = split;
        }

        [GoMethod]
        public void Buffer(Slice<byte> buf, [GoParam("int")] long max)
        {
            _buf = new byte[buf.Len > 0 ? buf.Len : 4096];
            for (int i = 0; i < buf.Len && i < _buf.Length; i++)
                _buf[i] = buf[i];
            _maxTokenSize = (int)max;
        }
    }
}

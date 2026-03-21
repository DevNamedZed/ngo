using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    // slog.Record struct
    [GoType("struct", Name = "Record", Package = "log/slog")]
    public struct GoRecord
    {
        [GoField(Name = "Time")]
        public object? Time;

        [GoField(Name = "Message")]
        public string Message;

        [GoField(Name = "Level")]
        public GoLevel Level;

        [GoField(Name = "PC", Type = "uintptr")]
        public nuint PC;

        private System.Collections.Generic.List<GoAttr>? _attrs;

        [GoMethod]
        public long NumAttrs() => _attrs?.Count ?? 0;

        [GoMethod]
        public void Attrs(Func<GoAttr, bool> fn)
        {
            if (_attrs != null)
            {
                foreach (var a in _attrs)
                {
                    if (!fn(a))
                    {
                        return;
                    }
                }
            }
        }

        [GoMethod]
        public void AddAttrs([GoParam("slog.Attr")] params GoAttr[] attrs)
        {
            _attrs ??= new System.Collections.Generic.List<GoAttr>();
            _attrs.AddRange(attrs);
        }

        [GoMethod]
        public void Add([GoParam("any")] params object[] args)
        {
            _attrs ??= new System.Collections.Generic.List<GoAttr>();
            // Convert key-value pairs to Attrs: Add("key1", value1, "key2", value2, ...)
            int i = 0;
            while (i < args.Length)
            {
                if (args[i] is GoAttr attr)
                {
                    _attrs.Add(attr);
                    i++;
                }
                else if (args[i] is string key && i + 1 < args.Length)
                {
                    _attrs.Add(new GoAttr { Key = key, Value = new GoValue(args[i + 1]) });
                    i += 2;
                }
                else
                {
                    i++;
                }
            }
        }
    }
}

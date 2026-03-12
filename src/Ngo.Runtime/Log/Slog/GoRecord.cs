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
        public void Attrs(Action<GoAttr> fn)
        {
            if (_attrs != null)
            {
                foreach (var a in _attrs)
                    fn(a);
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
            // Stub: in Go this converts key-value pairs to Attrs
            _attrs ??= new System.Collections.Generic.List<GoAttr>();
        }
    }
}

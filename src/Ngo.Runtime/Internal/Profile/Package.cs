using System.Collections.Generic;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Internal.Profile
{
    [GoPackage("internal/profile")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*Profile", "error")]
        public static (GoProfile?, object?) Parse([GoParam("io.Reader")] object? r)
            => (new GoProfile(), null);

        [GoFunc]
        [return: GoReturn("*Profile", "error")]
        public static (GoProfile?, object?) ParseData(Slice<byte> data)
            => (new GoProfile(), null);

        [GoFunc]
        [return: GoReturn("*Profile", "error")]
        public static (GoProfile?, object?) Merge(Slice<GoProfile> srcs)
            => (new GoProfile(), null);
    }

    [GoType("struct", Name = "Profile", Package = "internal/profile")]
    public class GoProfile
    {
        [GoField(Name = "SampleType")] public Slice<GoValueType> SampleType;
        [GoField(Name = "Sample")] public Slice<GoSample> Sample;
        [GoField(Name = "Mapping")] public Slice<GoMapping> Mapping;
        [GoField(Name = "Location")] public Slice<GoLocation> Location;
        [GoField(Name = "Function")] public Slice<GoFunction> Function;
        [GoField(Name = "DropFrames")] public long DropFrames;
        [GoField(Name = "KeepFrames")] public long KeepFrames;
        [GoField(Name = "TimeNanos")] public long TimeNanos;
        [GoField(Name = "DurationNanos")] public long DurationNanos;
        [GoField(Name = "PeriodType")] public GoValueType? PeriodType;
        [GoField(Name = "Period")] public long Period;

        [GoMethod] [return: GoReturn("error")] public object? Write([GoParam("io.Writer")] object? w) => null;
        [GoMethod] public void Prune(string dropRx, string keepRx) { }
        [GoMethod] [return: GoReturn("*Profile", "error")] public (GoProfile?, object?) FilterSamplesByName(object? focus, object? ignore, object? hide, object? show) => (this, null);
        [GoMethod] public bool HasFileLines() => false;
        [GoMethod] public void Scale(long ratio) { }
        [GoMethod] [return: GoReturn("*Profile")] public GoProfile? Copy() => this;
        [GoMethod] [return: GoReturn("error")] public object? CheckValid() => null;
        [GoMethod] public void SetLabel(string key, Slice<string> value) { }
        [GoMethod] [return: GoReturn("*Profile", "error")] public (GoProfile?, object?) Merge(Slice<GoProfile> pb, long r) => (this, null);
        [GoMethod] public void Compact() { }
        [GoMethod] public void Aggregate(bool inlineFrame, bool function, bool filename, bool linenumber, bool address) { }
    }

    [GoType("struct", Name = "ValueType", Package = "internal/profile")]
    public class GoValueType
    {
        [GoField(Name = "Type")] public string Type = "";
        [GoField(Name = "Unit")] public string Unit = "";
    }

    [GoType("struct", Name = "Sample", Package = "internal/profile")]
    public class GoSample
    {
        [GoField(Name = "Location")] public Slice<GoLocation> Location;
        [GoField(Name = "Value")] public Slice<long> Value;
        [GoField(Name = "Label")] public Map<string, Slice<string>> Label;
    }

    [GoType("struct", Name = "Mapping", Package = "internal/profile")]
    public class GoMapping
    {
        [GoField(Name = "ID")] public long ID;
        [GoField(Name = "Start")] public long Start;
        [GoField(Name = "Limit")] public long Limit;
        [GoField(Name = "Offset")] public long Offset;
        [GoField(Name = "File")] public string File = "";
        [GoField(Name = "BuildID")] public string BuildID = "";
    }

    [GoType("struct", Name = "Location", Package = "internal/profile")]
    public class GoLocation
    {
        [GoField(Name = "ID")] public long ID;
        [GoField(Name = "Mapping")] public GoMapping? Mapping;
        [GoField(Name = "Address")] public long Address;
        [GoField(Name = "Line")] public Slice<GoLine> Line;
    }

    [GoType("struct", Name = "Line", Package = "internal/profile")]
    public class GoLine
    {
        [GoField(Name = "Function")] public GoFunction? Function;
        [GoField(Name = "Line")] public long LineNum;
    }

    [GoType("struct", Name = "Function", Package = "internal/profile")]
    public class GoFunction
    {
        [GoField(Name = "ID")] public long ID;
        [GoField(Name = "Name")] public string Name = "";
        [GoField(Name = "SystemName")] public string SystemName = "";
        [GoField(Name = "Filename")] public string Filename = "";
        [GoField(Name = "StartLine")] public long StartLine;
    }
}

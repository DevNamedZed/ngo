using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Archive.Tar
{
    // tar.Header struct
    [GoType("struct", Name = "Header", Package = "archive/tar")]
    public class GoHeader
    {
        [GoField(Name = "Typeflag")] public byte Typeflag;
        [GoField(Name = "Name")] public string Name = "";
        [GoField(Name = "Linkname")] public string Linkname = "";
        [GoField(Name = "Size")] public long Size;
        [GoField(Name = "Mode")] public long Mode;
        [GoField(Name = "Uid")] public long Uid;
        [GoField(Name = "Gid")] public long Gid;
        [GoField(Name = "Uname")] public string Uname = "";
        [GoField(Name = "Gname")] public string Gname = "";
        [GoField(Name = "ModTime")] public object? ModTime; // time.Time
        [GoField(Name = "AccessTime")] public object? AccessTime; // time.Time
        [GoField(Name = "ChangeTime")] public object? ChangeTime; // time.Time
        [GoField(Name = "Devmajor")] public long Devmajor;
        [GoField(Name = "Devminor")] public long Devminor;
        [GoField(Name = "Xattrs")] public Map<string, string> Xattrs;
        [GoField(Name = "PAXRecords")] public Map<string, string> PAXRecords;
        [GoField(Name = "Format")] public long Format; // tar.Format

        [GoMethod]
        [return: GoReturn("fs.FileInfo")]
        public object? FileInfo() => null;
    }
}

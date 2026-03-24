using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Database.Sql
{
    [GoType("struct", Name = "NullInt16", Package = "database/sql")]
    public struct GoNullInt16
    {
        [GoField(Name = "Int16")]
        public short Int16;

        [GoField(Name = "Valid")]
        public bool Valid;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(object? value)
        {
            if (value == null)
            {
                Int16 = 0;
                Valid = false;
                return null;
            }
            Int16 = Convert.ToInt16(value);
            Valid = true;
            return null;
        }
    }

    [GoType("struct", Name = "NullInt32", Package = "database/sql")]
    public struct GoNullInt32
    {
        [GoField(Name = "Int32")]
        public int Int32;

        [GoField(Name = "Valid")]
        public bool Valid;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(object? value)
        {
            if (value == null)
            {
                Int32 = 0;
                Valid = false;
                return null;
            }
            Int32 = Convert.ToInt32(value);
            Valid = true;
            return null;
        }
    }

    [GoType("struct", Name = "NullByte", Package = "database/sql")]
    public struct GoNullByte
    {
        [GoField(Name = "Byte")]
        public byte Byte;

        [GoField(Name = "Valid")]
        public bool Valid;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(object? value)
        {
            if (value == null)
            {
                Byte = 0;
                Valid = false;
                return null;
            }
            Byte = Convert.ToByte(value);
            Valid = true;
            return null;
        }
    }

    [GoType("struct", Name = "NullTime", Package = "database/sql")]
    public struct GoNullTime
    {
        [GoField(Name = "Time", Type = "time.Time")]
        public object? Time;

        [GoField(Name = "Valid")]
        public bool Valid;

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan(object? value)
        {
            if (value == null)
            {
                Time = null;
                Valid = false;
                return null;
            }
            Time = value;
            Valid = true;
            return null;
        }
    }
}

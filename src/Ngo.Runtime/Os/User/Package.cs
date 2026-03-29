using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Os.User
{
    /// <summary>
    /// Runtime support for Go's os/user package.
    /// </summary>
    [GoPackage("os/user")]
    public static class Package
    {
        [GoFunc]
        [return: GoReturn("*User", "error")]
        public static (GoUser, string) Current()
        {
            var user = new GoUser
            {
                Uid = "1000",
                Gid = "1000",
                Username = Environment.UserName,
                Name = Environment.UserName,
                HomeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            };
            return (user, null!);
        }

        [GoFunc]
        [return: GoReturn("*User", "error")]
        public static (GoUser, string) Lookup(string username)
        {
            if (username == Environment.UserName)
                return Current();
            return (null!, $"user: unknown user {username}");
        }

        [GoFunc]
        [return: GoReturn("*User", "error")]
        public static (GoUser, string) LookupId(string uid)
        {
            // Stub
            return (null!, $"user: unknown userid {uid}");
        }

        [GoFunc]
        [return: GoReturn("*Group", "error")]
        public static (GoGroup, object?) LookupGroup(string name)
        {
            return (null!, (object?)$"group: unknown group {name}");
        }

        [GoFunc]
        [return: GoReturn("*Group", "error")]
        public static (GoGroup, object?) LookupGroupId(string gid)
        {
            return (null!, (object?)$"group: unknown groupid {gid}");
        }
    }

    [GoType("struct", Name = "Group", Package = "os/user")]
    public class GoGroup
    {
        [GoField] public string Gid;
        [GoField] public string Name;
    }

    [GoType("struct", Name = "User", Package = "os/user")]
    public class GoUser
    {
        [GoField]
        public string Uid;
        [GoField]
        public string Gid;
        [GoField]
        public string Username;
        [GoField]
        public string Name;
        [GoField]
        public string HomeDir;
    }
}

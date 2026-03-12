using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.ImagePalette
{
    [GoPackage("image/color/palette")]
    public static class Package
    {
        // Plan9 is a 256-color palette from the Plan 9 color map
        [GoVar]
        public static readonly Slice<object> Plan9 = new Slice<object>(new object[256]);

        // WebSafe is a 216-color palette for web
        [GoVar]
        public static readonly Slice<object> WebSafe = new Slice<object>(new object[216]);
    }
}

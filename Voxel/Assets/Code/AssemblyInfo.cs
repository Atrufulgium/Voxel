using System.Runtime.CompilerServices;
using System.ComponentModel;

[assembly: InternalsVisibleTo("VoxelTests")]

// This is needed to support records.
// There isn't really a better place to put it than this I guess.
namespace System.Runtime.CompilerServices {
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}
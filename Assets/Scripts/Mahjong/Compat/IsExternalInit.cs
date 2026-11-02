// Unity ships .NET Standard 2.1 / .NET Framework profiles where this type is absent,
// so `record` and `init` accessors fail to compile (CS0518) even though the C# 9 compiler
// supports them. The runtime only needs the marker type to exist; nothing else.
// Kept internal so it cannot clash with another assembly defining the same polyfill.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}

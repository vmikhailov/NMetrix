using System;

namespace NMetrics
{
    [Flags]
    public enum UsageKind
    {
        Unknown = 0,
        TypeReference = 1,
        Construction = 2,
        MutableAccess = 4,
        ImmutableAccess = 8,
        Explicit = 1024,
        Implicit = 2048
    }
}
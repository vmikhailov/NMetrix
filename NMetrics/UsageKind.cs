using System;

namespace NMetrics
{
    [Flags]
    public enum UsageKind
    {
        Unknown = 0,
        TypeReference = 1,
        Construction = TypeReference << 1,
        MutableAccess = Construction << 1,
        ImmutableAccess = MutableAccess << 1,
        Interface = ImmutableAccess << 1,
        InterfaceImplementation = Interface << 1,
        GenericParameter = InterfaceImplementation << 1,
        MethodParameter = GenericParameter << 1,
        MethodGenericParameter = MethodParameter << 1,
        MethodReturnType = MethodGenericParameter << 1,
        InheritedFrom = MethodReturnType << 1,
        ContainsNested = InheritedFrom << 1,
    }
}
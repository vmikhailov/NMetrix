using System;

namespace NMetrics.Relations
{
    [Flags]
    public enum RelationKind
    {
        Construction,
        TypeReference,
        MethodCall,
        MethodCallImmutable,
        Interface,
        InterfaceImplementation,
        MethodParameter,
        MethodGenericParameter,
        MethodReturnType,
        BaseType,
        NestedType,
        GenericConstraint,
        GenericDefinition,
        GenericParameter
    }
}
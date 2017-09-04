using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Diagnostics;

namespace NMetrics
{
    [DebuggerDisplay("[{UsingType.Name}] -> [{UsedType.Name}] as {UsageKind}")]
    public class UsageInfo
    {
        public UsageInfo(
            TypeDefinition usingType,
            MethodDefinition usingMethod,
            TypeReference usedType,
            MethodReference usedMethod,
            Instruction op, int offset, UsageKind kind)
        {
            UsedType = usedType;
            UsingType = usingType;
            UsedMethod = usedMethod;
            UsingMethod = usingMethod;
            UsageKind = kind;
            Op = op;
            Offset = offset;
        }

        public TypeReference UsedType { get; }
        public MethodReference UsedMethod { get; }

        public TypeDefinition UsingType { get; }

        public MethodDefinition UsingMethod { get; }

        public UsageKind UsageKind { get; }

        public Instruction Op { get; }

        public int Offset { get; }

        public override int GetHashCode()
        {
            return UsedType.FullName.GetHashCode()
                   ^ UsingType.FullName.GetHashCode()
                   ^ UsingMethod?.FullName?.GetHashCode() ?? 0
                   ^ UsageKind.GetHashCode()
                   ^ Offset.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var x = obj as UsageInfo;
            if (x == null)
            {
                return false;
            }

            return x.UsedType.FullName.Equals(UsedType.FullName)
                   && x.UsingType.FullName.Equals(UsingType.FullName)
                   && (x.UsingMethod?.FullName?.Equals(UsingMethod?.FullName) ?? true)
                   && x.UsageKind.Equals(UsageKind)
                   && x.Offset.Equals(Offset);
        }

        public bool HasFlag(UsageKind kind)
        {
            return (UsageKind & kind) > 0;
        }

        internal static UsageInfo Implements(TypeDefinition type, TypeReference interf)
        {
            return new UsageInfo(type, null, interf, null, null, 0, UsageKind.Interface | UsageKind.TypeReference);
        }

        internal static UsageInfo ImplementedBy(TypeDefinition interf, TypeReference type)
        {
            return new UsageInfo(interf, null, type, null, null, 0, UsageKind.InterfaceImplementation | UsageKind.TypeReference);
        }

        internal static UsageInfo AsGenericParameter(TypeDefinition type, TypeReference generic)
        {
            return new UsageInfo(type, null, generic, null, null, 0, UsageKind.GenericParameter | UsageKind.TypeReference);
        }

        internal static UsageInfo AsMethodParameter(TypeDefinition type, MethodDefinition m, TypeReference paramType)
        {
            return new UsageInfo(type, m, paramType, null, null, 0, UsageKind.MethodParameter | UsageKind.TypeReference);
        }
        internal static UsageInfo AsMethodGenericParameter(TypeDefinition type, MethodDefinition m, TypeReference paramType)
        {
            return new UsageInfo(type, m, paramType, null, null, 0, UsageKind.MethodGenericParameter | UsageKind.TypeReference);
        }

        internal static UsageInfo AsMethodReturnType(TypeDefinition type, MethodDefinition m, TypeReference paramType)
        {
            return new UsageInfo(type, m, paramType, null, null, 0, UsageKind.MethodReturnType | UsageKind.TypeReference);
        }

        internal static UsageInfo InheritedFrom(TypeDefinition type, TypeReference paramType)
        {
            return new UsageInfo(type, null, paramType, null, null, 0, UsageKind.InheritedFrom | UsageKind.TypeReference);
        }

        internal static UsageInfo ContainsNested(TypeDefinition type, TypeReference paramType)
        {
            return new UsageInfo(type, null, paramType, null, null, 0, UsageKind.ContainsNested | UsageKind.TypeReference);
        }
    }
}
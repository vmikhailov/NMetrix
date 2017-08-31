using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NMetrics
{
    public class UsageInfo
    {
        public UsageInfo(
            TypeDefinition usedType,
            MethodDefinition usedMethod,
            TypeDefinition usingType, 
            MethodDefinition usingMethod, 
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

        public TypeDefinition UsedType { get; }
        public MethodDefinition UsedMethod { get; }

        public TypeDefinition UsingType { get; }

        public MethodDefinition UsingMethod { get; }

        public UsageKind UsageKind { get; }

        public Instruction Op { get; }

        public int Offset { get; }

        public override int GetHashCode()
        {
            return UsedType.FullName.GetHashCode()
                   ^ UsingType.FullName.GetHashCode()
                   ^ UsingMethod.FullName.GetHashCode()
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
                   && x.UsingMethod.FullName.Equals(UsingMethod.FullName)
                   && x.UsageKind.Equals(UsageKind)
                   && x.Offset.Equals(Offset);
        }

        public bool HasFlag(UsageKind kind)
        {
            return (UsageKind & kind) > 0;
        }
    }
}
using Mono.Cecil;

namespace NMetrics.Relations
{
    public class TypeRelation : Relation
    {
        public TypeRelation(TypeDefinition usingType, TypeReference usedType, RelationKind kind)
            : base(usingType, null, usedType, null, null, 0, kind)
        {
        }

        public TypeDefinition SourceType => (TypeDefinition)Source;
        public TypeReference TargetType => (TypeReference)Target;
    }
}
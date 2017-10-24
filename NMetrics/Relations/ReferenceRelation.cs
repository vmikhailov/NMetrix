using Mono.Cecil;

namespace NMetrics.Relations
{
    public class ReferenceRelation : Relation
    {
        public ReferenceRelation(TypeReference usingType, TypeReference usedType, RelationKind kind)
            : base(usingType, null, usedType, null, null, 0, kind)
        {
        }

        public TypeReference SourceType => Source;
        public TypeReference TargetType => Target;
    }
}
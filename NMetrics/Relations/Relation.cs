using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NMetrics.Relations
{
    [DebuggerDisplay("[{SourceName}] -> [{TargetName}] as {Kind}")]
    public class Relation
    {
        protected Relation(
            TypeReference source,
            MethodDefinition targetMettod,
            TypeReference target,
            MethodReference sourceMethod,
            Instruction op, int offset, RelationKind kind)
        {
            Source = source;
            SourceMethod = sourceMethod;
            Target = target;
            TargetMethod = targetMettod;
            Kind = kind;
            Op = op;
            Offset = offset;
        }

        public TypeReference Target { get; }
        public TypeReference Source { get; }

        public MethodReference SourceMethod { get; }

        public MethodDefinition TargetMethod { get; }

        public RelationKind Kind { get; }

        public Instruction Op { get; private set; }

        public int Offset { get; private set; }

        public virtual string SourceName => Source.GetShortName();
        public virtual string TargetName => Target.GetShortName();

        public override int GetHashCode()
        {
            return Target.FullName.GetHashCode()
                   ^ Source.FullName.GetHashCode()
                   ^ TargetMethod?.FullName?.GetHashCode() ?? 0
                   ^ SourceMethod?.FullName?.GetHashCode() ?? 0
                   ^ Kind.GetHashCode()
                   ^ Offset.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var x = obj as Relation;
            if (x == null)
            {
                return false;
            }

            return x.Target.FullName.Equals(Target.FullName)
                   && x.Source.FullName.Equals(Source.FullName)
                   && (x.TargetMethod?.FullName?.Equals(TargetMethod?.FullName) ?? true)
                   && x.Kind.Equals(Kind)
                   && x.Offset.Equals(Offset);
        }

        internal static Relation Implements(TypeDefinition type, TypeReference interf)
        {
            return new TypeRelation(type, interf, RelationKind.Interface);
        }

        internal static Relation ImplementedBy(TypeDefinition interf, TypeReference type)
        {
            return new TypeRelation(interf, type, RelationKind.InterfaceImplementation);
        }

        internal static Relation AsMethodParameter(TypeDefinition type, MethodDefinition m, TypeReference paramType)
        {
            return new Relation(type, m, paramType, null, null, 0, RelationKind.MethodParameter);
        }
        internal static Relation AsMethodGenericParameter(TypeDefinition type, MethodDefinition m, TypeReference paramType)
        {
            return new Relation(type, m, paramType, null, null, 0, RelationKind.MethodGenericParameter);
        }

        internal static Relation AsMethodReturnType(TypeDefinition type, MethodDefinition m, TypeReference paramType)
        {
            return new Relation(type, m, paramType, null, null, 0, RelationKind.MethodReturnType);
        }

        internal static Relation InheritedFrom(TypeDefinition type, TypeReference baseType)
        {
            return new TypeRelation(type, baseType, RelationKind.BaseType);
        }

        internal static Relation ContainsNested(TypeDefinition type, TypeReference nestedType)
        {
            return new TypeRelation(type, nestedType, RelationKind.NestedType);
        }

        internal static Relation GenericContraint(TypeReference type, TypeReference constraintType)
        {
            return new ReferenceRelation(type, constraintType, RelationKind.GenericConstraint);
        }

        internal static Relation GenericDefinition(TypeReference instanceType, TypeReference definition)
        {
            return new ReferenceRelation(instanceType, definition, RelationKind.GenericDefinition);
        }

        internal static Relation GenericParameter(TypeReference type, TypeReference generic)
        {
            return new ReferenceRelation(type, generic, RelationKind.GenericParameter);
        }

        internal static Relation TypeReference(TypeDefinition type, MethodDefinition method, TypeReference target)
        {
            return new Relation(type, method, target, null, null, 0, RelationKind.TypeReference);
        }

        internal static Relation MethodCall(TypeDefinition type, MethodDefinition method, TypeReference targetType, MethodReference targetMethod)
        {
            var resolvedMethod = targetMethod.SmartResolve();

            RelationKind kind = RelationKind.MethodCall;

            if (resolvedMethod != null)
            {
                if (resolvedMethod.IsConstructor)
                {
                    kind = RelationKind.Construction;
                }
                if (resolvedMethod.IsGetter)
                {
                    kind = RelationKind.MethodCallImmutable;
                }
            }
            return new Relation(type, method, targetType, targetMethod, null, 0, kind);
        }

        internal Relation WithInstruction(Instruction inst)
        {
            Op = inst;
            Offset = inst.Offset;
            return this;
        }
    }
}